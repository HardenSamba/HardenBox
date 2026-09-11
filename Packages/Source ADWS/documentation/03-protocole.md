# Détails protocolaires

Ce document décrit ce qui circule effectivement sur le port `9389/tcp`. Utile pour debugger un échec de connexion, écrire un nouveau handler, ou comprendre une trace `tcpdump`.

## Empilement

```
┌─────────────────────────────────────────────────┐
│  Application : ADWS / WS-* / Active Directory   │  ← MS-ADDM
├─────────────────────────────────────────────────┤
│  SOAP 1.2 + WS-Addressing                       │
├─────────────────────────────────────────────────┤
│  XML binaire WCF (compression dictionnaire)     │  ← MS-NBFX, MS-NBFS
├─────────────────────────────────────────────────┤
│  GSSAPI (Kerberos AES256, scellement msg)       │  ← RFC 4121
├─────────────────────────────────────────────────┤
│  Net.tcp framing (MS-NMF)                       │  ← MS-NMF
├─────────────────────────────────────────────────┤
│  TCP / 9389                                     │
└─────────────────────────────────────────────────┘
```

Spécifications Microsoft de référence :

- **MS-NMF** : .NET Message Framing Protocol
- **MS-NBFX** : .NET Binary Format: XML Data Structure
- **MS-NBFS** : .NET Binary Format: SOAP Extension
- **MS-ADDM** : Active Directory Web Services: Data Model and Common Elements

## Phase 1 — Préambule NMF

Le client envoie une **séquence fixe de records** sans aucune négociation préalable :

| Octet | Record | Contenu |
|---|---|---|
| `0x00 0x01 0x00 0x01` | `VersionRecord` | major=1, minor=0 |
| `0x02 0x02` | `ModeRecord` | mode=DUPLEX (2) |
| `0x03 …` | `ViaRecord` | URL cible : `net.tcp://samba.local:9389/ActiveDirectoryWebServices/Windows/Resource` |
| `0x04 0x03` | `KnownEncodingRecord` | encoding=BINARY_WITH_DICT (3) |
| `0x09 …` | `UpgradeRequestRecord` | type = `application/negotiate` (déclenche GSSAPI) |

Le serveur ne répond rien tant qu'il n'a pas reçu `UpgradeRequestRecord` — il accumule. Si la séquence est invalide, il ferme la connexion sans réponse.

Côté code : `src/nettcp/nmf.py` (parsing) + `classes/server/protocol.py` (machine d'état).

## Phase 2 — Négociation GSSAPI

Une fois `UpgradeRequestRecord` reçu, le serveur répond `UpgradeResponseRecord` puis tous deux échangent des **tokens GSSAPI bruts** (préfixés d'un `int32` little-endian de longueur).

```
client ──── token #1 (AP-REQ Kerberos) ──── ► serveur
client ◄─── token #2 (AP-REP)         ──── serveur
[éventuellement plusieurs allers-retours]
client ◄─── PreambleEndRecord (0x0c) ───── serveur   (= sécurité OK)
```

Le serveur utilise `samba.gensec` (via `src/nettcp/stream/gssapi.py`) — donc le keytab est **automatiquement** celui de Samba (`/var/lib/samba/private/secrets.tdb`). Pas besoin de keytab séparé.

À partir de cet instant, **chaque message est scellé** : `gss.wrap(payload)` côté envoi, `gss.unwrap(...)` côté réception. C'est invisible pour les couches supérieures.

> **Erreur fréquente :** `STATUS_NO_SUCH_USER` (NTSTATUS `0xC0000064`) côté serveur signifie que le SPN demandé par le client n'est pas dans AD. Voir [06-depannage.md](06-depannage.md).

## Phase 3 — Messages applicatifs

Format d'un message :

```
┌──────────────┬──────────────┬───────────────────────────────┐
│ 0x06 (Sized) │ varint length│  GSSAPI-wrapped( WCF binary ) │
└──────────────┴──────────────┴───────────────────────────────┘
```

Le `varint` suit l'encodage MS-NMF (continuation bit). Une fois descellé, le payload est du **XML binaire WCF**.

### XML binaire WCF (MS-NBFX)

Au lieu de `<a>b</a>`, on a une suite de **records** typés. Exemple simplifié :

| Record (octet) | Sens |
|---|---|
| `0x40 0x01 'a'` | `<a` (élément avec nom court) |
| `0x06 'b'` | texte court "b" |
| `0x01` | `>` puis `</a>` implicite (EndElement) |

Chaque type primitif a son record :

| Type XSD | Record |
|---|---|
| `xsd:int` | `Int32TextRecord` (0x88) |
| `xsd:long` | `Int64TextRecord` (0x8a) |
| `xsd:dateTime` | `DatetimeTextRecord` (0x96) |
| `xsd:string` | `Chars8TextRecord` / `Chars16TextRecord` (0x98 / 0x9a) |
| `xsd:base64Binary` | `Bytes8TextRecord` (0x9e) |
| `xsd:guid` | `GuidTextRecord` (0xb0) |

Le **dictionnaire** raccourcit encore : les chaînes Microsoft connues (`MessageID`, `Action`, `s:Body`, …) sont encodées sur 1 octet via la table statique (`src/wcf/dictionary.py`). Le dictionnaire dynamique négocié par session (MS-NBFSE) n'est pas implémenté — seule la table statique est utilisée.

Côté code :

- **Encode** (XML → records) : `src/wcf/xml2records.py` + `src/wcf/html_parser.py` (parser SAX feeding the records).
- **Decode** (records → XML) : `src/wcf/records/` (un module par catégorie de record) + `src/wcf/datatypes.py`.

## Enveloppe SOAP

Une fois le XML reconstitué, c'est du SOAP 1.2 standard avec des headers WS-Addressing :

```xml
<s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
            xmlns:a="http://www.w3.org/2005/08/addressing">
  <s:Header>
    <a:Action s:mustUnderstand="1">
      http://schemas.xmlsoap.org/ws/2004/09/transfer/Get
    </a:Action>
    <a:MessageID>urn:uuid:ece4c158-…</a:MessageID>
    <a:ReplyTo><a:Address>http://www.w3.org/2005/08/addressing/anonymous</a:Address></a:ReplyTo>
    <a:To s:mustUnderstand="1">net.tcp://samba.local:9389/.../Resource</a:To>
    <wsa:ResourceReference xmlns:wsa="…">
      <wsa:InstanceId>11111111-1111-1111-1111-111111111111</wsa:InstanceId>
      <wsa:ObjectReferenceProperty>CN=Administrator,CN=Users,DC=samba,DC=local</wsa:ObjectReferenceProperty>
    </wsa:ResourceReference>
  </s:Header>
  <s:Body>…</s:Body>
</s:Envelope>
```

`Action` détermine le handler (voir [04-handlers.md](04-handlers.md)). `MessageID` doit être renvoyé **tel quel** dans `<a:RelatesTo>` de la réponse — sans ça PowerShell ignore le retour.

## Modes de Get : WS-Transfer vs IMDA

Le module `ActiveDirectory` PowerShell emploie **deux mises en forme distinctes** pour récupérer un objet, selon le contexte (cmdlet, version, attributs demandés).

### WS-Transfer (objet entier)

```xml
<s:Body>
  <addata:user xmlns:addata="…ActiveDirectory/Data"
               xmlns:ad="…ActiveDirectory">
    <ad:objectReferenceProperty><ad:value xsi:type="xsd:string">f6aac55f-…</ad:value></ad:objectReferenceProperty>
    <addata:cn LdapSyntax="UnicodeString"><ad:value xsi:type="xsd:string">Administrator</ad:value></addata:cn>
    <addata:objectGUID LdapSyntax="OctetString"><ad:value xsi:type="xsd:base64Binary">…</ad:value></addata:objectGUID>
    <addata:objectSid LdapSyntax="SidString"><ad:value xsi:type="xsd:base64Binary">…</ad:value></addata:objectSid>
    …
    <ad:relativeDistinguishedName><ad:value xsi:type="xsd:string">CN=Administrator</ad:value></ad:relativeDistinguishedName>
    <ad:distinguishedName><ad:value xsi:type="xsd:string">CN=…</ad:value></ad:distinguishedName>
  </addata:user>
</s:Body>
```

Wrapper unique nommé d'après l'objectClass structurel. `objectReferenceProperty`
(le **GUID** EPR, pas le CN) en tête ; les synthétiques `distinguishedName` /
`relativeDistinguishedName` en `ad:` à la fin. `LdapSyntax` = nom d'énum
`DirectorySyntaxType`. Les items de `Pull` (WS-Enumeration) emploient
exactement cette forme.

### IMDA — Identity Management Directory Access

Deux sous-formes selon que la requête `<da:BaseObjectSearchRequest>` liste ou
non des `<da:AttributeType>` (discriminant `ActiveDirectoryWebService.cs`).

**Forme a — au moins un attribut demandé** : un `<da:PartialAttribute>` par
attribut, `distinguishedName` toujours émis (synthétique `ad:`) :

```xml
<s:Body>
  <da:BaseObjectSearchResponse xmlns:da="…DirectoryAccess">
    <da:PartialAttribute>
      <addata:cn LdapSyntax="UnicodeString"><ad:value xsi:type="xsd:string">Administrator</ad:value></addata:cn>
    </da:PartialAttribute>
    <da:PartialAttribute>
      <addata:objectSid LdapSyntax="SidString"><ad:value xsi:type="xsd:base64Binary">…</ad:value></addata:objectSid>
    </da:PartialAttribute>
    <da:PartialAttribute>
      <ad:distinguishedName><ad:value xsi:type="xsd:string">CN=…</ad:value></ad:distinguishedName>
    </da:PartialAttribute>
  </da:BaseObjectSearchResponse>
</s:Body>
```

**Forme b — aucun attribut demandé** : l'objet entier (forme WS-Transfer
ci-dessus) dans **un seul** `<da:PartialAttribute>`.

> `distinguishedName` est un **synthétique** : `<ad:distinguishedName>` en
> espace `ad:`, **sans** `LdapSyntax` — jamais `<addata:distinguishedName>`.

### Détection

`classes/parsing/context.py` — `is_imda` est vrai si la requête contient
`<da:IdentityManagementOperation>` **ou** `<da:BaseObjectSearchRequest>` ;
faux pour un `<wxf:Get>` nu (WS-Transfer). La forme a/b se décide ensuite sur
`context.requested_attributes`.

Les handlers qui rendent des objets (`get.py`, `pull.py`) délèguent à
`object_serializer.ObjectSerializer` (`whole_object` / `imda_projection`).

> **Bug typique** : envoyer la forme WS-Transfer en réponse à une requête IMDA donne côté Windows :
> `Élément de fin '…' attendu, trouvé 'ad:distinguishedName'`. Voir [06-depannage.md](06-depannage.md).

## Énumération paginée (WS-Enumeration)

Une cmdlet comme `Get-ADUser -Filter *` ne tient pas dans une seule réponse. Le flux est :

```
client → Enumerate {Filter, BaseDN, Scope}            ─► serveur
serveur ◄ EnumerateResponse {EnumerationContext=UUID, Expires=...}

client → Pull {EnumerationContext=UUID, MaxElements=N} ─► serveur
serveur ◄ PullResponse {Items=[...], EnumerationContext OR EndOfSequence}

[répéter Pull autant que nécessaire]

client → Release {EnumerationContext=UUID}             ─► serveur
serveur ◄ ReleaseResponse
```

Le contexte d'énumération est stocké dans `src/enumeration/enumeration.py` (en mémoire, par UUID). Comme on est en mode forking, **chaque process garde son propre cache** : la stickiness session côté Windows fait que tous les Pull pour le même Enumerate retombent sur le même process (la connexion TCP est réutilisée).

`Renew` permet au client de prolonger l'expiration ; `GetStatus` permet de vérifier l'état.

## SOAP Faults

Toute exception serveur est traduite en :

```xml
<s:Envelope …>
  <s:Header>
    <a:Action>http://schemas.xmlsoap.org/ws/2004/08/addressing/fault</a:Action>
    <a:RelatesTo>{message_id_de_la_requête}</a:RelatesTo>
  </s:Header>
  <s:Body>
    <s:Fault>
      <s:Code><s:Value>s:Receiver</s:Value></s:Code>
      <s:Reason><s:Text xml:lang="en">message</s:Text></s:Reason>
    </s:Fault>
  </s:Body>
</s:Envelope>
```

**Sans le `<a:RelatesTo>`** PowerShell pense qu'il s'agit d'une réponse non sollicitée et la jette → la cmdlet bloque jusqu'au timeout. C'est pour ça que `_fault()` dans `classes/handlers/dispatcher.py` capture `context.message_id` même si l'erreur survient avant d'appeler le handler.

Codes de fault typés (`src/faults/fault_codes.py`) :

| Code | Sens |
|---|---|
| `DirectoryError` | Erreur LDB générique |
| `InsufficientAccessRights` | Manque de droits |
| `ConstraintViolation` | Violation de contrainte (mdp trop court, attribut requis manquant, …) |
| `ObjectClassViolation` | Object class invalide |
| `EntryAlreadyExists` | DN déjà utilisé |
| `NoSuchObject` | DN cible introuvable |

## Outils de capture

Pour observer un échange réel :

```bash
# Côté DC, sur la boucle locale :
sudo tcpdump -i any -w /tmp/adws.pcap port 9389
```

Le contenu sera **chiffré** après la négo GSSAPI — pour voir le clair, journaliser `proxy.py` :

```python
# Dans tools/work/proxy.py (debug seulement) :
RECORD_LOG = "/tmp/adws-records.log"
```

Pour décoder un payload binaire à la main :
```bash
python3 -c "from src.wcf.records import dump_records; \
            data=open('/tmp/payload.bin','rb').read(); \
            print(dump_records(data))"
```

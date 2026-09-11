# Architecture

## Pourquoi ce projet existe

Samba AD fournit un contrôleur de domaine compatible Active Directory au niveau **LDAP, Kerberos, SMB et DNS**, mais **n'expose pas** ADWS (port 9389/tcp). Sans ADWS, les outils PowerShell Microsoft tombent en erreur dès la première cmdlet :

```
Get-ADUser : Unable to find a default server with Active Directory Web Services running
```

Ce projet implémente côté Linux **le même protocole** que `Microsoft.ActiveDirectory.WebServices.exe` afin que `ActiveDirectory` PowerShell, ADAC, et tout outil basé sur le namespace `Microsoft.ActiveDirectory.Management` fonctionnent **sans modification**.

## Vue à 10 km

```
PowerShell / ADAC (Windows)                                Samba DC (Linux)
─────────────────────────────                              ────────────────
Get-ADUser administrator                                   python3 -m src.main
        │                                                          │
        │  net.tcp://samba.local:9389                              │
        │  ┌──────────────────────────────────┐                    │
        └──┤  1. Préambule (NMF)              ├──── src/nettcp/ ────►  │
           │  2. Négociation GSSAPI/Kerberos  │                    │
           │  3. Trame SOAP binaire (WCF)     │                    │
           └──────────────────────────────────┘                    │
                          │                                        │
                          ▼                                        ▼
                   src/wcf/xml2records.py ◄──── XML lisible ──  classes/handlers/
                          │                                   dispatcher
                          ▼                                        │
                  classes/handlers/                               │
                  (Get, Pull, CRUD, …)                             │
                          │                                        │
                          └─────► samba.samdb (python3-samba)──────┘
                                          │
                                          ▼
                                    sam.ldb (Samba)
```

## Couches du serveur

Le code est organisé **par responsabilité**, pas par fonctionnalité métier. Chaque couche ne connaît que celle juste en dessous.

### 1. `src/nettcp/` — Net.tcp framing

Implémente [MS-NMF] (.NET Message Framing Protocol).

| Fichier | Rôle |
|---|---|
| `nmf.py` | Records de préambule (Version, Mode, Via, Encoding, UpgradeRequest, EndRecord, …) |
| `protocol2xml.py` | Conversion record binaire → fragment XML |
| `tools/work/proxy.py` | Outil de capture / rejeu pour debug (hors runtime, déplacé dans `tools/work/`) |
| `stream/socket.py` | Wrapper bufferisé du socket TCP |
| `stream/gssapi.py` | Encapsulation GSSAPI : négocie Kerberos, scelle/descelle chaque message après l'upgrade sécurité |

C'est la couche qui parle « le câble ». Au-dessus, les payloads sont du XML.

### 2. `src/wcf/` — Encodage WCF binaire

Implémente [MS-NBFX] (.NET Binary Format: XML Data Structure).

| Fichier | Rôle |
|---|---|
| `xml2records.py` | XML texte → suite de records binaires (à émettre) |
| `records/` | Décodage records binaires → XML texte (à recevoir) |
| `dictionary.py` | Dictionnaire statique Microsoft (préfixe les chaînes connues sur 1 octet) |
| `datatypes.py` | Types primitifs WCF (Int32TextRecord, DatetimeTextRecord, GuidTextRecord, …) |
| `html_parser.py` / `html_parse_error.py` | Parser SAX-like utilisé pour fabriquer les records depuis le XML applicatif |

Tout ce qui sort du dispatcher est **du XML SOAP normal** ; cette couche le compresse en binaire WCF avant émission.

### 3. Logique ADWS — `classes/<phase>/` + `src/<phase>/`

Coeur applicatif « maison », organisé **par phase du protocole** (cf.
[03-protocole.md](03-protocole.md)). Chaque phase a un dossier de même nom dans
**deux arbres miroirs** : `classes/<phase>/` contient les **classes** (un
fichier = une classe), `src/<phase>/` contient le **code sans classe** de la
même phase (constantes, fonctions, tables de routage, singletons). `src/nettcp/`
(framing NMF + GSSAPI) et `src/wcf/` (encodage WCF binaire) sont du code tiers
vendoré, intégré sous `src/` mais exclu des normes §6.1 (cf. `pyproject.toml`).

| Phase (dossier) | `classes/<phase>/` (une classe/fichier) | `src/<phase>/` (non-classes) |
|---|---|---|
| `server` | `config`, `server`, `connection`, `protocol`, `service_context` | — |
| `soap` | `soap_envelope` | `namespaces` |
| `parsing` | `context`, `body_parser`, `attribute_parser`, `controls_parser`, `filter_parser` | — |
| `serialization` | `object_serializer`, `xml_renderer`, `value_codec`, `ldb_value`, `ca_elements`, `constructed_attrs`, `dc/domain/group_serializer`, `name_formats`, `syntaxes`, `syntax_cache` | `value_special`, `name_filters`, `name_renderers`, `syntax_constants` |
| `handlers` | `dispatcher`, `handler_registry`, `base` + un fichier par action | `handler_routing` |
| `enumeration` | `enumeration_cache`, `enumeration_context`, `subscription_cache` | `enumeration`, `subscription_id`, `subscription_registry` |
| `directory` | `samba_db`, `ldap_attr_builder`, `ldap_session_pool`, `schema_cache`, `domain_loader`, `principal_loader`, `instance_map`, `quota_tracker`, `reservation_token` | `fsmo_roles` |
| `faults` | `exceptions`, `faults`, `typed_faults` | `fault_codes` |

`dispatcher` reçoit un `Record`, parse en `RequestContext`, route vers le bon
handler et encode la réponse ; `parsing/` découpe les fragments XML ;
`serialization/` rend les objets AD par syntaxe LDAP. Le lavage SDDL
(`nTSecurityDescriptor`) n'est pas encore câblé.

### 4. `classes/handlers/` — Une action = un fichier

Un fichier par action SOAP. Tous héritent de `BaseHandler` et exposent `handle(context: RequestContext) → str` (XML de réponse).

Voir [04-handlers.md](04-handlers.md) pour la liste complète.

### 5. `classes/server/server.py` + `classes/server/connection.py` + `classes/server/protocol.py`

| Fichier | Rôle |
|---|---|
| `server.py` | `socketserver.ForkingTCPServer` qui écoute 9389/tcp |
| `connection.py` | Pour chaque connexion : préambule → upgrade GSSAPI → boucle messages |
| `protocol.py` | Machine d'état des phases du protocole NMF |

> **Important :** `ForkingTCPServer` signifie que **chaque connexion est un process séparé**. Toute ressource (cache `SamDB`, dictionnaires WCF) doit être ouverte **dans le handler**, pas au niveau module — sinon elle est partagée entre forks et les locks sautent.

## Flux d'une requête

Exemple : `Get-ADUser Administrator`.

1. **Côté Windows** — Le module `ActiveDirectory` ouvre une connexion `net.tcp` sur `samba.local:9389`.
2. **Préambule (Phase 1)** — `VersionRecord`, `ModeRecord`, `ViaRecord`, `KnownEncodingRecord`, `UpgradeRequestRecord` envoyés en série. Le serveur valide chaque record dans `classes/server/protocol.py`.
3. **Sécurité (Phase 2)** — Le client passe en mode TLS-like via GSSAPI. Plusieurs allers-retours `update()` jusqu'à obtention d'un contexte Kerberos. À partir de là tous les messages sont scellés (`wrap`/`unwrap`).
4. **Message SOAP** — Le client envoie un payload binaire WCF.
5. **Décodage** — `nettcp` lit le record, extrait la payload chiffrée, GSSAPI la déchiffre, `src/wcf/xml2records.py` la transforme en XML texte.
6. **Routage** — `RequestContext.from_xml(xml)` extrait l'action (`http://schemas.xmlsoap.org/ws/2004/09/transfer/Get`), le message ID, le DN cible. `HandlerRegistry.resolve()` retourne `GetHandler`.
7. **Exécution** — `GetHandler.handle()` appelle `samba.samdb.search()`, formate le résultat selon que la requête est en mode **WS-Transfer** (un `<addata:user>...</addata:user>`) ou **IMDA** (chaque attribut dans son propre `<da:PartialAttribute>`).
8. **Encodage retour** — XML SOAP → records WCF → bytes scellés GSSAPI → trame Net.tcp.
9. **Côté Windows** — `Get-ADUser` reçoit l'objet, le désérialise, l'affiche.

Toutes les erreurs serveur sont converties en **SOAP fault** par le dispatcher (avec `<a:Action>` et `<a:RelatesTo>` qui réfèrent au `MessageID` original — sans ça PowerShell ignore la réponse).

## Décisions de conception

### Pourquoi `ForkingTCPServer` ?

Samba `python3-samba` n'est **pas thread-safe** (les structures C internes ne supportent pas l'accès concurrent). Le forking offre une isolation totale par requête. Coût : un fork par connexion (acceptable car les clients PowerShell réutilisent la connexion pour plusieurs cmdlets).

### Pourquoi parser le XML SOAP plutôt que générer/parser directement les records WCF ?

Le SOAP textuel reste **lisible et debuggable**. La couche `src/wcf/` est un encodeur/décodeur réversible : on peut tout journaliser en XML. Le coût CPU de l'aller-retour est négligeable face au temps réseau et LDB.

### Pourquoi un fichier = un handler ?

Lisibilité, isolation, et règle de projet : **5 fonctions max par fichier, une classe max par fichier**. Ajouter une nouvelle action SOAP devient mécanique (voir [04-handlers.md](04-handlers.md)).

### Pourquoi des modes IMDA et WS-Transfer séparés ?

Le module PowerShell `ActiveDirectory` envoie les requêtes Get sous **deux formes selon la version** :

- **WS-Transfer** : enveloppe = `<addata:user>` contenant tous les attributs (réponse compacte).
- **IMDA** (Identity Management Directory Access) : enveloppe = `<da:BaseObjectSearchResponse>` avec un `<da:PartialAttribute>` par attribut.

Le détecteur est dans `classes/parsing/context.py` (`is_imda`) et chaque handler choisit le rendu adapté.

## Limites connues

- **Changement de mot de passe self-service** (`Set-ADAccountPassword` avec ancien + nouveau) non supporté : le module Samba `password_hash` exige l'adresse réseau du client (`remoteAddress`) sur ce chemin, non fournissable via une connexion `SamDB` locale (un `tsocket_address` ne se construit pas depuis Python). Le **reset administrateur** (`-Reset`) fonctionne. Piste : connexion `ldapi://` authentifiée ou délégation de l'identité du client GSSAPI.
- **Trusts inter-domaines / inter-forêts** non supportés (le code suppose un seul domaine local).
- **Attributs binaires** partiellement supportés (les plus courants — `objectGUID`, `objectSid`, `nTSecurityDescriptor` — fonctionnent ; les attributs custom binaires d'extension de schéma sont rendus en base64 brut).
- **Pagination LDAP étendue** : les contrôles `PagedResults` côté client sont acceptés mais le serveur fait un seul `search()` complet et pagine côté handler.
- **Filtres LDAP exotiques** : les opérateurs étendus type `1.2.840.113556.1.4.804` (LDAP_MATCHING_RULE_BIT_OR) sont supportés ; ceux non documentés peuvent ne pas l'être (voir `classes/parsing/filter_parser.py`).

## Pour aller plus loin

- [02-installation.md](02-installation.md) — comment déployer
- [03-protocole.md](03-protocole.md) — détails NMF / WCF / SOAP
- [04-handlers.md](04-handlers.md) — comment ajouter une action

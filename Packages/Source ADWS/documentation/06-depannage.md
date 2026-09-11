# Dépannage

Erreurs concrètement rencontrées dans des runs réels, avec leurs causes et leurs corrections.

## Index

| Symptôme | Section |
|---|---|
| `STATUS_NO_SUCH_USER` à la connexion | [§ Kerberos / SPN](#1-kerberos--spn) |
| `Le site distant a fermé le flux de transport` | [§ Connexion fermée](#2-connexion-fermée-côté-windows) |
| `Élément de fin '…' attendu, trouvé '…'` | [§ XML mal formé](#3-élément-de-fin-attendu) |
| `[PULL] Error: __str__ returned non-string` | [§ MessageElement](#4-pull-error-__str__-returned-non-string) |
| `[ENUMERATE] Search failed: empty base DN` | [§ Base DN vide](#5-empty-base-dn) |
| Cmdlet bloque jusqu'au timeout | [§ RelatesTo manquant](#6-cmdlet-bloque) |
| `Unable to find a default server` / ADAC ne se connecte pas (`1355`) | [§ Découverte ADWS](#7-unable-to-find-a-default-server) |
| Mutations refusées | [§ Droits insuffisants](#8-droits-insuffisants) |
| Écriture avec accents → `ADServerDownException` / `undefined entity` | [§ Caractères non-ASCII](#9-caractères-non-ascii-dans-une-écriture) |

---

## 1. Kerberos / SPN

### Symptôme

Côté serveur, dans les logs :

```
[SECURITY] ✗ Error: (3221225572, 'The specified account does not exist.')
Traceback (most recent call last):
  ...
  File ".../src/nettcp/stream/gssapi.py", line 119, in update
    ...
samba.NTSTATUSError: (3221225572, 'The specified account does not exist.')
```

`3221225572` = `0xC0000064` = `STATUS_NO_SUCH_USER`.

Côté Windows, la première cmdlet (typiquement `Get-ADRootDSE`) échoue avec :

```
The remote endpoint has terminated the transport stream
```

### Cause

Le ticket Kerberos que présente le client cible un **SPN qui n'est pas enregistré** sur le compte machine du DC. Samba ne peut pas dériver la clé pour déchiffrer.

### Vérification

```bash
sudo samba-tool spn list "$(hostname -s)$"
```

Il faut au minimum **`host/<fqdn>`** dans la liste.

### Correction

```bash
sudo samba-tool spn add "host/$(hostname -f)" "$(hostname -s)$"
sudo samba-tool spn add "HOST/$(hostname -f)" "$(hostname -s)$"
```

Côté Windows, purger le cache de tickets pour forcer une nouvelle demande :

```powershell
klist purge
```

> **Attention :** une heure désynchronisée entre Windows et le DC (> 5 minutes) produit la **même** erreur. Vérifier `timedatectl status` côté DC et `w32tm /query /status` côté Windows.

---

## 2. Connexion fermée côté Windows

### Symptôme

```
Get-ADUser : Le site distant a fermé le flux de transport.
```

### Causes possibles

1. **Erreur GSSAPI** au préambule (voir [§ 1](#1-kerberos--spn)).
2. **Crash du handler** côté serveur — la couche dispatcher convertit normalement en SOAP fault, mais une exception dans la couche WCF/NMF coupe la connexion brute.
3. **Pare-feu** qui ferme la connexion après un délai.

### Diagnostic

Sur le DC :

```bash
sudo journalctl -u samba-adws -n 200 --no-pager
```

Chercher la dernière trace avant la fermeture. Si c'est un `Traceback`, c'est un bug serveur ; si c'est `[CONNECTION CLOSED]` sans erreur, c'est probablement réseau.

---

## 3. Élément de fin attendu

### Symptôme

Côté Windows, exception PowerShell :

```
Élément de fin 'subSchema' attendu, trouvé 'ad:distinguishedName'
```

(ou une autre paire élément/objectClass selon la cmdlet)

### Cause

Le serveur a renvoyé une réponse en **mode WS-Transfer** (wrapper `<addata:objectClass>...`) alors que le client a demandé en **mode IMDA** (chaque attribut dans son propre `<da:PartialAttribute>`). Le parser AD côté Windows applique le schéma XML strict et rejette.

### Correction

Dans le handler concerné, vérifier `context.is_imda` et choisir le bon rendu :

```python
if context.is_imda:
    attributes_xml = self._render_attributes_for_imda(attributes)
    body_xml = f'<da:BaseObjectSearchResponse>{attributes_xml}</da:BaseObjectSearchResponse>'
else:
    attributes_xml = self._render_attributes_for_wst(attributes)
    body_xml = f'<addata:{obj_class}>{attributes_xml}</addata:{obj_class}>'
```

S'assurer aussi que `distinguishedName` est ajouté comme attribut explicite (PowerShell le réclame en clé d'identité même si LDB le retourne via la propriété `dn`).

Voir [03-protocole.md § Modes de Get](03-protocole.md#modes-de-get--ws-transfer-vs-imda).

---

## 4. PULL Error: __str__ returned non-string

### Symptôme

```
[PULL] Error: __str__ returned non-string (type NoneType)
```

### Cause

`str()` appelé sur un `ldb.MessageElement` vide ou exotique. La méthode `__str__` C de Samba retourne `None` dans certains cas → `TypeError`.

C'est typique quand on fait :

```python
class_val = val[0] if isinstance(val, list) else val   # MessageElement n'est pas une list
object_class = str(class_val)                          # boom si __str__ → None
```

### Correction

Dérouler explicitement le `MessageElement` en `bytes` puis décoder :

```python
try:
    last_raw = val[-1] if hasattr(val, '__getitem__') and len(val) > 0 else val
except (IndexError, TypeError):
    last_raw = None

if isinstance(last_raw, bytes):
    object_class = last_raw.decode('utf-8', errors='replace')
elif last_raw is not None:
    try:
        object_class = str(last_raw)
    except TypeError:
        object_class = 'top'
```

Idem dans tout point qui consomme une valeur d'attribut LDB : **toujours** assumer qu'on reçoit un `MessageElement`, pas une `list`.

---

## 5. Empty base DN

### Symptôme

```
[ENUMERATE] Search failed: (32, 'empty base DN at .../partition.c:821')
```

### Cause

Le client envoie un `<adlq:BaseObject>` vide. C'est légal pour PowerShell (« cherche dans tout le naming context par défaut ») mais Samba LDB refuse une recherche sans base DN.

### Correction

Dans `enumerate.py`, fallback sur le naming context :

```python
base_dn = context.base_dn or ''
if not base_dn:
    base_dn = self.db.naming_context() or ''
```

---

## 6. Cmdlet bloque

### Symptôme

`Get-ADUser` ne retourne jamais, ou retourne après un long délai (`Operation timed out`).

### Cause classique

Le serveur a renvoyé une **SOAP fault sans `<a:RelatesTo>`**. Sans cet en-tête, le client PowerShell pense qu'il s'agit d'une réponse non sollicitée et l'ignore → la cmdlet attend toujours.

### Vérification

Activer le journal des messages :

```bash
sudo journalctl -u samba-adws -f | grep -E "fault|RelatesTo"
```

### Correction

Dans `dispatcher._fault()`, **toujours** échapper avec :

```python
relates_to = context.message_id if context else ""
return SoapEnvelope.fault(relates_to, "s:Receiver", message)
```

Et s'assurer que `SoapEnvelope.fault()` injecte bien `<a:RelatesTo>{relates_to}</a:RelatesTo>` dans le header.

---

## 7. Unable to find a default server

### Symptôme

```
Get-ADUser : Unable to find a default server with Active Directory Web Services running.
```

### Causes — d'abord les cas simples

1. Le service `samba-adws` n'écoute pas (`ss -tlnp | grep 9389` à vérifier).
2. Pare-feu qui bloque 9389/tcp depuis le client.
3. DNS du client ne pointant pas vers le DC Samba (le DC locator a besoin des SRV records).

### Cause de fond : limite Samba `DS_WS` (ADAC + cmdlets SANS `-Server`)

Si tout ce qui précède est bon (service up, port joignable, `nltest /dsgetdc` OK) mais que **la
découverte automatique** échoue encore — typiquement **ADAC** et les cmdlets lancées **sans
`-Server`** —, c'est une **limite de Samba**, pas un bug du projet.

La découverte du module AD et d'ADAC n'appelle pas un simple « trouve un DC » : elle appelle le
DC locator avec le flag **`DS_WEB_SERVICE_REQUIRED`** (« ne me renvoie un DC **que s'il annonce
faire tourner ADWS** »). Or **Samba n'émet pas le flag `DS_WS` (`NBT_SERVER_ADS_WEB_SERVICE`,
0x2000) dans sa réponse Netlogon/CLDAP**. Ce projet fournit le *service* ADWS (TCP 9389) mais ne
peut pas modifier l'annonce CLDAP de Samba (UDP 389). Résultat : `ERROR_NO_SUCH_DOMAIN (1355)` /
`ADServerDownException`, **même si ADWS tourne et répond** (une connexion `-Server` directe marche).

### Diagnostic décisif

```powershell
nltest /dsgetdc:samba.local        # OK  -> le DC est localisable (DNS bon)
nltest /dsgetdc:samba.local /ws    # 1355 -> Samba n'annonce pas ADWS = LA cause
Test-NetConnection 192.168.141.10 -Port 9389   # True -> le service répond bien
```

Si `/ws` échoue alors que sans `/ws` réussit : c'est la limite `DS_WS`. **Rien à corriger côté
serveur.**

### Contournements

- **PowerShell** : toujours passer **`-Server <DC>`** (court-circuite la découverte) — chemin
  supporté/validé. Poser `$PSDefaultParameterValues` une fois (cf. [README](../README.md)).
- **Console graphique** : utiliser **ADUC (`dsa.msc`)**, qui parle **LDAP** direct à Samba et
  n'utilise **ni** ADWS **ni** la découverte par flag — il fonctionne là où ADAC est bloqué.
- **ADAC** : exige le flag `DS_WS`. Le débloquer imposerait de **patcher + recompiler Samba**
  (positionner `NBT_SERVER_ADS_WEB_SERVICE` dans `source4/cldap_server/netlogon.c`) — lourd,
  fragile, et déconseillé sur un DC de production géré par apt. **Aucune option `smb.conf`** ne
  l'active.

---

## 8. Droits insuffisants

### Symptôme

```
Set-ADUser : Insufficient access rights to perform the operation
```

ou côté serveur :

```
[CRUD] InsufficientAccessRights: ...
```

### Cause

Le compte authentifié (typiquement celui qui a fait `kinit`) n'a pas les droits AD pour la modification demandée.

### Correction

- Vérifier que l'utilisateur courant est dans `Domain Admins` ou a une délégation explicite.
- Tester en LDAP direct pour isoler ADWS du droit AD :

  ```bash
  ldbedit -H ldap://samba.local -U Administrator \
          -b "CN=cible,CN=Users,DC=samba,DC=local"
  ```

  Si `ldbedit` refuse aussi → c'est un problème de droits AD, pas un bug ADWS.

---

## 9. Caractères non-ASCII dans une écriture

### Symptôme

Une lecture (`Get-AD*`) fonctionne, mais une écriture (`Set-ADUser`, `New-ADUser`, …)
dont **une valeur contient un accent** (`créé`, `Ingénieur`, `Société`) échoue côté Windows :

```
Set-ADUser : Impossible de contacter le serveur. Il se peut que le serveur n'existe pas,
est actuellement hors service ou ne dispose pas des services Web Active Directory.
    + FullyQualifiedErrorId : ActiveDirectoryServer:0,...Commands.SetADUser
```

(`ADServerDownException`) — alors que le serveur écoute et répond aux lectures. Côté DC :

```bash
sudo journalctl -u samba-adws -n 50 --no-pager | grep -A2 DISPATCH
```

```
[DISPATCH] unexpected failure
xml.etree.ElementTree.ParseError: undefined entity: line N, column M
ValueError: Failed to parse request XML: undefined entity
```

### Cause

Au **décodage** de la requête WCF binaire en texte XML, la fonction `escape()` de la
bibliothèque vendorisée (`src/wcf/records/text.py`) convertissait **tout** caractère présent dans
`html.entities.codepoint2name` en **entité nommée HTML** : `é` → `&eacute;`, `à` → `&agrave;`…
Or XML ne prédéfinit que **cinq** entités (`lt gt amp quot apos`). `créé` produisait
`cr&eacute;&eacute;`, et `ET.fromstring()` (dans [`classes/parsing/context.py`](../classes/parsing/context.py))
levait `undefined entity`. L'exception remontait, le process enfant `ForkingTCPServer` mourait,
la connexion se coupait → le client traduit ça en `ADServerDownException`. Les lectures
passaient car elles n'envoient pas de valeur accentuée.

### Correction

Ne mapper que les entités **valides en XML** ; laisser tout autre point de code littéral
(le texte XML décodé est déjà une `str` Unicode, `ElementTree` gère `é` nativement) :

```python
# src/wcf/records/text.py
_XML_ENTITY_BY_CP = {0x3C: '&lt;', 0x3E: '&gt;', 0x26: '&amp;', 0x22: '&quot;'}

def escapecp(cp):
    return _XML_ENTITY_BY_CP.get(cp, chr(cp))
```

Vérifié de bout en bout : `Set-ADUser demo -Description 'créé via ADWS souverain'
-Replace @{title='Ingénieur Réseau'}` réussit et les valeurs font l'aller-retour intactes.

---

## Activer plus de logs

Par défaut le serveur loggue en `INFO`. Pour le rendre verbeux, modifier dans [`classes/server/config.py`](../classes/server/config.py) :

```python
@dataclass
class LogConfig:
    level: int = logging.DEBUG     # au lieu de logging.INFO
```

Et redémarrer :
```bash
sudo systemctl restart samba-adws
```

Les fichiers de log sont :
- `adws.log` (fichier dans le répertoire de lancement, rotation 10 Mo × 5)
- `journalctl -u samba-adws` (si lancé en service)

---

## Capturer un échange complet pour analyse

```bash
# 1. Capture brute
sudo tcpdump -i any -w /tmp/adws.pcap port 9389

# 2. Reproduire l'erreur depuis Windows
# Get-ADUser ... -Server samba.local

# 3. Stop tcpdump

# 4. Activer le proxy de debug pour voir le XML clair (avant chiffrement) :
# Dans tools/work/proxy.py :
# RECORD_LOG = "/tmp/adws-records.log"
```

Le `.pcap` montre la trame TCP/NMF (chiffrée GSSAPI à partir de la phase 3), le `RECORD_LOG` montre le XML applicatif clair pour chaque message.

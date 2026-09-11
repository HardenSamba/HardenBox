# Documentation Samba AD ADWS

Index complet de la documentation projet.

## Par profil

### Je veux juste l'installer

→ [02-installation.md](02-installation.md), puis [05-tests.md § Test-ADWS-Sovereign.ps1](05-tests.md#1-test-adws-sovereignps1-validation-principale).

### Je veux comprendre comment c'est construit

→ [01-architecture.md](01-architecture.md), puis [03-protocole.md](03-protocole.md).

### J'ai une erreur en production

→ [06-depannage.md](06-depannage.md). L'index en haut du fichier mappe symptôme → section.

### Je veux ajouter une fonctionnalité

→ [04-handlers.md § Ajouter un nouveau handler](04-handlers.md#ajouter-un-nouveau-handler).

### Je veux écrire un test de régression

→ [05-tests.md § test_handlers.py](05-tests.md#2-test_handlerspy-handlers-en-isolation).

---

## Sommaire détaillé

### [01-architecture.md](01-architecture.md)
- Pourquoi le projet existe
- Vue à 10 km : flux client → serveur
- Couches du serveur : `src/nettcp/`, `src/wcf/`, `classes/` + `src/` (par phase), `classes/server/server.py`
- Flux d'une requête `Get-ADUser`, étape par étape
- Décisions de conception (forking, IMDA vs WS-Transfer, …)
- Limites connues

### [02-installation.md](02-installation.md)
- Pré-requis Linux et Windows
- Configuration Kerberos (`/etc/krb5.conf`, SPN)
- Installation en une commande (`git clone` + `sudo tools/installation/install.sh`)
- Lancement manuel et service systemd
- Vérification (`Test-NetConnection`, `Get-ADRootDSE`)
- Pas de virtualenv ni de pip (tout via apt)
- Désinstallation

### [03-protocole.md](03-protocole.md)
- Empilement des protocoles (NMF, WCF binary XML, GSSAPI, SOAP)
- Phase 1 — Préambule NMF
- Phase 2 — Négociation GSSAPI
- Phase 3 — Messages applicatifs (WCF binaire, dictionnaire)
- Enveloppe SOAP + WS-Addressing
- Modes de Get : WS-Transfer vs IMDA
- Énumération paginée (Enumerate / Pull / Release / Renew)
- Format des SOAP faults
- Outils de capture (`tcpdump`, proxy debug)

### [04-handlers.md](04-handlers.md)
- Catalogue complet des handlers (WS-Transfer, WS-Enumeration, actions ADWS)
- Routage `action SOAP → handler`
- Contrat `BaseHandler.handle()`
- Procédure pour ajouter un nouveau handler
- Helpers fréquemment utilisés
- Bonnes pratiques

### [05-tests.md](05-tests.md)
- Vue d'ensemble des trois suites
- `Test-ADWS-Sovereign.ps1` (validation E2E PowerShell)
- `test_handlers.py` (handlers en isolation)
- `test_adws.py` (diagnostic environnement)
- Workflow recommandé

### [06-depannage.md](06-depannage.md)
- Kerberos / SPN (`STATUS_NO_SUCH_USER`)
- Connexion fermée par le serveur
- XML mal formé (élément attendu)
- `MessageElement` mal déballé
- Base DN vide
- Cmdlet qui bloque (RelatesTo manquant)
- ADWS introuvable côté Windows
- Droits insuffisants
- Activation des logs DEBUG
- Capture d'un échange complet

---

## Conventions des exemples

- **`samba.local`** : FQDN factice du DC dans tous les exemples. Remplacer par le tien.
- **`SAMBA.LOCAL`** : realm Kerberos correspondant (toujours en majuscules).
- **`Administrator`** : compte admin par défaut Samba. Selon ton domaine ce peut être un autre nom.
- **`/root/SambaAD_ADWS`** : chemin d'installation par défaut (dossier cloné, lancé par `install.sh`). Adapter à ta convention.

## Règles de contribution à la doc

- **Français** côté documentation, **anglais** côté code (cf. `CLAUDE.md`).
- Pas d'emoji décoratif — l'icône doit porter une information (✅/❌ acceptables sur des tableaux d'état).
- Les chemins de fichiers cités sont des **liens cliquables** vers le fichier source.
- Les blocs de code shell sont **copiables tels quels** sur un Debian/Ubuntu standard, sans variable à substituer (sinon utiliser `<placeholder>` explicite).
- Toute correction d'un comportement décrit doit aussi mettre à jour la doc dans le même PR.

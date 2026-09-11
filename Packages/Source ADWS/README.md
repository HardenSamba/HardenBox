# Samba AD — ADWS (version souveraine)

Implémentation **100 % Python, sans aucun composant Microsoft** des **Active Directory
Web Services** (ADWS, `9389/tcp`) pour un contrôleur de domaine **Samba AD**.

Elle fait le pont entre les requêtes WS-\* des clients Windows et l'annuaire Samba (LDB).
Résultat : un poste Windows pilote le domaine Samba avec ses outils habituels —
`Get-ADUser`, `Set-ADUser`, le module PowerShell `ActiveDirectory`, la console **ADUC** —
quasiment comme face à un AD Microsoft (ADAC mis à part — voir [Limites connues](#limites-connues)).

> **Objectif stratégique :** une alternative souveraine à Microsoft AD, ouverte et auditable,
> sans renoncer à l'outillage PowerShell existant.

**État validé** (8 juin 2026, vrai client Windows 11 -> DC Samba) : suite officielle **26/28**,
batterie de parité admin **18/18**. Les 2 écarts restants ne sont pas des bugs serveur
(voir [Limites connues](#limites-connues)).

---

## Sommaire

- [Comment ça marche](#comment-ça-marche)
- [Installation sur le DC Samba](#installation-sur-le-dc-samba)
- [Vérifier que ça tourne](#vérifier-que-ça-tourne)
- [Depuis Windows : interroger l'annuaire](#depuis-windows--interroger-lannuaire)
- [Depuis Windows : administrer](#depuis-windows--administrer)
- [Limites connues](#limites-connues)
- [Documentation](#documentation)

---

## Comment ça marche

Pile protocolaire (bas → haut), tout côté Linux, aucun binaire .NET :

| Couche | Rôle | Code |
|---|---|---|
| [MS-NMF] | Cadrage .NET Message Framing | `src/nettcp/nmf.py` |
| GSSAPI / SPNEGO | Kerberos AES (seal/unseal) via `samba.gensec` | `src/nettcp/stream/gssapi.py` |
| [MS-NBFX/NBFS] | XML binaire WCF + dictionnaire | `src/wcf/` |
| SOAP 1.2 + WS-Addressing | Enveloppe applicative | `classes/soap/`, `src/soap/` |
| WS-Transfer / WS-Enumeration / IMDA | CRUD, énumération, data model AD | `classes/handlers/` |

**Authentification : Kerberos uniquement** (pas de mot de passe en clair). L'accès à
l'annuaire passe par le `SamDB` local, ouvert **paresseusement et par processus** (le serveur
est un `ForkingTCPServer` car `python3-samba` n'est pas thread-safe).

---

## Installation sur le DC Samba

Le dépôt étant **déjà cloné sur le contrôleur de domaine Samba** (Debian/Ubuntu), tout
s'installe et démarre avec **une seule commande**, en `root` :

```bash
cd SambaAD_ADWS        # le dossier cloné
sudo tools/installation/install.sh
```

C'est tout. `install.sh` est idempotent et souverain (bash lisible, aucun binaire) : il
installe les paquets système via **apt** (**aucun `pip`, aucun virtualenv,
aucun `--break-system-packages`**), enregistre le SPN `host/<fqdn>` s'il manque, écrit la
config `/etc/default/samba-adws`, installe et démarre le service systemd (rendu depuis
[`tools/installation/samba-adws.service`](tools/installation/samba-adws.service)) et ouvre `9389/tcp`.

| Action | Commande |
|---|---|
| **Installer + démarrer** | `sudo tools/installation/install.sh` |
| **Arrêter proprement** | `sudo tools/installation/stop.sh` |
| Redémarrer | `sudo systemctl restart samba-adws` |
| Reconfigurer (host/port/log) | éditer `/etc/default/samba-adws` puis redémarrer |
| Désinstaller | `sudo tools/installation/install.sh --uninstall` |
| Suivre les logs | `journalctl -u samba-adws -f` |

> **Pré-requis :** un DC Samba AD **déjà provisionné** — `samba-tool domain info "$(hostname -d)"`
> répond et `systemctl is-active samba-ad-dc` vaut `active`. Lancement manuel (debug),
> Kerberos/SPN et variantes : [documentation/02-installation.md](documentation/02-installation.md).

---

## Vérifier que ça tourne

Sur le DC :

```bash
ss -tlnp | grep 9389                 # LISTEN 0.0.0.0:9389 (python3)
journalctl -u samba-adws -f          # suivre les logs en direct
```

Depuis un poste Windows joint au domaine :

```powershell
Test-NetConnection samba.local -Port 9389     # joignabilité TCP brute
Get-ADRootDSE -Server samba.local             # si defaultNamingContext s'affiche, c'est bon
```

---

## Depuis Windows : interroger l'annuaire

> **Pré-requis client :** module RSAT `ActiveDirectory`.
> `Add-WindowsCapability -Online -Name "Rsat.ActiveDirectory.DS-LDS.Tools~~~~0.0.1.0"`
> (Windows 10/11) ou `Install-WindowsFeature RSAT-AD-PowerShell` (Server).

### Préparer la session (une fois)

`-Server` accepte le FQDN (`samba.local`) **ou l'IP** du DC. Si le poste n'a pas de ticket
Kerberos pour le domaine, fournir `-Credential`.

```powershell
Import-Module ActiveDirectory

$Server = 'samba.local'                         # ou l'IP du DC, ex. 192.168.141.10
$cred   = Get-Credential                        # ex. SAMBA\Administrator

# Rendre -Server / -Credential / -AuthType implicites pour toutes les cmdlets AD.
# (Indispensable notamment pour Get-ADRootDSE qui, sinon, n'envoie pas le credential.)
$PSDefaultParameterValues = @{
    '*-AD*:Server'     = $Server
    '*-AD*:Credential' = $cred
    '*-AD*:AuthType'   = 'Negotiate'
}
```

### Lecture / information

```powershell
# --- Topologie ---
Get-ADRootDSE | Format-List defaultNamingContext, schemaNamingContext, configurationNamingContext
Get-ADDomain  | Format-List Name, DNSRoot, NetBIOSName, DomainSID, DomainMode, PDCEmulator
Get-ADForest  | Format-List Name, ForestMode, GlobalCatalogs
Get-ADDomainController | Format-List Name, HostName, IPv4Address, IsGlobalCatalog

# --- Objets (WS-Transfer Get) ---
Get-ADUser Administrator -Properties *                       # objet complet
Get-ADUser Administrator -Properties memberOf, lastLogonDate, userAccountControl
Get-ADGroup 'Domain Admins' -Properties *
Get-ADComputer -Filter * | Select-Object Name, DNSHostName, DistinguishedName

# --- Énumérations filtrées (Enumerate / Pull / Release) ---
Get-ADUser -Filter *                                         # tous les utilisateurs
Get-ADUser -Filter "Enabled -eq 'True'"                      # filtre AND
Get-ADUser -LDAPFilter "(|(samAccountName=admin*)(!(userAccountControl:1.2.840.113556.1.4.803:=2)))"
Get-ADObject -Filter * -ResultPageSize 50                    # énumération paginée

# --- Groupes ---
Get-ADGroupMember 'Domain Admins'                            # membres directs
Get-ADGroupMember <groupe> -Recursive                        # membres imbriqués
Get-ADPrincipalGroupMembership <utilisateur>                 # appartenances inverses

# --- Comptes / schéma ---
Search-ADAccount -AccountDisabled                            # aussi -Inactive, -LockedOut
$schema = (Get-ADRootDSE).schemaNamingContext
Get-ADObject -SearchBase $schema -LDAPFilter '(objectClass=classSchema)' `
    -ResultPageSize 50 | Select-Object Name -First 10
```

### Console graphique : ADUC (`dsa.msc`) — pas ADAC

Pour piloter le domaine à la souris, utilise **ADUC** (Utilisateurs et ordinateurs AD) :

```powershell
dsa.msc
```

ADUC parle **LDAP** directement à Samba — il n'utilise pas ADWS et fonctionne sans réglage
(au besoin : clic droit sur la racine → *Changer de contrôleur de domaine* → ton DC).

> **⚠️ ADAC (`dsac.exe`) ne se connecte pas à un DC Samba.** Sa découverte automatique exige que
> le DC **annonce** le service ADWS via le flag Netlogon `DS_WS`, que **Samba n'émet pas** (limite
> côté Samba, pas de ce projet — il n'existe aucune option `smb.conf` pour l'activer). Diagnostic :
> `nltest /dsgetdc:samba.local /ws` renvoie `1355`. Détails :
> [06-depannage.md § 7](documentation/06-depannage.md#7-unable-to-find-a-default-server).
> → Pour le graphique, reste sur **ADUC** ; en ligne de commande, **PowerShell `-Server`**.

---

## Depuis Windows : administrer

Toutes ces opérations sont validées (batterie de parité 18/18). Droits **Domain Admins** requis.

```powershell
# --- Utilisateurs ---
New-ADUser -Name demo -SamAccountName demo -Enabled $true `
    -AccountPassword (ConvertTo-SecureString 'P@ssw0rd!Test123' -AsPlainText -Force)
Set-ADUser demo -Description 'créé via ADWS souverain' -Replace @{title='Engineer'}
Set-ADAccountPassword demo -Reset -NewPassword (ConvertTo-SecureString 'Reset@1234' -AsPlainText -Force)
Enable-ADAccount demo ; Disable-ADAccount demo
Rename-ADObject (Get-ADUser demo).DistinguishedName -NewName demo-renamed
Move-ADObject (Get-ADUser demo-renamed).DistinguishedName -TargetPath '<DN_OU_cible>'
Remove-ADUser demo-renamed -Confirm:$false

# --- Groupes ---
New-ADGroup -Name appgrp -GroupScope Global
Add-ADGroupMember appgrp -Members userA, userB
Remove-ADGroupMember appgrp -Members userA -Confirm:$false

# --- OU & suppression récursive ---
New-ADOrganizationalUnit -Name Test
Remove-ADObject '<DN_OU>' -Recursive -Confirm:$false        # honore le contrôle tree-delete

# --- Pipeline ---
Get-ADUser -Filter "Department -eq 'QA'" | Set-ADUser -Description 'updated'
```

---

## Limites connues

**Aucun de ces points n'est un bug du serveur :**

1. **ADAC (`dsac.exe`) ne se connecte pas** — ni les cmdlets lancées **sans `-Server`**. Leur
   découverte automatique exige que le DC **annonce** ADWS via le flag Netlogon `DS_WS`, que
   **Samba n'émet pas** (limite côté Samba, sans option `smb.conf` pour l'activer ; ce projet
   fournit le service ADWS mais pas l'annonce CLDAP de Samba). Diagnostic : `nltest /dsgetdc:samba.local /ws`
   → `1355`. **Contournement : ADUC (`dsa.msc`, LDAP) pour le graphique, PowerShell `-Server` en
   CLI.** Détails : [06-depannage § 7](documentation/06-depannage.md#7-unable-to-find-a-default-server).
2. **Changement de mot de passe self-service** (`Set-ADAccountPassword` ancien → nouveau) :
   échoue côté Samba (`Failed to obtain remote address for the LDAP client`). Le module
   `password_hash` de Samba exige une `remoteAddress` qu'une connexion `SamDB` locale ne peut
   pas fournir. **Le reset administrateur (`-Reset`) fonctionne.**
3. **Phase `Stress`** de la suite de test : `ForEach-Object -Parallel` n'existe pas en Windows
   PowerShell 5.1 (limite de l'harnais, pas du serveur). Lancer sous PowerShell 7+, ou
   `-SkipStress`.

Périmètre serveur : domaine local unique (pas de trusts inter-forêts) ; le SDDL/`nTSecurityDescriptor`
et les attributs binaires sont rendus en base64 brut. Détails : [01-architecture.md](documentation/01-architecture.md#limites-connues).

---

## Documentation

| Fichier | Sujet |
|---|---|
| [01-architecture.md](documentation/01-architecture.md) | Vue d'ensemble, couches protocolaires, organisation du code |
| [02-installation.md](documentation/02-installation.md) | Pré-requis, déploiement, Kerberos / SPN, désinstallation |
| [03-protocole.md](documentation/03-protocole.md) | Net.tcp, NMF, WCF binary XML, SOAP, IMDA vs WS-Transfer |
| [04-handlers.md](documentation/04-handlers.md) | Actions SOAP supportées et comment en ajouter |
| [05-tests.md](documentation/05-tests.md) | Suite de tests Windows + serveur, lecture des résultats |
| [06-depannage.md](documentation/06-depannage.md) | Erreurs fréquentes (Kerberos, base DN vide, faults SOAP) |

Pour valider de bout en bout : [`Test-ADWS-Sovereign.ps1`](tools/test/Test-ADWS-Sovereign.ps1) depuis le
poste Windows (voir [05-tests.md](documentation/05-tests.md)).

---

## Statut

Phases 1 → 4 (CRUD, recherche LDAP, opérations de groupe, compatibilité PowerShell/ADAC)
**complètes**. Phase 5 (perf + durcissement sécurité) **en cours**.

## Licence

synetis

# Installation et déploiement

## 1. Pré-requis

### Côté serveur (Linux)

- **Distribution** : Debian 11/12 ou Ubuntu 22.04+ recommandé.
- **Samba AD** : un domaine **opérationnel** avec niveau fonctionnel ≥ 2008R2. Vérifier :
  ```bash
  sudo samba-tool domain info $(hostname -d)
  ```
- **Python** : 3.9 ou plus récent.
- **Paquets système** (toutes les dépendances runtime ; **aucun `pip`**) :
  ```bash
  sudo apt-get update
  sudo apt-get install -y \
      python3 python3-samba python3-gssapi \
      samba-common-bin krb5-user
  ```
  `python3-samba` fournit `samba`+`ldb`, `python3-gssapi` fournit `gssapi`,
  `samba-common-bin` fournit `samba-tool`. Le serveur n'a **aucune** dépendance
  pip : ne jamais `pip install samba`/`ldb` ni utiliser `--break-system-packages`.
  Le script `install.sh` (§3) installe ces paquets pour toi.

### Côté client (Windows)

- Windows 10/11 ou Windows Server 2019/2022, joint au domaine (ou avec mappage DNS vers `samba.local`).
- Module PowerShell **ActiveDirectory** :
  ```powershell
  Add-WindowsCapability -Online -Name "Rsat.ActiveDirectory.DS-LDS.Tools~~~~0.0.1.0"
  ```

### Réseau

- Le client doit pouvoir résoudre **le FQDN du DC** (ex. `samba.local`) en IP — soit via DNS, soit via `C:\Windows\System32\drivers\etc\hosts`.
- Ports ouverts du client vers le DC :
  | Port | Service |
  |---|---|
  | 88/tcp+udp | Kerberos |
  | 389/tcp | LDAP |
  | 636/tcp | LDAPS (optionnel) |
  | **9389/tcp** | **ADWS (ce projet)** |

  ```bash
  sudo ufw allow 9389/tcp
  ```

## 2. Configuration Kerberos

ADWS exige une authentification GSSAPI/Kerberos — **pas** de mot de passe en clair.

### `/etc/krb5.conf` minimal sur le DC

```ini
[libdefaults]
    default_realm = SAMBA.LOCAL
    dns_lookup_realm = false
    dns_lookup_kdc = true

[realms]
    SAMBA.LOCAL = {
        kdc = samba.local
        admin_server = samba.local
    }

[domain_realm]
    .samba.local = SAMBA.LOCAL
    samba.local = SAMBA.LOCAL
```

### Vérifier que le DC est lui-même son KDC

```bash
kinit Administrator@SAMBA.LOCAL
klist
```

Si `klist` affiche un ticket pour `krbtgt/SAMBA.LOCAL@SAMBA.LOCAL`, Kerberos fonctionne.

### Enregistrer les SPN nécessaires

PowerShell demande un ticket pour `host/<fqdn-du-dc>`. **Si ce SPN n'est pas enregistré sur le compte machine du DC, l'auth échoue avec `STATUS_NO_SUCH_USER`** (voir [06-depannage.md](06-depannage.md)).

Lister les SPN actuels :
```bash
sudo samba-tool spn list "$(hostname -s)$"
```

Ajouter ceux qui manquent :
```bash
sudo samba-tool spn add "host/$(hostname -f)" "$(hostname -s)$"
sudo samba-tool spn add "HOST/$(hostname -f)" "$(hostname -s)$"
```

## 3. Installation en une commande (recommandé)

```bash
cd /root
git clone <url-du-depot> SambaAD_ADWS
cd SambaAD_ADWS
sudo tools/installation/install.sh
```

`install.sh` est **idempotent** et **souverain** (bash lisible, aucun binaire).
Il enchaîne tout ce que ce document décrivait à la main :

1. installe les paquets système (§1) — aucun `pip`, aucun virtualenv ;
2. enregistre le SPN `host/<fqdn>` sur le compte machine du DC s'il manque ;
3. écrit `/etc/default/samba-adws` (host/port/log, lus par le service) ;
4. rend et installe l'unit systemd depuis `tools/installation/samba-adws.service`
   (`WorkingDirectory` = le dossier cloné) ;
5. démarre le service (`enable --now`) et ouvre `9389/tcp` dans ufw.

Désinstaller : `sudo tools/installation/install.sh --uninstall` (garde les logs, laisse le SPN
dans AD). Reconfigurer : éditer `/etc/default/samba-adws` puis
`sudo systemctl restart samba-adws`.

## 4. Lancement manuel (debug) — sous le capot

`install.sh` n'est que l'automatisation de ceci ; pour lancer à la main en
développement :

```bash
sudo python3 -m src.main
```

Sortie attendue :
```
============================================================
SambaAD ADWS Server Starting
============================================================
NMF protocol registered
[LDB] Backend verification passed
ADWS server started on 0.0.0.0:9389
```

L'unit installée par `install.sh` est rendue depuis le template versionné
[`tools/installation/samba-adws.service`](../tools/installation/samba-adws.service)
(`EnvironmentFile=-/etc/default/samba-adws`, `Requires=samba-ad-dc.service`).
Statut / activation manuelle :
```bash
sudo systemctl daemon-reload
sudo systemctl enable --now samba-adws
sudo systemctl status samba-adws
```

Logs en temps réel :
```bash
sudo journalctl -u samba-adws -f
```

## 5. Vérification

### Depuis le DC

```bash
ss -tlnp | grep 9389
# Doit afficher : LISTEN ... 0.0.0.0:9389 ... python3
```

### Depuis Windows

```powershell
# Test TCP brut
Test-NetConnection samba.local -Port 9389

# Test ADWS fonctionnel
Get-ADRootDSE -Server samba.local
```

Si `Get-ADRootDSE` retourne un objet contenant `defaultNamingContext`, le déploiement est validé.

### Suite de tests complète

Voir [05-tests.md](05-tests.md) pour `Test-ADWS-Sovereign.ps1` qui couvre lecture, mutations, groupes, schéma et stress.

## Pas de virtualenv ni de pip

Le serveur n'a **aucune dépendance pip** : toutes les dépendances runtime sont des paquets
système (`python3-samba`, `python3-gssapi`) installés par `install.sh` (§3). Inutile de créer
un virtualenv ou d'utiliser `pip --break-system-packages`.

## Désinstallation

```bash
sudo tools/installation/install.sh --uninstall        # arrête + retire le service (garde les logs, laisse le SPN)
sudo rm -rf /root/SambaAD_ADWS        # supprime le code si souhaité
sudo ufw delete allow 9389/tcp        # si non déjà retiré par --uninstall
```

Aucune modification n'est faite au schéma AD ni aux objets Samba — désinstaller le serveur ADWS ne casse pas le domaine.

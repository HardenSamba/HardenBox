# HB-SambaAD

## Description

`HB-SambaAD` cree le premier controleur de domaine d'une nouvelle foret Samba Active Directory sur Debian 13. Le script `provision-samba-ad.sh` configure notamment :

- le nom d'hote et une adresse IPv4 statique ;
- le depot Samba de Tranquil IT et les composants AD DC ;
- le domaine Kerberos, le DNS Samba et SYSVOL ;
- Winbind, Chrony, nftables et les politiques de mot de passe ;
- l'audit, les sauvegardes en ligne et le durcissement SMB ;
- facultativement HIBP, OpenRSAT, PowerShell et un certificat LDAPS approuve.

Le paquet Debian installe le script dans `/usr/sbin/provision-samba-ad.sh`. Debconf collecte les parametres obligatoires pendant l'installation, puis un service systemd execute le provisionnement en tant que `root`. L'execution est differee jusqu'a la liberation des verrous APT et serialisee avec les autres deploiements HardenBox.

Le paquet depend de `hb-common`. Sur une nouvelle installation, le service Common doit terminer avec succes avant le demarrage du service Samba AD. Un controleur de domaine ne necessite pas de GUI : le mode Core est recommande. OpenRSAT n'est propose que lorsqu'une interface graphique est disponible.

> Ce paquet cree une nouvelle foret et modifie le nom d'hote, le reseau, le DNS, Kerberos, Samba et nftables. Il ne permet pas de joindre un domaine existant. Testez les parametres et conservez un acces console avant l'installation.

## Construire le paquet

Depuis le repertoire `Packages` :

```bash
./build-packages.sh
```

Pour choisir une autre version :

```bash
VERSION=1.3.0 ./build-packages.sh
```

Le paquet est genere dans `Packages/dist` sous le nom `HB-SambaAD_<version>_all.deb`.

## Installer le paquet

Depuis le repertoire `Packages/dist`, en tant que `root` :

```bash
cp HB-SambaAD_1.3.0_all.deb /tmp/
chmod 644 /tmp/HB-SambaAD_1.3.0_all.deb
apt install /tmp/HB-SambaAD_1.3.0_all.deb
rm -f /tmp/HB-SambaAD_1.3.0_all.deb
```

L'assistant demande le realm DNS, le nom NetBIOS, le nom du controleur, l'interface, l'adressage IPv4, le niveau fonctionnel, les options de durcissement et le mot de passe `Administrator`. Deux questions Oui/Non distinctes proposent OpenRSAT et PowerShell Core; elles sont activees par defaut.

Suivre le provisionnement :

```bash
sudo systemctl status hb-sambaad-bootstrap.service
sudo journalctl -fu hb-sambaad-bootstrap.service
```

Afficher le journal complet sans lignes tronquees :

```bash
journalctl -u hb-sambaad-bootstrap.service --no-pager -l
```

A la fin, le resultat est affiche sur les consoles locales et les terminaux SSH ouverts. Il reste aussi consultable avec :

```bash
cat /var/lib/hardenbox/hb-sambaad.result
```

Verifier directement les composants d'administration :

```bash
dpkg-query -W openrsat powershell
pwsh --noninteractive --nologo --command '$PSVersionTable.PSVersion.ToString()'
```

### Lancer OpenRSAT

Depuis GNOME, ouvrez la vue des applications et recherchez `OpenRSAT`. Le paquet installe une entree de menu avec son icone.

Depuis un terminal, utilisez l'une de ces commandes sans prefixe `./` :

```bash
openrsat
OpenRSAT
```

Le prefixe `./` recherche un fichier dans le repertoire courant. Le binaire reel est installe dans `/opt/openrsat/OpenRSAT` et les commandes ci-dessus sont creees dans `/usr/local/bin`.

Verifier l'installation et les lanceurs :

```bash
dpkg-query -W openrsat
command -v openrsat
ls -l /usr/local/bin/openrsat /usr/local/bin/OpenRSAT
```

Relancer un provisionnement qui a echoue :

```bash
sudo systemctl restart hb-sambaad-bootstrap.service
```

Le marqueur de reussite est `/var/lib/hardenbox/hb-sambaad.completed`. La configuration temporaire contenant le mot de passe est en mode `0600` et est supprimee apres une execution reussie.

### Reconfigurer le durcissement d'un domaine fonctionnel

Lorsque Samba AD est deja provisionne et que `samba-tool domain level show` reussit, relancez uniquement le questionnaire de durcissement :

```bash
dpkg-reconfigure hb-sambaad
journalctl -fu hb-sambaad-bootstrap.service
```

Le questionnaire affiche seulement les options de durcissement, OpenRSAT, PowerShell Core et le mot de passe `Administrator`. Le realm, le domaine, le nom du controleur et le reseau sont relus depuis Samba, debconf et la configuration active. Le runner ajoute automatiquement `--resume` et ne reprovisionne pas la foret.

### Reprendre apres un echec de validation Kerberos

La version `1.3.0` utilise explicitement un cache Kerberos de type `FILE:`, retire l'option `smbclient -N`, permet la reconfiguration du durcissement et ajoute les commandes `openrsat` et `OpenRSAT`. Si la foret a deja ete creee par une version precedente mais que le service a echoue pendant la validation, installez la nouvelle version :

```bash
cp HB-SambaAD_1.3.0_all.deb /tmp/
chmod 644 /tmp/HB-SambaAD_1.3.0_all.deb
apt install /tmp/HB-SambaAD_1.3.0_all.deb
rm -f /tmp/HB-SambaAD_1.3.0_all.deb
journalctl -fu hb-sambaad-bootstrap.service
```

Renseignez de nouveau les memes parametres et le mot de passe `Administrator`. Le runner detecte le domaine valide et ajoute automatiquement `--resume` sans reprovisionner la foret.

## Exemple d'execution manuelle

Afficher l'aide :

```bash
sudo ./provision-samba-ad.sh --help
```

Creer une foret de maniere non interactive sans inscrire le mot de passe dans la commande :

```bash
read -r -s -p "Mot de passe Administrator : " SAMBA_ADMIN_PASSWORD
printf '\n'
sudo env SAMBA_ADMIN_PASSWORD="$SAMBA_ADMIN_PASSWORD" \
    ./provision-samba-ad.sh \
    --non-interactive \
    --realm AD.EXEMPLE.FR \
    --domain EXEMPLE \
    --hostname dc1 \
    --interface ens18 \
    --ip-address 192.168.10.10/24 \
    --gateway 192.168.10.1 \
    --dns-forwarder 1.1.1.1 \
    --allowed-cidr 192.168.10.0/24 \
    --function-level 2016 \
    --hardening mdb,smb,winbind,audit,backup
unset SAMBA_ADMIN_PASSWORD
```

Le mot de passe doit contenir au moins 14 caracteres, avec une majuscule, une minuscule, un chiffre et un symbole.

Reprendre la post-configuration d'un domaine deja provisionne :

```bash
read -r -s -p "Mot de passe Administrator : " SAMBA_ADMIN_PASSWORD
printf '\n'
sudo env SAMBA_ADMIN_PASSWORD="$SAMBA_ADMIN_PASSWORD" \
    ./provision-samba-ad.sh \
    --non-interactive \
    --resume \
    --interface ens18 \
    --ip-address 192.168.10.10/24 \
    --gateway 192.168.10.1 \
    --dns-forwarder 1.1.1.1 \
    --allowed-cidr 192.168.10.0/24 \
    --function-level 2016 \
    --hardening mdb,smb,winbind,audit,backup
unset SAMBA_ADMIN_PASSWORD
```

Nettoyer un provisionnement incomplet apres sauvegarde demande une relance avec `--cleanup-partial` et tous les parametres de creation.

# HB-SambaAD

## Description

`HB-SambaAD` cree une nouvelle foret Samba Active Directory ou ajoute un controleur a un domaine existant sur Debian 13. Le script `provision-samba-ad.sh` configure notamment :

- le nom d'hote et une adresse IPv4 statique ;
- les composants Samba AD DC fournis par Debian ;
- le domaine Kerberos, le DNS Samba et SYSVOL ;
- Winbind, Chrony, nftables et les politiques de mot de passe ;
- l'audit, les sauvegardes en ligne et le durcissement SMB ;
- facultativement OpenRSAT, PowerShell et un certificat LDAPS approuve.

Le paquet Debian installe le script dans `/usr/sbin/provision-samba-ad.sh`. Il ne pose aucune question pendant l'installation. Sur le profil Core, connectez-vous d'abord en console et lancez `sudo hb-common-config` pour definir le nom d'hote, l'adresse statique, la passerelle, les DNS et SSH. Le service Web Samba AD devient ensuite accessible sur la nouvelle adresse. Sur le profil GUI, `SambaADPromo` se lance a la premiere ouverture de session GNOME.

Le paquet depend de `hb-common`. Sur une nouvelle installation, le service Common doit terminer avec succes avant le demarrage du service Samba AD. Un controleur de domaine ne necessite pas de GUI : le mode Core est recommande. OpenRSAT n'est propose que lorsqu'une interface graphique est disponible.

Le profil `sambaad-gui` installe `hb-sambaad-promo`. Il ne cree aucun domaine automatiquement et ajoute le raccourci bureau `SambaADPromo`, qui lance l'assistant Python de promotion avec les composants deja installes localement. La premiere page permet de creer une nouvelle foret ou d'ajouter un controleur de domaine a un domaine existant. Dans ce second mode, le bouton de test DNS valide la zone et les enregistrements SRV LDAP et Kerberos avant de poursuivre.

> La promotion modifie le nom d'hote, le reseau, le DNS, Kerberos, Samba et nftables. Testez les parametres et conservez un acces console avant l'installation.

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

L'installation du paquet ne demande aucune information. Sur Core, commencez depuis la console locale :

```bash
sudo hb-common-config
```

Une fois Common termine, le service Web Samba AD ecoute exclusivement sur `127.0.0.1:9090`. Depuis un poste d'administration, ouvrez un tunnel SSH vers la nouvelle adresse :

```bash
ssh -L 9090:127.0.0.1:9090 hardenbox@adresse-du-serveur
```

Dans cette session SSH, affichez le jeton :

```bash
sudo cat /var/lib/hardenbox/sambaad-web.token
```

Ouvrez ensuite `http://127.0.0.1:9090`, saisissez le jeton et renseignez le domaine, le reseau, le durcissement et le mot de passe `Administrator`.

Suivre le service Web et le provisionnement :

```bash
sudo systemctl status hb-sambaad-setup-web.service
sudo journalctl -fu hb-sambaad-setup-web.service
```

Afficher le journal complet sans lignes tronquees :

```bash
journalctl -u hb-sambaad-setup-web.service --no-pager -l
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

Relancer le service Web apres un echec :

```bash
sudo systemctl restart hb-sambaad-setup-web.service
```

Le marqueur de reussite est `/var/lib/hardenbox/hb-sambaad.completed`. Le mot de passe est transmis directement au provisionneur par l'entree standard et n'est jamais ecrit sur disque. Le jeton Web est supprime apres une execution reussie.

### Reconfigurer le durcissement d'un domaine fonctionnel

Lorsque Samba AD est deja provisionne et que `samba-tool domain level show` reussit, relancez le service Web pour modifier le durcissement :

```bash
sudo systemctl start hb-sambaad-setup-web.service
sudo cat /var/lib/hardenbox/sambaad-web.token
```

L'assistant detecte le domaine existant et propose soit la correction du durcissement et des GPO, soit l'extension seule du schema Exchange. Il ajoute automatiquement `--resume` pour la correction sans reprovisionner la foret.

### Schema Exchange 2019

Copiez le dossier `Setup/Data` du media Exchange 2019 sur le controleur, par exemple dans `/etc/Exchange/data`. Ce chemin doit contenir les fichiers `PostWindows2003_schema*.ldf`; les LDF Microsoft ne sont pas inclus dans HardenBox.

Lors d'une premiere promotion, activez **Etendre le schema apres la promotion** et indiquez ce chemin. Sur un domaine deja provisionne, relancez le GUI et choisissez **Etendre uniquement le schema Exchange** : cette operation ne rejoue ni le durcissement ni la correction GPO.

L'extension verifie le role Schema Master, sauvegarde l'annuaire dans `/var/backups` et controle la replication lorsqu'il existe plusieurs DC. Une extension de schema est irreversible hors restauration complete de l'annuaire.
Apres la correction du durcissement, `samba-dcgpofix` sauvegarde puis remet a l'etat Windows par defaut la Default Domain Policy et la Default Domain Controllers Policy. Ce traitement est identique avec SambaADPromo et avec le service Web Core.

Verifier les GPO sans les modifier :

```bash
sudo samba-dcgpofix --check
```

Les sauvegardes de correction sont conservees dans `/var/backups/samba-dcgpofix`.

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

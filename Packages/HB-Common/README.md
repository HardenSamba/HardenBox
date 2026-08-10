# HB-Common

## Description

`HB-Common` deploie et durcit une installation minimale de Debian 13. Le script principal `deploy-debian13.sh` permet notamment de :

- mettre le systeme a jour ;
- installer les outils de securite et d'administration ;
- configurer la locale, le fuseau horaire et le nom d'hote ;
- durcir SSH et configurer le pare-feu nftables ;
- activer les mises a jour automatiques, Auditd et Fail2ban ;
- installer facultativement un environnement GNOME minimal.

Le paquet Debian installe le script dans `/usr/sbin/deploy-debian13.sh`. Debconf demande le profil a installer, avec `gnome` comme choix par defaut, ainsi que les comptes administrateur et bureau. Un service systemd execute ensuite le script en tant que `root`. L'execution est differee jusqu'a la liberation des verrous APT et n'est pas repetee lorsque le profil choisi est deja configure.

> Ce script modifie la configuration reseau, SSH, nftables et plusieurs services systeme. Utilisez-le uniquement sur une machine Debian 13 sauvegardee et accessible par une console de secours.

## Construire le paquet

Depuis le repertoire `Packages` :

```bash
./build-packages.sh
```

Pour choisir une autre version :

```bash
VERSION=1.3.0 ./build-packages.sh
```

Le paquet est genere dans `Packages/dist` sous le nom `HB-Common_<version>_all.deb`.

## Installer le paquet

Depuis le repertoire `Packages/dist`, en tant que `root` :

```bash
cp HB-Common_1.3.0_all.deb /tmp/
chmod 644 /tmp/HB-Common_1.3.0_all.deb
apt install /tmp/HB-Common_1.3.0_all.deb
rm -f /tmp/HB-Common_1.3.0_all.deb
```

Le questionnaire demande le nom d'hote, le compte administrateur, le port SSH et le reseau autorise pour les deux profils. Si vous selectionnez `gnome`, il demande aussi le compte bureau, l'interface reseau, l'adresse statique CIDR, la passerelle et les serveurs DNS. Apres la fin du service, redemarrez la machine :

```bash
sudo systemctl status hb-common-bootstrap.service
journalctl -u hb-common-bootstrap.service --no-pager
systemctl get-default
systemctl is-enabled gdm3.service
sudo systemctl reboot
```

Le retour de la commande `apt install` ne signifie pas que le deploiement est termine : le service poursuit l'installation en arriere-plan. Attendez que son etat soit `inactive (dead)` avec `status=0/SUCCESS`. La cible par defaut doit ensuite etre `graphical.target` et `gdm3.service` doit etre active.

A la fin de l'execution, le service affiche sur les consoles locales et les terminaux SSH ouverts l'un des messages suivants :

```text
HB-Common: installation du profil gnome terminee avec succes. Vous devez redemarrer la machine: systemctl reboot
HB-Common: ECHEC de l'installation. Consultez: journalctl -u hb-common-bootstrap.service
```

Le dernier resultat reste consultable a tout moment :

```bash
cat /var/lib/hardenbox/hb-common.result
```

L'avertissement indiquant qu'un telechargement est effectue hors du bac a sable apparait lorsque l'utilisateur `_apt` ne peut pas traverser le repertoire contenant le fichier `.deb`. La copie temporaire ci-dessus l'evite sans modifier les permissions du dossier personnel.

Pour ajouter GNOME a une installation anterieure terminee avec le profil serveur :

```bash
cp HB-Common_1.3.0_all.deb /tmp/
chmod 644 /tmp/HB-Common_1.3.0_all.deb
apt install /tmp/HB-Common_1.3.0_all.deb
rm -f /tmp/HB-Common_1.3.0_all.deb
sudo journalctl -fu hb-common-bootstrap.service
```

Pour modifier ensuite le profil ou les comptes selectionnes, utilisez `sudo dpkg-reconfigure hb-common`.

Suivre le deploiement :

```bash
sudo systemctl status hb-common-bootstrap.service
sudo journalctl -fu hb-common-bootstrap.service
```

Relancer un deploiement qui a echoue :

```bash
sudo systemctl restart hb-common-bootstrap.service
```

Le marqueur `/var/lib/hardenbox/hb-common.completed` contient le dernier profil configure avec succes.

## Exemples d'execution manuelle

Afficher l'aide :

```bash
sudo ./deploy-debian13.sh --help
```

Deployer un serveur avec un nom d'hote et un port SSH personnalises :

```bash
sudo ./deploy-debian13.sh \
    --profile server \
    --hostname srv01.exemple.fr \
    --admin-user administrateur \
    --ssh-port 2222 \
    --ssh-allowed-cidr 192.168.10.0/24
```

Deployer un poste GNOME avec une adresse statique :

```bash
sudo ./deploy-debian13.sh \
    --profile gnome \
    --hostname poste01.exemple.fr \
    --admin-user administrateur \
    --desktop-user administrateur \
    --network-interface ens18 \
    --ip-address 192.168.10.20/24 \
    --gateway 192.168.10.1 \
    --dns 192.168.10.10,1.1.1.1
```

La configuration reseau statique exige simultanement l'interface, l'adresse CIDR, la passerelle et les serveurs DNS. Elle est reservee au profil `gnome`.

# Construction des paquets HardenBox

## Description

Le script `build-packages.sh` construit automatiquement un paquet Debian pour chaque sous-repertoire immediat de `Packages` contenant au moins un fichier `.sh`.

Avec la structure actuelle, il genere notamment :

- `HB-Common_<version>_all.deb` avec le nom Debian interne `hb-common` ;
- `HB-SambaAD_<version>_all.deb` avec le nom Debian interne `hb-sambaad` ;
- `secop-hardening_<version>_all.deb`, construit par le `build.sh` du module autonome `HB-DurcissementHarden` (voir plus bas).

Les archives sont placees par defaut dans `Packages/dist`.

## Prerequis

La construction necessite Debian ou un systeme disposant de `dpkg-deb` :

```bash
sudo apt update
sudo apt install dpkg-dev
```

Le script doit etre executable :

```bash
chmod +x build-packages.sh
```

## Construire tous les paquets

Depuis le repertoire `Packages` :

```bash
./build-packages.sh
```

Resultat attendu :

```text
dpkg-deb: building package 'hb-common' ...
dpkg-deb: building package 'hb-sambaad' ...
2 paquet(s) construit(s) dans .../Packages/dist
```

Lister les archives produites :

```bash
ls -lh dist/*.deb
```

## Variables de construction

Le comportement peut etre personnalise avec des variables d'environnement.

### Version

La version par defaut est definie dans `build-packages.sh`. Pour construire une autre version sans modifier le script :

```bash
VERSION=1.3.0 ./build-packages.sh
```

La version doit commencer par un chiffre et respecter le format accepte par Debian.

### Mainteneur

```bash
MAINTAINER="Equipe HardenBox <admin@example.org>" ./build-packages.sh
```

### Repertoire de sortie

```bash
OUTPUT_DIR=/tmp/hardenbox-packages ./build-packages.sh
```

Les variables peuvent etre combinees :

```bash
VERSION=1.3.0 \
MAINTAINER="Equipe HardenBox <admin@example.org>" \
OUTPUT_DIR=/tmp/hardenbox-packages \
./build-packages.sh
```

## Structure attendue d'un module

Exemple minimal :

```text
Packages/
|-- build-packages.sh
`-- HB-Exemple/
    `-- configure-exemple.sh
```

Le script `.sh` sera installe dans `/usr/sbin` avec le mode `0755`.

Un module peut fournir des fichiers Debian supplementaires :

```text
HB-Exemple/
|-- configure-exemple.sh
`-- debian/
    |-- config
    |-- postinst
    |-- runner
    `-- templates
```

Fichiers reconnus :

- `debian/config` : questionnaire debconf execute avant la configuration ;
- `debian/templates` : definitions des questions debconf ;
- `debian/postinst` : script execute par `dpkg` en tant que `root` ;
- `debian/runner` : traitement differe installe dans `/usr/lib/hardenbox`.

Lorsqu'un `runner` est present, le constructeur genere aussi une unite systemd :

```text
/usr/lib/systemd/system/<nom-du-paquet>-bootstrap.service
```

Le runner est installe ici :

```text
/usr/lib/hardenbox/<nom-du-paquet>-runner
```

## Module autonome avec build.sh

Un sous-repertoire contenant un fichier `build.sh` est un module autonome : il porte sa propre arborescence Debian et construit lui-meme son paquet. `build-packages.sh` ne le traite alors pas comme un module ordinaire (son `build.sh` n'est pas installe dans `/usr/sbin`) : il execute `build.sh`, puis copie dans le repertoire de sortie chaque `.deb` produit pendant cette execution.

C'est le cas de `HB-DurcissementHarden`, qui produit le paquet de durcissement ANSSI BP-028 `secop-hardening` :

```text
HB-DurcissementHarden/
|-- build.sh                     # construit secop-hardening_<version>_all.deb ici
|-- DurcissementLinux_HardenBox.docx
`-- secop-hardening/
    |-- DEBIAN/                  # control, conffiles, postinst, prerm, postrm
    |-- etc/secop/hardening.conf
    `-- usr/...                  # secop-harden, profils, service de premier demarrage
```

Le paquet peut etre construit seul, depuis n'importe quel repertoire courant :

```bash
./HB-DurcissementHarden/build.sh
ls HB-DurcissementHarden/secop-hardening_*_all.deb
```

Sa version est celle de `secop-hardening/DEBIAN/control`, independante de `VERSION`. Le `.deb` est ignore par Git.

Installe depuis l'ISO, le paquet n'applique pas le durcissement dans le chroot de l'installateur : `secop-hardening-firstboot.service` lance `secop-harden apply` une fois au premier demarrage. Installe sur un systeme demarre (`apt install ./secop-hardening_*.deb`), il l'applique immediatement.

## Nommage

Le nom du sous-repertoire est conserve dans le nom du fichier `.deb` :

```text
HB-Exemple_1.3.0_all.deb
```

Le nom Debian interne est converti en minuscules et les caracteres `_` deviennent `-` :

```text
hb-exemple
```

Les paquets generes utilisent l'architecture `all` et declarent au minimum les dependances `bash`, `systemd` et `util-linux`. La dependance `debconf` est ajoutee automatiquement lorsque `debian/templates` existe.

## Verifier un paquet

Afficher ses metadonnees :

```bash
dpkg-deb --info dist/HB-Common_1.3.0_all.deb
```

Afficher les fichiers embarques :

```bash
dpkg-deb --contents dist/HB-Common_1.3.0_all.deb
```

Verifier uniquement certains champs :

```bash
dpkg-deb --field dist/HB-Common_1.3.0_all.deb Package Version Depends
```

Verifier la syntaxe du constructeur :

```bash
bash -n build-packages.sh
```

## Installer un paquet local

Pour eviter l'avertissement `_apt` lie aux permissions d'un dossier personnel, copiez le paquet dans `/tmp` en tant que `root` :

```bash
cp dist/HB-Common_1.3.0_all.deb /tmp/
chmod 644 /tmp/HB-Common_1.3.0_all.deb
apt install /tmp/HB-Common_1.3.0_all.deb
rm -f /tmp/HB-Common_1.3.0_all.deb
```

Pour Samba AD :

```bash
cp dist/HB-SambaAD_1.3.0_all.deb /tmp/
chmod 644 /tmp/HB-SambaAD_1.3.0_all.deb
apt install /tmp/HB-SambaAD_1.3.0_all.deb
rm -f /tmp/HB-SambaAD_1.3.0_all.deb
```

Consultez ensuite les README propres a chaque module pour la configuration et le suivi des services.

## Nettoyer les archives

```bash
rm -f dist/*.deb
```

Une nouvelle execution de `build-packages.sh` recreera les paquets.

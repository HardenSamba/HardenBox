# HardenBox ISO avec simple-cdd

## Objectif

Ce repertoire construit une ISO d'installation HardenBox basee sur Debian 13.x avec `simple-cdd`.

La cible est la suite Debian `trixie`. Elle n'est pas verrouillee sur une version ponctuelle : chaque nouvelle construction utilise la version 13.x actuellement publiee sur les miroirs Debian, par exemple 13.6 puis 13.7.

L'ISO embarque les paquets construits depuis le repertoire `Packages` :

- `hb-common` ;
- `hb-common-config` ;
- `hb-sambaad` ;
- `hb-sambaad-promo`.

## Profils disponibles

L'installateur simple-cdd propose quatre profils :

- `common` : serveur Debian standard, puis installation de `hb-common-config`, qui affiche le questionnaire Common et configure `hb-common` ;
- `sambaad-core` : serveur sans GUI, puis `hb-common` et `hb-sambaad` ; ce profil est recommande pour un controleur de domaine ;
- `sambaad-gui` : GNOME, promotion Samba AD pendant l'installation, OpenRSAT et PowerShell Core ;
- `sambaad-gui-ready` : GNOME et tous les outils d'administration, sans promotion. Le raccourci bureau `SambaADPromo` permet de la lancer plus tard.

Selectionnez un seul profil HardenBox. Les profils Samba AD incluent deja Common et ne doivent pas etre combines avec le profil `common`.

Samba AD ne necessite pas d'interface graphique. Le profil Core est plus leger, reduit la surface d'attaque et convient a l'administration distante. Le profil GUI est utile uniquement lorsque vous souhaitez lancer OpenRSAT directement sur le controleur de domaine.

Tous les profils installent exclusivement depuis le DVD. Aucun miroir APT n'est configure sur le systeme cible. OpenRSAT, PowerShell Core, les personnalisations GNOME et le depot Flatpak de SysD Manager sont telecharges et verifies pendant la construction de l'ISO, puis embarques sur le media.

Les comptes, mots de passe, disques, parametres reseau et parametres Active Directory restent interactifs. Aucun secret n'est stocke dans l'ISO.

## Arborescence

```text
Distribution/
|-- build-iso.sh
|-- simple-cdd.conf
|-- profiles/
|   |-- default.preseed
|   |-- common.description
|   |-- common.packages
|   |-- common.preseed
|   |-- sambaad-core.description
|   |-- sambaad-core.packages
|   |-- sambaad-core.preseed
|   |-- sambaad-gui.description
|   |-- sambaad-gui.packages
|   |-- sambaad-gui.preseed
|   |-- sambaad-gui-ready.description
|   |-- sambaad-gui-ready.packages
|   `-- sambaad-gui-ready.preseed
|-- vendor-cache/         # ressources externes verifiees, generees
|-- local-packages/       # genere et ignore par Git
|-- images/              # travail simple-cdd, genere
`-- iso/                 # image finale et somme SHA256
```

## Prerequis

Construisez l'ISO sur Debian 13.x amd64 avec un acces Internet. Installez `simple-cdd` et ses dependances en tant qu'administrateur :

```bash
sudo apt update
sudo apt install simple-cdd flatpak
```

Le paquet Debian installe notamment `debian-cd`, `reprepro`, `rsync`, `wget`, `dctrl-tools` et les modules Python requis.

Prevoyez plusieurs gigaoctets d'espace libre pour le miroir partiel, les fichiers temporaires et l'ISO.

## Construire l'ISO

Depuis le repertoire `Distribution` :

```bash
./build-iso.sh
```

Le script effectue automatiquement les operations suivantes :

1. telecharge dans `vendor-cache` les ressources externes epinglees et verifie leurs sommes SHA256 ;
2. cree un depot Flatpak transportable contenant SysD Manager et son runtime ;
3. reconstruit les quatre paquets HardenBox avec `Packages/build-packages.sh` ;
4. copie les paquets HardenBox, OpenRSAT et PowerShell dans `Distribution/local-packages` ;
5. construit une ISO DVD amd64 avec `simple-cdd`, `--force-preseed` et Debian `trixie` ;
6. copie l'image finale et calcule sa somme SHA256.

Les paquets utilisent une valeur `SOURCE_DATE_EPOCH` stable issue du dernier commit. Une relance conserve ainsi les memes sommes pour une version inchangee.

`--force-preseed` est conserve pour la construction simple-cdd. Les questions debconf propres a HardenBox ne figurent pas dans les fichiers preseed : Debian Installer les lirait avant que les paquets locaux aient enregistre leurs modeles. Elles sont posees interactivement pendant la configuration de `hb-common` et `hb-sambaad`.

Le depot local produit par simple-cdd n'est pas signe. Le preseed global autorise donc `base-installer` a utiliser ce media pendant `debootstrap`; les sommes des paquets et de l'ISO restent verifiees lors de la construction.

Debian 13 ne publie plus d'installateur i386. La configuration utilise donc `DI_WWW_HOME` pour recuperer directement l'installateur amd64 et `DISABLE_UEFI_32=1` pour neutraliser l'ancien ajout UEFI 32 bits de simple-cdd 0.6.9.

Fichiers produits :

```text
Distribution/iso/HardenBox-13.x-amd64.iso
Distribution/iso/HardenBox-13.x-amd64.iso.sha256
```

La premiere construction telecharge les paquets Debian requis et peut etre longue. Les constructions suivantes reutilisent les donnees de travail de simple-cdd.

## Choisir une version des paquets HardenBox

Par defaut, `build-iso.sh` utilise la version definie dans `Packages/build-packages.sh`.

Pour imposer une autre version aux deux paquets embarques :

```bash
PACKAGE_VERSION=1.3.0 ./build-iso.sh
```

Cette variable change la version des paquets HardenBox, pas la version mineure de Debian 13.x.

## Verifier l'ISO

Verifier la somme SHA256 :

```bash
sha256sum --check iso/HardenBox-13.x-amd64.iso.sha256
```

Afficher le type de fichier :

```bash
file iso/HardenBox-13.x-amd64.iso
```

Lister les paquets HardenBox prepares pour simple-cdd :

```bash
for package in local-packages/*.deb; do
    dpkg-deb --field "$package" Package Version Architecture
done
```

## Tester dans une machine virtuelle

Creez une machine virtuelle amd64, attachez `iso/HardenBox-13.x-amd64.iso` au lecteur DVD virtuel et demarrez dessus.

Le menu de demarrage propose uniquement l'installation graphique. L'installateur utilise toujours la langue et la locale `fr_FR.UTF-8`, avec le clavier francais `fr(latin9)`.

Attention : le partitionnement est automatique. L'installateur efface entierement `/dev/sda` et cree une seule partition systeme avec la recette Debian `atomic`. N'attachez aucun disque contenant des donnees a conserver comme `/dev/sda`.

Pendant l'installation :

1. configurez les comptes Debian ;
2. choisissez un seul profil HardenBox ;
3. avec le profil `common`, repondez aux questions HB-Common et choisissez `server` ou `gnome` ;
4. avec `sambaad-core` ou `sambaad-gui`, repondez aux questions Samba ;
5. avec `sambaad-gui-ready`, repondez seulement aux questions Common. Aucune promotion n'est lancee ;
6. pour un profil promu, saisissez puis confirmez le mot de passe Administrator ;
7. terminez l'installation et redemarrez sur le disque.

L'analyse de supports d'installation CD ou DVD supplementaires est desactivee.

Le `late_command` execute les runners HardenBox dans `/target` avant le premier redemarrage avec `HARDENBOX_OFFLINE=1`. L'ecran de connexion n'apparait donc qu'apres la configuration Common et, pour les profils promus, la creation de la foret Samba AD. Les services bootstrap restent uniquement un mecanisme de reprise en cas d'installation manuelle du paquet.

Pour les profils Samba AD, le paquet `hb-sambaad` depend de `hb-common`. Le service Samba AD attend `hb-common-bootstrap.service` et verifie son marqueur de reussite avant de poursuivre.

Suivre la configuration Common :

```bash
journalctl -fu hb-common-bootstrap.service
```

Suivre la configuration Samba AD :

```bash
journalctl -fu hb-sambaad-bootstrap.service
```

## Configuration simple-cdd

Les points principaux de `simple-cdd.conf` sont :

```bash
CODENAME="trixie"
DI_CODENAME="trixie"
profiles="common sambaad-core sambaad-gui sambaad-gui-ready"
build_profiles="kernel-requirements"
local_packages="$PWD/local-packages"
export ARCH="amd64"
export DISKTYPE="DVD"
```

`trixie` correspond a toute la branche Debian 13.x. Ne remplacez pas cette valeur par `13.6` ou un autre numero ponctuel.

Le profil de construction `kernel-requirements` force la presence de `zstd` sur le DVD. Debian Installer en a besoin pour generer l'initramfs, mais `debian-cd` l'ajoute apres la resolution initiale du miroir partiel de simple-cdd.

## Nettoyage

Les repertoires suivants sont generes et ignores par Git :

```text
local-packages/
images/
iso/
tmp/
mirror/
```

Pour repartir d'une construction simple-cdd propre :

```bash
rm -rf local-packages images iso tmp mirror
```

Cette commande ne supprime ni les profils ni les sources dans `Packages`.

## Depannage

### simple-cdd absent

```text
ERREUR: simple-cdd est absent
```

Installez-le avec :

```bash
sudo apt update
sudo apt install simple-cdd
```

### Plusieurs versions d'un paquet

Si `Packages/dist` contient plusieurs versions du meme paquet, nettoyez les archives puis relancez :

```bash
rm -f ../Packages/dist/*.deb
./build-iso.sh
```

### Journal de construction

Pour lancer directement simple-cdd avec un journal detaille :

```bash
build-simple-cdd --conf simple-cdd.conf --dvd --force-preseed --verbose \
    --logfile "$PWD/simple-cdd-build.log"
```

### ISO non produite

Consultez le journal et verifiez l'acces aux miroirs :

```bash
wget --spider https://deb.debian.org/debian/
wget --spider https://security.debian.org/debian-security/
```

La machine de construction doit pouvoir telecharger les paquets de la suite `trixie`.

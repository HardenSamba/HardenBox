# HardenBox ISO avec simple-cdd

## Objectif

Ce repertoire construit une ISO d'installation HardenBox basee sur Debian 13.x avec `simple-cdd`.

La cible est la suite Debian `trixie`. Elle n'est pas verrouillee sur une version ponctuelle : chaque nouvelle construction utilise la version 13.x actuellement publiee sur les miroirs Debian, par exemple 13.6 puis 13.7.

L'ISO embarque les paquets construits depuis le repertoire `Packages` :

- `hb-common` ;
- `hb-common-config` ;
- `hb-sambaad` ;
- `hb-sambaad-promo` ;
- `hb-sambadws`, conserve sur le media puis installe uniquement apres une promotion Samba AD reussie.

## Profils disponibles

L'installateur simple-cdd propose quatre profils :

- `common` : poste GNOME avec `hb-common-config`, dont l'assistant applique la configuration Common a la premiere ouverture de session ;
- `sambaad-core` : serveur sans GUI, assistant Common en console puis assistant Web Samba AD accessible par tunnel SSH ;
- `sambaad-gui` : GNOME, OpenRSAT et assistant SambaADPromo a la premiere session ;
- `sambaad-gui-ready` : GNOME et tous les outils d'administration, sans promotion. Le raccourci bureau `SambaADPromo` permet de la lancer plus tard.

Selectionnez un seul profil HardenBox. Les profils Samba AD incluent deja Common et ne doivent pas etre combines avec le profil `common`.

Samba AD ne necessite pas d'interface graphique. Le profil Core est plus leger, reduit la surface d'attaque et convient a l'administration distante. Le profil GUI est utile uniquement lorsque vous souhaitez lancer OpenRSAT directement sur le controleur de domaine.

Tous les profils s'installent exclusivement depuis le DVD. A la fin de l'installation, les depots officiels Debian 13 (`trixie`, `trixie-updates` et `trixie-security`) sont configures dans `/etc/apt/sources.list.d/debian.sources`. OpenRSAT, PowerShell Core, les personnalisations GNOME et le depot Flatpak de SysD Manager sont telecharges et verifies pendant la construction de l'ISO, puis embarques sur le media.

Les comptes Debian sont preconfigures : le compte root utilise le mot de passe defini dans le preseed et le compte administrateur `hardenbox` est cree automatiquement. Les disques, parametres reseau et parametres Active Directory restent interactifs. Changez les mots de passe par defaut apres l'installation.

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
sudo apt install simple-cdd flatpak ostree
```

Le paquet Debian installe notamment `debian-cd`, `reprepro`, `rsync`, `wget`, `dctrl-tools` et les modules Python requis. Le script utilise aussi `curl` pour les ressources externes et `xorriso` pour personnaliser les menus de demarrage de l'image :

```bash
sudo apt install curl xorriso
```

Prevoyez plusieurs gigaoctets d'espace libre pour le miroir partiel, les fichiers temporaires et l'ISO.

## Construire l'ISO

Depuis le repertoire `Distribution` :

```bash
./build-iso.sh
```

Le script effectue automatiquement les operations suivantes :

1. telecharge dans `vendor-cache` les ressources externes epinglees et verifie leurs sommes SHA256 ;
2. cree un depot Flatpak local autonome contenant SysD Manager et ses runtimes, en excluant les extensions de langue partielles ;
3. reconstruit les quatre paquets HardenBox avec `Packages/build-packages.sh` ;
4. copie les paquets HardenBox, OpenRSAT et PowerShell dans `Distribution/local-packages` ;
5. retire du miroir de travail les anciennes occurrences de ces paquets locaux ;
6. construit une ISO DVD amd64 avec `simple-cdd`, `--force-preseed` et Debian `trixie` ;
7. copie l'image finale et calcule sa somme SHA256.

Les paquets utilisent une valeur `SOURCE_DATE_EPOCH` stable issue du dernier commit. Une relance conserve ainsi les memes sommes pour une version inchangee.

`--force-preseed` est conserve pour la construction simple-cdd. La configuration Common est reportee apres le redemarrage pour les profils qui embarquent `hb-common-config` : assistant graphique sous GNOME ou assistant console en Core. La configuration Samba AD n'est jamais effectuee dans l'installateur.

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

Pour imposer une autre version aux paquets HardenBox embarques :

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

1. les comptes Debian sont crees automatiquement, sans question ;
2. choisissez un seul profil HardenBox ;
3. avec le profil `common`, aucune question HB-Common n'est posee ; l'assistant se lance a la premiere session GNOME ;
4. avec `sambaad-core` ou `sambaad-gui`, aucune question Samba AD n'est posee pendant l'installation ;
5. avec `sambaad-gui-ready`, la configuration Common est egalement reportee a la premiere session. Aucune promotion n'est lancee ;
6. apres le redemarrage de `sambaad-core`, connectez-vous en console et lancez `sudo hb-common-config` ;
7. apres Common, utilisez SambaADPromo sous GNOME ou le service Web local en Core ;
8. terminez l'installation et redemarrez sur le disque.

L'analyse de supports d'installation CD ou DVD supplementaires est desactivee.

Lorsqu'il detecte `hb-common-config`, le `late_command` reporte le socle Common apres le premier redemarrage. Sous GNOME, l'assistant se lance automatiquement. Sur Samba AD Core, lancez-le depuis la console :

```bash
sudo hb-common-config
```

L'assistant configure le nom d'hote, l'adresse IPv4 statique, la passerelle, les DNS et SSH. La connexion peut etre coupee lorsque la nouvelle adresse est appliquee. Reconnectez-vous sur cette adresse avant de poursuivre.

Pour Samba AD Core, le service ecoute uniquement sur `127.0.0.1:9090`. Creez un tunnel avec `ssh -L 9090:127.0.0.1:9090 hardenbox@serveur`, lisez le jeton avec `sudo cat /var/lib/hardenbox/sambaad-web.token`, puis ouvrez `http://127.0.0.1:9090`.

Suivre la configuration Common :

```bash
journalctl -fu hb-common-bootstrap.service
```

Suivre la configuration Samba AD Core :

```bash
journalctl -fu hb-sambaad-setup-web.service
```

## Configuration simple-cdd

Les points principaux de `simple-cdd.conf` sont :

```bash
CODENAME="trixie"
DI_CODENAME="trixie"
profiles="common sambaad-core sambaad-gui sambaad-gui-ready"
build_profiles="kernel-requirements adws-payload"
local_packages="$PWD/local-packages"
export ARCH="amd64"
export DISKTYPE="DVD"
```

`trixie` correspond a toute la branche Debian 13.x. Ne remplacez pas cette valeur par `13.6` ou un autre numero ponctuel.

Le profil de construction `kernel-requirements` force la presence de `zstd` sur le DVD. Debian Installer en a besoin pour generer l'initramfs, mais `debian-cd` l'ajoute apres la resolution initiale du miroir partiel de simple-cdd. Le profil `adws-payload` ajoute au DVD les dependances necessaires au service AD Web Services embarque.

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

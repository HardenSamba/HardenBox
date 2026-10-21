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

L'installateur simple-cdd propose trois profils :

- `common` : poste GNOME avec `hb-common-config`, dont l'assistant applique la configuration Common a la premiere ouverture de session ;
- `sambaad-core` : serveur sans GUI, assistant Common en console puis assistant Web Samba AD accessible par tunnel SSH ;
- `sambaad-gui` : GNOME, OpenRSAT et assistant SambaADPromo a la premiere session.

Selectionnez un seul profil HardenBox. Les profils Samba AD incluent deja Common et ne doivent pas etre combines avec le profil `common`.

Samba AD ne necessite pas d'interface graphique. Le profil Core est plus leger, reduit la surface d'attaque et convient a l'administration distante. Le profil GUI est utile uniquement lorsque vous souhaitez lancer OpenRSAT directement sur le controleur de domaine.

Tous les profils s'installent exclusivement depuis le DVD. A la fin de l'installation, les depots officiels Debian 13 (`trixie`, `trixie-updates` et `trixie-security`) sont configures dans `/etc/apt/sources.list.d/debian.sources`. OpenRSAT, PowerShell Core, les personnalisations GNOME et le depot Flatpak de SysD Manager sont telecharges et verifies pendant la construction de l'ISO, puis embarques sur le media.

Les comptes Debian sont preconfigures : le compte root utilise le mot de passe defini dans le preseed et le compte administrateur `hardenbox` est cree automatiquement. Les disques, parametres reseau et parametres Active Directory restent interactifs. Changez les mots de passe par defaut apres l'installation.

## Arborescence

```text
Distribution/
|-- build-iso.sh
|-- check-updates.sh      # verifie les versions des ressources epinglees
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
|   `-- sambaad-gui.preseed
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

1. signale avec `check-updates.sh` les ressources externes epinglees dont une version plus recente est publiee, sans bloquer la construction ;
2. telecharge dans `vendor-cache` les ressources externes epinglees et verifie leurs sommes SHA256 ;
3. cree ou actualise un depot Flatpak local autonome contenant SysD Manager et ses runtimes, en excluant les extensions de langue partielles ;
4. reconstruit les cinq paquets HardenBox avec `Packages/build-packages.sh` ;
5. copie les paquets HardenBox, OpenRSAT et PowerShell dans `Distribution/local-packages` ;
6. retire du miroir de travail les anciennes occurrences de ces paquets locaux ;
7. met a jour le miroir Debian depuis `trixie`, `trixie-security` et `trixie-updates`, puis construit une ISO DVD amd64 avec `simple-cdd`, `--force-preseed` et Debian `trixie` ;
8. copie l'image finale et calcule sa somme SHA256.

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

## Mises a jour des paquets et ressources

Chaque source de logiciels embarquee sur l'ISO suit sa propre politique de mise a jour :

| Source | Mise a jour | Controle |
|---|---|---|
| Paquets Debian | automatique a chaque construction | `simple-cdd` interroge `trixie`, `trixie-security` et `trixie-updates` |
| OpenRSAT, PowerShell, theme Windows-10, icones We10X, Add to Desktop | manuelle, versions epinglees | `check-updates.sh` signale les nouvelles versions |
| SysD Manager et runtimes Flatpak | automatique tous les 30 jours | horodatage `vendor-cache/flatpak.timestamp` |

### Paquets Debian

Aucune action n'est necessaire : chaque construction met a jour le miroir de travail avec la derniere version 13.x publiee et les correctifs de securite.

### Ressources externes epinglees

Les ressources GitHub sont epinglees dans `build-iso.sh` par URL et somme SHA256. La construction est ainsi reproductible et refuse une archive modifiee en amont, mais elle ne choisit jamais une nouvelle version d'elle-meme.

Pour comparer les versions epinglees aux dernieres versions stables publiees :

```bash
./check-updates.sh
```

Exemple de resultat :

```text
Depot                                Epinglee       Disponible     Etat
B00merang-Project/Windows-10         3.2.1          3.2.1          a jour
PowerShell/PowerShell                v7.6.6         v7.6.6         a jour
Tommimon/add-to-desktop              v16            v17            MISE A JOUR DISPONIBLE
```

Le script lit directement les URL de `build-iso.sh` et ne modifie rien. Les preversions (`preview`, `rc`, `beta`...) sont ignorees. Une ressource epinglee sur un commit est comparee au dernier commit de la branche par defaut. Codes de sortie : `0` tout est a jour, `10` mise a jour disponible, `2` verification impossible.

L'API GitHub anonyme est limitee a 60 requetes par heure. En cas d'erreur repetee, fournissez un jeton :

```bash
GITHUB_TOKEN=ghp_... ./check-updates.sh
```

`build-iso.sh` lance cette verification au debut de chaque construction et affiche un avertissement si une mise a jour existe, sans s'arreter. Pour une construction hors ligne :

```bash
HARDENBOX_SKIP_UPDATE_CHECK=1 ./build-iso.sh
```

Pour appliquer une mise a jour :

1. telechargez la nouvelle version et calculez sa somme avec `sha256sum`, puis comparez-la a l'empreinte publiee sur la page de la release lorsqu'elle existe ;
2. remplacez l'URL, le nom du fichier et la somme dans l'appel `fetch_verified` de `build-iso.sh` ;
3. pour OpenRSAT, mettez aussi a jour le nom de fichier et la version `+hardenbox` du reconditionnement dans `build-iso.sh`, ainsi que `OPENRSAT_VERSION` et `OPENRSAT_SHA256` dans `Packages/HB-SambaAD/provision-samba-ad.sh` ;
4. pour les archives de themes et d'extensions, mettez aussi a jour leur nom (`install_bundled_archive`) et leur reference (`download_github_archive`) dans `Packages/HB-Common/deploy-debian13.sh` ;
5. relancez `./check-updates.sh`, puis reconstruisez l'ISO.

### Depot Flatpak hors ligne

SysD Manager et ses runtimes sont conserves dans `vendor-cache/flatpak`. `build-iso.sh` reconstruit ce depot depuis Flathub lorsqu'il est absent, lorsque sa date de construction est inconnue ou lorsqu'il a plus de 30 jours. La date de la derniere actualisation est enregistree dans `vendor-cache/flatpak.timestamp`.

Forcer l'actualisation, par exemple apres un correctif de securite du runtime GNOME :

```bash
REFRESH_FLATPAK=1 ./build-iso.sh
```

Changer le delai d'actualisation automatique, en jours :

```bash
FLATPAK_MAX_AGE_DAYS=7 ./build-iso.sh
```

Si Flathub est injoignable lors d'une actualisation automatique, le depot existant est conserve avec un avertissement. Avec `REFRESH_FLATPAK=1` ou sans depot existant, la construction s'arrete.

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
5. apres le redemarrage de `sambaad-core`, connectez-vous en console et lancez `sudo hb-common-config` ;
6. apres Common, utilisez SambaADPromo sous GNOME ou le service Web local en Core ;
7. terminez l'installation et redemarrez sur le disque.

L'analyse de supports d'installation CD ou DVD supplementaires est desactivee.

Lorsqu'il detecte `hb-common-config`, le `late_command` reporte le socle Common apres le premier redemarrage. Sous GNOME, l'assistant se lance automatiquement. Sur Samba AD Core, lancez-le depuis la console :

```bash
sudo hb-common-config
```

L'assistant configure le nom d'hote, l'adresse IPv4 statique, la passerelle, les DNS et SSH. La connexion peut etre coupee lorsque la nouvelle adresse est appliquee. Reconnectez-vous sur cette adresse avant de poursuivre.

Pour Samba AD Core, le service ecoute uniquement sur `127.0.0.1:9090`. Creez un tunnel avec `ssh -L 9090:127.0.0.1:9090 hardenbox@serveur`, lisez le jeton avec `sudo cat /var/lib/hardenbox/sambaad-web.token`, puis ouvrez `http://127.0.0.1:9090`.

Les GUI Samba AD proposent l'extension facultative du schema Exchange 2019 pendant la premiere promotion ou, lors d'un second lancement, comme unique operation. Il suffit de cocher l'option : les LDF sont lus dans `/usr/share/hardenbox/exchange-data`, rempli a partir de `Packages/HB-SambaAD/exchange-data` (copie du dossier `Setup/Data` du media Exchange) lors de la construction de l'ISO.

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
profiles="common sambaad-core sambaad-gui"
build_profiles="kernel-requirements adws-payload"
local_packages="$PWD/local-packages"
export ARCH="amd64"
export DISKTYPE="DVD"
```

`trixie` correspond a toute la branche Debian 13.x. Ne remplacez pas cette valeur par `13.6` ou un autre numero ponctuel.

Le profil de construction `kernel-requirements` force la presence de `zstd` sur le DVD. Debian Installer en a besoin pour generer l'initramfs, mais `debian-cd` l'ajoute apres la resolution initiale du miroir partiel de simple-cdd. Le profil `adws-payload` ajoute au DVD les dependances necessaires au service AD Web Services embarque.

## Construction automatique (GitHub Actions)

Le workflow `.github/workflows/build-iso.yml` construit l'ISO sur GitHub et la publie en pre-release.

### Declenchement

- **Manuel** : onglet *Actions* > *Build ISO* > *Run workflow*. Le champ facultatif `package_version` impose la version des paquets HardenBox. La pre-release est nommee `iso-AAAAMMJJ-HHMM-<commit>`.
- **Etiquette** : pousser une etiquette `v*` construit l'ISO pour cette version. `v1.3.41` produit la pre-release `v1.3.41` et des paquets HardenBox en version `1.3.41`.

```bash
git tag v1.3.41
git push origin v1.3.41
```

### Deroulement

1. libere de l'espace sur le runner : la construction occupe environ 10 Go ;
2. recupere tout l'historique Git, utilise pour dater les paquets de facon reproductible ;
3. restaure `Distribution/vendor-cache` depuis le cache GitHub. La cle change chaque mois et depend de `build-iso.sh` ;
4. construit l'ISO dans un conteneur `debian:trixie` privilegie, avec `simple-cdd`, `xorriso`, `curl`, `flatpak`, `ostree`, `git` et `python3`. Le mode privilegie est requis par Flatpak, qui utilise `bubblewrap` pour installer SysD Manager et ses runtimes ;
5. recalcule la somme SHA256 avec un nom de fichier relatif ;
6. publie l'ISO et sa somme comme artefact du workflow, conserve 14 jours ;
7. liste les commits, avec leur message complet, depuis la derniere execution reussie du workflow. Lors de la premiere execution, ou si cet historique a ete reecrit, ce sont les 20 derniers commits. La liste apparait dans le resume de l'execution et dans les notes de la pre-release ;
8. cree la pre-release GitHub avec l'ISO, sa somme, le commit source et cette liste de modifications.

Le jeton du workflow est transmis a `check-updates.sh`, ce qui evite la limite de l'API GitHub anonyme. Une release GitHub refuse les fichiers de plus de 2 Gio : si l'ISO depasse cette taille, seule sa somme est attachee et l'ISO reste disponible dans les artefacts.

### Prerequis du depot

- le workflow demande `contents: write` pour creer les releases. Si l'organisation limite les permissions des workflows, autorisez l'ecriture dans *Settings* > *Actions* > *General* > *Workflow permissions* ;
- la premiere execution, ou la premiere de chaque mois, telecharge le depot Flatpak complet depuis Flathub et dure plus longtemps.

## Nettoyage

Les repertoires suivants sont generes et ignores par Git :

```text
local-packages/
images/
iso/
tmp/
mirror/
vendor-cache/
```

`vendor-cache` conserve les ressources externes verifiees et le depot Flatpak. Le supprimer force leur telechargement complet a la prochaine construction.

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

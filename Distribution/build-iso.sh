#!/usr/bin/env bash

set -Eeuo pipefail
IFS=$'\n\t'

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPOSITORY_DIR=$(cd -- "$SCRIPT_DIR/.." && pwd)
PACKAGES_DIR="$REPOSITORY_DIR/Packages"
LOCAL_PACKAGES_DIR="$SCRIPT_DIR/local-packages"
ISO_OUTPUT_DIR="$SCRIPT_DIR/iso"
ISO_OUTPUT="$ISO_OUTPUT_DIR/HardenBox-13.x-amd64.iso"
BUILD_TMP_DIR=${HARDENBOX_BUILD_TMPDIR:-"$SCRIPT_DIR/tmp"}
install -d -m 0755 "$BUILD_TMP_DIR"
PACKAGE_BUILD_DIR=$(mktemp -d "$BUILD_TMP_DIR/hardenbox-build.XXXXXX")
trap 'rm -rf -- "$PACKAGE_BUILD_DIR"' EXIT
BIOS_MENU="$SCRIPT_DIR/boot/isolinux-menu.cfg"
UEFI_MENU="$SCRIPT_DIR/boot/grub.cfg"
VENDOR_CACHE_DIR="$SCRIPT_DIR/vendor-cache"
COMMON_ASSETS_DIR="$PACKAGE_BUILD_DIR/common-assets"
PERSONALIZATION_DIR="$SCRIPT_DIR/personnalisation"
PLYMOUTH_ARCHIVE="$PERSONALIZATION_DIR/HARDENBOX-Debian13-Plymouth.tar.gz"

fetch_verified() {
    local url=$1 destination=$2 expected_sha256=$3
    if [[ ! -f $destination ]] || \
        ! printf '%s  %s\n' "$expected_sha256" "$destination" | sha256sum --check --status; then
        rm -f -- "$destination"
        curl --fail --location --silent --show-error "$url" --output "$destination"
    fi
    printf '%s  %s\n' "$expected_sha256" "$destination" | sha256sum --check --status || {
        printf 'ERREUR: empreinte invalide pour %s\n' "$destination" >&2
        exit 1
    }
}

command -v build-simple-cdd >/dev/null 2>&1 || {
    printf 'ERREUR: simple-cdd est absent. Installez les prerequis avec:\n' >&2
    printf '  sudo apt update && sudo apt install simple-cdd\n' >&2
    exit 1
}

command -v dpkg-deb >/dev/null 2>&1 || {
    printf 'ERREUR: dpkg-deb est requis.\n' >&2
    exit 1
}

command -v reprepro >/dev/null 2>&1 || {
    printf 'ERREUR: reprepro est requis pour actualiser les paquets locaux.\n' >&2
    exit 1
}

command -v xorriso >/dev/null 2>&1 || {
    printf 'ERREUR: xorriso est requis pour personnaliser le menu de demarrage.\n' >&2
    exit 1
}

command -v curl >/dev/null 2>&1 || {
    printf 'ERREUR: curl est requis pour preparer les ressources hors ligne.\n' >&2
    exit 1
}

command -v flatpak >/dev/null 2>&1 || {
    printf 'ERREUR: flatpak est requis pour embarquer SysD Manager hors ligne.\n' >&2
    printf 'Installez-le sur la machine de build: sudo apt install flatpak\n' >&2
    exit 1
}

command -v ostree >/dev/null 2>&1 || {
    printf 'ERREUR: ostree est requis pour finaliser le depot Flatpak hors ligne.\n' >&2
    printf 'Installez-le sur la machine de build: sudo apt install ostree\n' >&2
    exit 1
}

# Signale les ressources epinglees plus anciennes que leur derniere version
# publiee, sans bloquer la construction (HARDENBOX_SKIP_UPDATE_CHECK=1 l'evite).
if [[ ${HARDENBOX_SKIP_UPDATE_CHECK:-0} != 1 ]]; then
    printf 'Verification des mises a jour des ressources epinglees...\n'
    update_status=0
    "$SCRIPT_DIR/check-updates.sh" || update_status=$?
    case $update_status in
        0) ;;
        10) printf 'AVERTISSEMENT: des ressources epinglees ont une version plus recente.\n' >&2 ;;
        *) printf 'AVERTISSEMENT: verification des mises a jour impossible.\n' >&2 ;;
    esac
fi

install -d -m 0755 "$VENDOR_CACHE_DIR" "$COMMON_ASSETS_DIR"
POWERSHELL_DEB="$VENDOR_CACHE_DIR/powershell_7.6.7-1.deb_amd64.deb"
fetch_verified \
    https://github.com/tranquilit/OpenRSAT/releases/download/v0.4.519/OpenRSAT-0.4.519-amd64.deb \
    "$VENDOR_CACHE_DIR/OpenRSAT-0.4.519-amd64.deb" \
    9fdaf16329a38936e9ae5d1fac5a4e793f554ed781c5528de74ccbf541bfac6c
fetch_verified \
    https://github.com/PowerShell/PowerShell/releases/download/v7.6.7/powershell_7.6.7-1.deb_amd64.deb \
    "$POWERSHELL_DEB" \
    868c575f0822b2deed054c1c3c464a9d4b841f67e153369147fb6ace3545c8b2
fetch_verified \
    https://github.com/B00merang-Project/Windows-10/archive/3.2.1.tar.gz \
    "$VENDOR_CACHE_DIR/windows-10-3.2.1.tar.gz" \
    e6d441104931410bc52516810e8d9b1e39dc5f9c4b57cbc2d5d57ae25aa84152
fetch_verified \
    https://github.com/yeyushengfan258/We10X-icon-theme/archive/0f52ff2.tar.gz \
    "$VENDOR_CACHE_DIR/we10x-0f52ff2.tar.gz" \
    77a103035d9628423d3ca080e09eb73b262bdc1fe51bfe34ae8b86990109f91c
fetch_verified \
    https://github.com/Tommimon/add-to-desktop/archive/v17.tar.gz \
    "$VENDOR_CACHE_DIR/add-to-desktop-v17.tar.gz" \
    22bc2c69411a3aa8ab8057a2a7475e166cc3b6e1446f58fd907ae8147fb64363

install -m 0644 "$VENDOR_CACHE_DIR"/*.tar.gz "$COMMON_ASSETS_DIR/"

install -d -m 0755 "$COMMON_ASSETS_DIR/wallpapers" \
    "$COMMON_ASSETS_DIR/plymouth"
for wallpaper_size in 3840x2160 1920x1080 1280x720; do
    wallpaper="$PERSONALIZATION_DIR/HARDENBOX-Debian13-Wallpaper-$wallpaper_size.png"
    [[ -r $wallpaper ]] || {
        printf 'ERREUR: wallpaper HardenBox absent: %s\n' "$wallpaper" >&2
        exit 1
    }
    install -m 0644 "$wallpaper" "$COMMON_ASSETS_DIR/wallpapers/"
done
[[ -r $PLYMOUTH_ARCHIVE ]] || {
    printf 'ERREUR: archive Plymouth absente: %s\n' "$PLYMOUTH_ARCHIVE" >&2
    exit 1
}
plymouth_extract_dir="$PACKAGE_BUILD_DIR/plymouth-source"
install -d -m 0755 "$plymouth_extract_dir"
tar -xzf "$PLYMOUTH_ARCHIVE" -C "$plymouth_extract_dir" \
    ./usr/share/plymouth/themes/hardenbox
install -m 0644 "$PERSONALIZATION_DIR/hardenbox.script" \
    "$plymouth_extract_dir/usr/share/plymouth/themes/hardenbox/hardenbox.script"
cp -a -- "$plymouth_extract_dir/usr/share/plymouth/themes/hardenbox" \
    "$COMMON_ASSETS_DIR/plymouth/"

# Le depot Flatpak hors ligne (SysD Manager et ses runtimes) est reconstruit
# lorsqu'il manque, lorsqu'il a plus de FLATPAK_MAX_AGE_DAYS jours ou avec
# REFRESH_FLATPAK=1.
FLATPAK_APP=io.github.plrigaux.sysd-manager
FLATPAK_MAX_AGE_DAYS=${FLATPAK_MAX_AGE_DAYS:-30}
flatpak_cache=$VENDOR_CACHE_DIR/flatpak
flatpak_stamp=$VENDOR_CACHE_DIR/flatpak.timestamp
[[ $FLATPAK_MAX_AGE_DAYS =~ ^[0-9]+$ ]] || {
    printf 'ERREUR: FLATPAK_MAX_AGE_DAYS doit etre un nombre de jours.\n' >&2
    exit 1
}

refresh_flatpak_cache() {
    local staging="$VENDOR_CACHE_DIR/flatpak.new"
    flatpak remote-add --user --if-not-exists flathub \
        https://dl.flathub.org/repo/flathub.flatpakrepo &&
    flatpak remote-modify --user --collection-id=org.flathub.Stable flathub &&
    flatpak install --user --noninteractive -y flathub "$FLATPAK_APP" &&
    flatpak update --user --noninteractive -y "$FLATPAK_APP" &&
    rm -rf -- "$staging" &&
    install -d -m 0755 "$staging" &&
    flatpak create-usb "$staging" "$FLATPAK_APP" &&
    rm -rf -- "$flatpak_cache" &&
    mv -- "$staging" "$flatpak_cache" &&
    date '+%Y-%m-%d %H:%M:%S' >"$flatpak_stamp"
}

flatpak_refresh_reason=""
if [[ ! -d $flatpak_cache/.ostree/repo ]]; then
    flatpak_refresh_reason="depot absent"
elif [[ ${REFRESH_FLATPAK:-0} == 1 ]]; then
    flatpak_refresh_reason="REFRESH_FLATPAK=1"
elif [[ ! -f $flatpak_stamp ]]; then
    flatpak_refresh_reason="date de construction inconnue"
elif [[ -n $(find "$flatpak_stamp" -mtime +"$FLATPAK_MAX_AGE_DAYS" -print) ]]; then
    flatpak_refresh_reason="plus de $FLATPAK_MAX_AGE_DAYS jours"
fi
if [[ -n $flatpak_refresh_reason ]]; then
    printf 'Actualisation du depot Flatpak hors ligne (%s)...\n' "$flatpak_refresh_reason"
    if ! refresh_flatpak_cache; then
        rm -rf -- "$VENDOR_CACHE_DIR/flatpak.new"
        # Un depot existant reste utilisable si l'actualisation n'etait pas exigee.
        if [[ -d $flatpak_cache/.ostree/repo && ${REFRESH_FLATPAK:-0} != 1 ]]; then
            printf 'AVERTISSEMENT: actualisation Flatpak impossible, depot existant conserve.\n' >&2
        else
            printf 'ERREUR: impossible de preparer le depot Flatpak hors ligne.\n' >&2
            exit 1
        fi
    fi
else
    printf 'Depot Flatpak hors ligne a jour (construit le %s).\n' "$(<"$flatpak_stamp")"
fi

flatpak_repo=$VENDOR_CACHE_DIR/flatpak/.ostree/repo
flatpak_mirror_refs=$flatpak_repo/refs/mirrors/org.flathub.Stable
[[ -d $flatpak_mirror_refs ]] || {
    printf 'ERREUR: refs Flatpak de la collection Flathub absentes.\n' >&2
    exit 1
}
while IFS= read -r ref_file; do
    ref=${ref_file#"$flatpak_mirror_refs/"}
    checksum=$(<"$ref_file")
    if [[ -f $flatpak_repo/state/$checksum.commitpartial ]]; then
        ostree --repo="$flatpak_repo" refs --delete "$ref"
        printf 'Ref Flatpak partielle exclue du depot autonome: %s\n' "$ref"
        continue
    fi
    ostree --repo="$flatpak_repo" refs --force --create="$ref" "$checksum"
done < <(find "$flatpak_mirror_refs" -type f | sort)
flatpak build-update-repo --title='HardenBox Offline Flatpak' \
    --no-update-appstream "$flatpak_repo"

cp -a -- "$VENDOR_CACHE_DIR/flatpak" "$COMMON_ASSETS_DIR/flatpak"

# Modules PowerShell embarques dans hb-sambaad, comme Install-Module -AllowPrerelease :
# derniere version de la PowerShell Gallery, preversions comprises, sauf HGPM_VERSION.
POWERSHELL_MODULES_DIR="$PACKAGE_BUILD_DIR/powershell-modules"
printf 'Preparation du module PowerShell HGPM...\n'
python3 -I "$SCRIPT_DIR/fetch-psgallery-module.py" HGPM --version "${HGPM_VERSION:-}" \
    --cache-dir "$VENDOR_CACHE_DIR/psgallery" --output-dir "$POWERSHELL_MODULES_DIR"

# La fenetre Avalonia de HGPM telecharge sinon son runtime depuis nuget.org au premier
# lancement. Get-AvaloniaBinaries.ps1 l'installe dans Avalonia/bin/linux-x64, ou HGPM le
# cherche en premier; il verifie chaque paquet avec le SHA-512 epingle par le module.
# Le PowerShell deja en cache est simplement extrait, sans installation.
hgpm_module=$(find "$POWERSHELL_MODULES_DIR/HGPM" -mindepth 1 -maxdepth 1 -type d -print -quit)
avalonia_packages="$hgpm_module/Resources/AvaloniaPackages.json"
[[ -r $avalonia_packages ]] || {
    printf 'ERREUR: liste des paquets Avalonia absente du module HGPM.\n' >&2
    exit 1
}
avalonia_cache="$VENDOR_CACHE_DIR/avalonia/$(sha256sum "$avalonia_packages" | cut -c1-16)"
if [[ ! -r $avalonia_cache/linux-x64/Avalonia.dll ]]; then
    printf 'Telechargement du runtime Avalonia linux-x64 de HGPM...\n'
    pwsh_root="$PACKAGE_BUILD_DIR/pwsh-root"
    install -d -m 0755 "$pwsh_root" "$PACKAGE_BUILD_DIR/pwsh-home"
    dpkg-deb -x "$POWERSHELL_DEB" "$pwsh_root"
    rm -rf -- "$avalonia_cache"
    HOME="$PACKAGE_BUILD_DIR/pwsh-home" POWERSHELL_TELEMETRY_OPTOUT=1 \
        "$pwsh_root/opt/microsoft/powershell/7/pwsh" -NoLogo -NoProfile -NonInteractive \
        -File "$hgpm_module/Avalonia/Get-AvaloniaBinaries.ps1" -Rid linux-x64 -Destination "$avalonia_cache"
fi
[[ -r $avalonia_cache/linux-x64/Avalonia.dll ]] || {
    printf 'ERREUR: runtime Avalonia linux-x64 incomplet: %s\n' "$avalonia_cache" >&2
    exit 1
}
install -d -m 0755 "$hgpm_module/Avalonia/bin"
cp -a -- "$avalonia_cache/linux-x64" "$hgpm_module/Avalonia/bin/linux-x64"

printf 'Construction des paquets HardenBox...\n'
if [[ -n ${PACKAGE_VERSION:-} ]]; then
    HARDENBOX_COMMON_ASSETS_DIR=$COMMON_ASSETS_DIR \
        HARDENBOX_POWERSHELL_MODULES_DIR=$POWERSHELL_MODULES_DIR \
        VERSION=$PACKAGE_VERSION OUTPUT_DIR=$PACKAGE_BUILD_DIR "$PACKAGES_DIR/build-packages.sh"
else
    HARDENBOX_COMMON_ASSETS_DIR=$COMMON_ASSETS_DIR \
        HARDENBOX_POWERSHELL_MODULES_DIR=$POWERSHELL_MODULES_DIR \
        OUTPUT_DIR=$PACKAGE_BUILD_DIR "$PACKAGES_DIR/build-packages.sh"
fi

rm -rf -- "$LOCAL_PACKAGES_DIR"
install -d -m 0755 "$LOCAL_PACKAGES_DIR" "$ISO_OUTPUT_DIR"

for package_name in hb-common hb-common-config hb-sambaad hb-sambaad-promo hb-sambadws; do
    package_path=""
    while IFS= read -r candidate; do
        [[ $(dpkg-deb --field "$candidate" Package) == "$package_name" ]] || continue
        if [[ -n $package_path ]]; then
            printf 'ERREUR: plusieurs paquets %s ont ete produits.\n' "$package_name" >&2
            exit 1
        fi
        package_path=$candidate
    done < <(find "$PACKAGE_BUILD_DIR" -maxdepth 1 -type f -name '*.deb' -print | sort)
    [[ -n $package_path ]] || {
        printf 'ERREUR: paquet %s absent de la construction courante.\n' "$package_name" >&2
        exit 1
    }
    install -m 0644 "$package_path" "$LOCAL_PACKAGES_DIR/${package_path##*/}"
done

openrsat_root="$PACKAGE_BUILD_DIR/openrsat-root"
dpkg-deb --raw-extract "$VENDOR_CACHE_DIR/OpenRSAT-0.4.519-amd64.deb" "$openrsat_root"
sed -i -E 's/^Package:[[:space:]]*.*/Package: openrsat/' "$openrsat_root/DEBIAN/control"
sed -i -E 's/^Version:[[:space:]]*.*/Version: 0.4.519+hardenbox1/' \
    "$openrsat_root/DEBIAN/control"
find "$openrsat_root" -print0 | xargs -0 touch --no-dereference --date='@0'
SOURCE_DATE_EPOCH=0 dpkg-deb --build --root-owner-group "$openrsat_root" \
    "$LOCAL_PACKAGES_DIR/openrsat_0.4.519+hardenbox1_amd64.deb" >/dev/null

for vendor_package in "$POWERSHELL_DEB"; do
    vendor_name=$(dpkg-deb --field "$vendor_package" Package)
    vendor_version=$(dpkg-deb --field "$vendor_package" Version)
    vendor_architecture=$(dpkg-deb --field "$vendor_package" Architecture)
    install -m 0644 "$vendor_package" \
        "$LOCAL_PACKAGES_DIR/${vendor_name}_${vendor_version}_${vendor_architecture}.deb"
done

mirror_dir="$BUILD_TMP_DIR/mirror"
if [[ -d $mirror_dir/db && -f $mirror_dir/conf/distributions ]]; then
    printf 'Actualisation des paquets locaux dans le miroir simple-cdd...\n'
    for local_package in "$LOCAL_PACKAGES_DIR"/*.deb; do
        package_name=$(dpkg-deb --field "$local_package" Package)
        if reprepro -b "$mirror_dir" list trixie "$package_name" | grep -q .; then
            reprepro -b "$mirror_dir" remove trixie "$package_name" >/dev/null
        fi
    done
fi

printf 'Construction de l ISO HardenBox Debian 13.x (trixie)...\n'
cd "$SCRIPT_DIR"
build-simple-cdd --conf "$SCRIPT_DIR/simple-cdd.conf" --dvd --force-preseed

built_iso=$(find "$SCRIPT_DIR/images" -type f -name '*.iso' -printf '%T@ %p\n' 2>/dev/null \
    | sort -nr | head -n 1 | cut -d' ' -f2-)
[[ -n $built_iso && -f $built_iso ]] || {
    printf 'ERREUR: aucune image ISO produite dans Distribution/images.\n' >&2
    exit 1
}

checksum_manifest="$PACKAGE_BUILD_DIR/md5sum.txt"
remastered_iso="$PACKAGE_BUILD_DIR/HardenBox-13.x-amd64.iso"
xorriso -osirrox on -indev "$built_iso" \
    -extract /md5sum.txt "$checksum_manifest" >/dev/null 2>&1
chmod 0644 "$checksum_manifest"
sed -i '\|  \./boot/grub/grub.cfg$|d' "$checksum_manifest"
printf '%s  ./boot/grub/grub.cfg\n' "$(md5sum "$UEFI_MENU" | cut -d' ' -f1)" \
    >>"$checksum_manifest"

xorriso -indev "$built_iso" -outdev "$remastered_iso" \
    -boot_image any replay \
    -map "$BIOS_MENU" /isolinux/menu.cfg \
    -map "$UEFI_MENU" /boot/grub/grub.cfg \
    -map "$checksum_manifest" /md5sum.txt \
    -commit -end >/dev/null 2>&1

install -m 0644 "$remastered_iso" "$ISO_OUTPUT"
sha256sum "$ISO_OUTPUT" >"$ISO_OUTPUT.sha256"
printf 'ISO creee: %s\n' "$ISO_OUTPUT"
printf 'SHA256: %s\n' "$ISO_OUTPUT.sha256"

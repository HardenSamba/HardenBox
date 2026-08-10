#!/usr/bin/env bash

set -Eeuo pipefail
IFS=$'\n\t'

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
REPOSITORY_DIR=$(cd -- "$SCRIPT_DIR/.." && pwd)
PACKAGES_DIR="$REPOSITORY_DIR/Packages"
LOCAL_PACKAGES_DIR="$SCRIPT_DIR/local-packages"
ISO_OUTPUT_DIR="$SCRIPT_DIR/iso"
ISO_OUTPUT="$ISO_OUTPUT_DIR/HardenBox-13.x-amd64.iso"
PACKAGE_BUILD_DIR=$(mktemp -d)
trap 'rm -rf -- "$PACKAGE_BUILD_DIR"' EXIT
BIOS_MENU="$SCRIPT_DIR/boot/isolinux-menu.cfg"
UEFI_MENU="$SCRIPT_DIR/boot/grub.cfg"

command -v build-simple-cdd >/dev/null 2>&1 || {
    printf 'ERREUR: simple-cdd est absent. Installez les prerequis avec:\n' >&2
    printf '  sudo apt update && sudo apt install simple-cdd\n' >&2
    exit 1
}

command -v dpkg-deb >/dev/null 2>&1 || {
    printf 'ERREUR: dpkg-deb est requis.\n' >&2
    exit 1
}

command -v xorriso >/dev/null 2>&1 || {
    printf 'ERREUR: xorriso est requis pour personnaliser le menu de demarrage.\n' >&2
    exit 1
}

printf 'Construction des paquets HardenBox...\n'
if [[ -n ${PACKAGE_VERSION:-} ]]; then
    VERSION=$PACKAGE_VERSION OUTPUT_DIR=$PACKAGE_BUILD_DIR "$PACKAGES_DIR/build-packages.sh"
else
    OUTPUT_DIR=$PACKAGE_BUILD_DIR "$PACKAGES_DIR/build-packages.sh"
fi

rm -rf -- "$LOCAL_PACKAGES_DIR"
install -d -m 0755 "$LOCAL_PACKAGES_DIR" "$ISO_OUTPUT_DIR"

for package_name in hb-common hb-common-config hb-sambaad; do
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

#!/usr/bin/env bash

set -Eeuo pipefail
IFS=$'\n\t'

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
VERSION=${VERSION:-1.3.23}
MAINTAINER=${MAINTAINER:-HardenBox}
OUTPUT_DIR=${OUTPUT_DIR:-"$SCRIPT_DIR/dist"}

if [[ -z ${SOURCE_DATE_EPOCH:-} ]]; then
    SOURCE_DATE_EPOCH=$(git -C "$SCRIPT_DIR" log -1 --format=%ct -- . 2>/dev/null || printf '0')
    export SOURCE_DATE_EPOCH
fi

[[ $SOURCE_DATE_EPOCH =~ ^[0-9]+$ ]] || {
    printf 'ERREUR: SOURCE_DATE_EPOCH doit etre un entier positif.\n' >&2
    exit 1
}

command -v dpkg-deb >/dev/null 2>&1 || {
    printf 'ERREUR: dpkg-deb est requis pour construire les paquets.\n' >&2
    exit 1
}

[[ $VERSION =~ ^[0-9][A-Za-z0-9.+:~_-]*$ ]] || {
    printf 'ERREUR: version Debian invalide: %s\n' "$VERSION" >&2
    exit 1
}

install -d -m 0755 "$OUTPUT_DIR"

package_count=0
for source_dir in "$SCRIPT_DIR"/*/; do
    [[ -d $source_dir ]] || continue
    directory_name=${source_dir%/}
    directory_name=${directory_name##*/}
    [[ $directory_name == "dist" ]] && continue

    packaging_dir="$source_dir/debian"
    mapfile -d '' scripts < <(find "$source_dir" -maxdepth 1 -type f -name '*.sh' -print0 | sort -z)
    ((${#scripts[@]} > 0)) || [[ -d $packaging_dir ]] || {
        printf 'Ignore %s: aucun script ni metadata Debian.\n' "$directory_name"
        continue
    }

    package_name=$(tr '[:upper:]_' '[:lower:]-' <<<"$directory_name")
    package_name=${package_name//$'\n'/}
    if [[ -f $packaging_dir/package-name ]]; then
        package_name=$(<"$packaging_dir/package-name")
    fi
    [[ $package_name =~ ^[a-z0-9][a-z0-9+.-]+$ ]] || {
        printf 'ERREUR: le repertoire %s ne produit pas un nom de paquet Debian valide.\n' "$directory_name" >&2
        exit 1
    }

    build_root=$(mktemp -d)
    trap 'rm -rf -- "$build_root"' EXIT
    chmod 0755 "$build_root"
    install -d -m 0755 "$build_root/DEBIAN" "$build_root/usr/sbin"

    if [[ $package_name == "hb-common" && -n ${HARDENBOX_COMMON_ASSETS_DIR:-} ]]; then
        [[ -d $HARDENBOX_COMMON_ASSETS_DIR ]] || {
            printf 'ERREUR: ressources HB-Common absentes: %s\n' "$HARDENBOX_COMMON_ASSETS_DIR" >&2
            exit 1
        }
        install -d -m 0755 "$build_root/usr/share/hardenbox/assets"
        cp -a -- "$HARDENBOX_COMMON_ASSETS_DIR/." "$build_root/usr/share/hardenbox/assets/"
    fi

    for script in "${scripts[@]}"; do
        install -m 0755 "$script" "$build_root/usr/sbin/${script##*/}"
    done

    dependencies="bash, systemd, util-linux"
    if [[ -d $packaging_dir ]]; then
        if [[ -f $packaging_dir/depends ]]; then
            extra_dependencies=$(tr '\n' ',' <"$packaging_dir/depends")
            extra_dependencies=${extra_dependencies%,}
            [[ -z $extra_dependencies ]] || dependencies+=", $extra_dependencies"
        fi
        for maintainer_script in config postinst; do
            if [[ -f $packaging_dir/$maintainer_script ]]; then
                install -m 0755 "$packaging_dir/$maintainer_script" \
                    "$build_root/DEBIAN/$maintainer_script"
            fi
        done
        if [[ -f $packaging_dir/templates ]]; then
            install -m 0644 "$packaging_dir/templates" "$build_root/DEBIAN/templates"
            dependencies+=", debconf"
        fi
        if [[ -f $packaging_dir/include ]]; then
            while IFS=' ' read -r source_path destination_path file_mode || [[ -n $source_path ]]; do
                [[ -n $source_path && ${source_path:0:1} != "#" ]] || continue
                [[ $destination_path == usr/* ]] || {
                    printf 'ERREUR: destination de paquet invalide: %s\n' "$destination_path" >&2
                    exit 1
                }
                install -D -m "${file_mode:-0644}" "$source_dir$source_path" \
                    "$build_root/$destination_path"
            done <"$packaging_dir/include"
        fi
        if [[ -f $packaging_dir/runner ]]; then
            service_after="network-online.target"
            service_requires=""
            if [[ -f $packaging_dir/service-after ]]; then
                service_after+=" $(tr '\n' ' ' <"$packaging_dir/service-after")"
                service_after=${service_after% }
            fi
            if [[ -f $packaging_dir/service-requires ]]; then
                service_requires=$(tr '\n' ' ' <"$packaging_dir/service-requires")
                service_requires=${service_requires% }
            fi
            install -d -m 0755 "$build_root/usr/lib/hardenbox" \
                "$build_root/usr/lib/systemd/system"
            install -m 0755 "$packaging_dir/runner" \
                "$build_root/usr/lib/hardenbox/$package_name-runner"
            cat >"$build_root/usr/lib/systemd/system/$package_name-bootstrap.service" <<EOF
[Unit]
Description=Configuration initiale HardenBox - $directory_name
After=$service_after
Wants=network-online.target
${service_requires:+Requires=$service_requires}

[Service]
Type=oneshot
ExecStart=/usr/lib/hardenbox/$package_name-runner

[Install]
WantedBy=multi-user.target
EOF
            chmod 0644 "$build_root/usr/lib/systemd/system/$package_name-bootstrap.service"
        fi
    fi

    cat >"$build_root/DEBIAN/control" <<EOF
Package: $package_name
Version: $VERSION
Section: admin
Priority: optional
Architecture: all
Maintainer: $MAINTAINER
Depends: $dependencies
Description: Scripts d'administration HardenBox - $directory_name
 Installe les scripts du module HardenBox $directory_name dans /usr/sbin.
EOF

    output_file="$OUTPUT_DIR/${directory_name}_${VERSION}_all.deb"
    find "$build_root" -print0 | xargs -0 touch --no-dereference --date="@$SOURCE_DATE_EPOCH"
    dpkg-deb --build --root-owner-group "$build_root" "$output_file"
    rm -rf -- "$build_root"
    trap - EXIT
    ((package_count += 1))
done

((package_count > 0)) || {
    printf 'ERREUR: aucun paquet n\x27a ete construit.\n' >&2
    exit 1
}

printf '%d paquet(s) construit(s) dans %s\n' "$package_count" "$OUTPUT_DIR"
#!/usr/bin/env bash

set -Eeuo pipefail
IFS=$'\n\t'

SCRIPT_NAME=${0##*/}
PROFILE="server"
HOSTNAME_VALUE=""
ADMIN_USER="${SUDO_USER:-}"
DESKTOP_USER=""
TIMEZONE="Europe/Paris"
LOCALE_VALUE="fr_FR.UTF-8"
SSH_PORT="22"
SSH_ALLOWED_CIDR="0.0.0.0/0"
NETWORK_INTERFACE=""
IP_ADDRESS=""
GATEWAY=""
DNS_SERVERS=""
REGENERATE_MACHINE_ID=0
REBOOT=0

log() {
    printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"
}

die() {
    printf 'ERREUR: %s\n' "$*" >&2
    exit 1
}

usage() {
    cat <<EOF
Usage: sudo ./$SCRIPT_NAME [options]

Deploie et durcit une installation minimale de Debian 13.

Options:
  --profile server|gnome       Profil a installer (defaut: server)
  --hostname NOM              Nom d'hote a configurer
  --admin-user UTILISATEUR    Compte existant a ajouter au groupe sudo
  --desktop-user UTILISATEUR  Compte cible des personnalisations GNOME
  --timezone ZONE             Fuseau horaire (defaut: Europe/Paris)
  --locale LOCALE             Locale (defaut: fr_FR.UTF-8)
  --ssh-port PORT             Port SSH autorise par nftables (defaut: 22)
  --ssh-allowed-cidr CIDR     Source autorisee pour SSH (defaut: 0.0.0.0/0)
  --network-interface IFACE   Interface a configurer avec NetworkManager
  --ip-address CIDR           Adresse statique, par exemple 192.168.1.10/24
  --gateway IP                Passerelle IPv4
  --dns IP[,IP...]            Serveurs DNS separes par des virgules
  --regenerate-machine-id     Regenere l'identifiant d'un clone
  --reboot                    Redemarre apres une execution reussie
  -h, --help                  Affiche cette aide

La configuration reseau statique exige les quatre options --network-interface,
--ip-address, --gateway et --dns. Elle n'est appliquee qu'au profil gnome.
EOF
}

require_value() {
    [[ $# -ge 2 && -n $2 ]] || die "L'option $1 exige une valeur."
}

while (($#)); do
    case "$1" in
        --profile)
            require_value "$@"; PROFILE=$2; shift 2 ;;
        --hostname)
            require_value "$@"; HOSTNAME_VALUE=$2; shift 2 ;;
        --admin-user)
            require_value "$@"; ADMIN_USER=$2; shift 2 ;;
        --desktop-user)
            require_value "$@"; DESKTOP_USER=$2; shift 2 ;;
        --timezone)
            require_value "$@"; TIMEZONE=$2; shift 2 ;;
        --locale)
            require_value "$@"; LOCALE_VALUE=$2; shift 2 ;;
        --ssh-port)
            require_value "$@"; SSH_PORT=$2; shift 2 ;;
        --ssh-allowed-cidr)
            require_value "$@"; SSH_ALLOWED_CIDR=$2; shift 2 ;;
        --network-interface)
            require_value "$@"; NETWORK_INTERFACE=$2; shift 2 ;;
        --ip-address)
            require_value "$@"; IP_ADDRESS=$2; shift 2 ;;
        --gateway)
            require_value "$@"; GATEWAY=$2; shift 2 ;;
        --dns)
            require_value "$@"; DNS_SERVERS=$2; shift 2 ;;
        --regenerate-machine-id)
            REGENERATE_MACHINE_ID=1; shift ;;
        --reboot)
            REBOOT=1; shift ;;
        -h|--help)
            usage; exit 0 ;;
        *)
            die "Option inconnue: $1" ;;
    esac
done

[[ $EUID -eq 0 ]] || die "Ce script doit etre execute en root."
[[ $PROFILE == "server" || $PROFILE == "gnome" ]] || die "Le profil doit etre server ou gnome."
[[ $SSH_PORT =~ ^[0-9]+$ ]] && ((SSH_PORT >= 1 && SSH_PORT <= 65535)) || die "Port SSH invalide."

if [[ ! -r /etc/os-release ]]; then
    die "Impossible d'identifier le systeme."
fi

# shellcheck disable=SC1091
source /etc/os-release
[[ ${ID:-} == "debian" && ${VERSION_ID:-} == "13" ]] || \
    die "Ce script est reserve a Debian 13 (systeme detecte: ${PRETTY_NAME:-inconnu})."

network_values=0
for value in "$NETWORK_INTERFACE" "$IP_ADDRESS" "$GATEWAY" "$DNS_SERVERS"; do
    [[ -n $value ]] && ((network_values += 1))
done
((network_values == 0 || network_values == 4)) || \
    die "La configuration reseau exige interface, adresse, passerelle et DNS."
[[ $network_values -eq 0 || $PROFILE == "gnome" ]] || \
    die "La configuration NetworkManager est reservee au profil gnome."

if [[ -n $ADMIN_USER ]]; then
    id "$ADMIN_USER" >/dev/null 2>&1 || die "L'utilisateur $ADMIN_USER n'existe pas."
fi

if [[ $PROFILE == "gnome" ]]; then
    DESKTOP_USER=${DESKTOP_USER:-$ADMIN_USER}
    [[ -n $DESKTOP_USER ]] || \
        die "Le profil gnome exige --desktop-user ou --admin-user."
    id "$DESKTOP_USER" >/dev/null 2>&1 || die "L'utilisateur $DESKTOP_USER n'existe pas."
fi

backup_once() {
    local path=$1
    if [[ -e $path && ! -e ${path}.pre-hardenbox ]]; then
        cp -a -- "$path" "${path}.pre-hardenbox"
    fi
}

install_content() {
    local path=$1 mode=$2
    local temporary
    temporary=$(mktemp)
    cat >"$temporary"
    install -D -o root -g root -m "$mode" "$temporary" "$path"
    rm -f "$temporary"
}

enable_service() {
    systemctl enable "$@"
    if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
        systemctl start "$@"
    fi
}

disable_service() {
    if [[ ${HARDENBOX_INSTALLER:-0} == 1 ]]; then
        systemctl disable "$@" || true
    else
        systemctl disable --now "$@" || true
    fi
}

apt_install_available() {
    local packages=() package
    for package in "$@"; do
        if apt-get install --simulate --no-install-recommends \
            "${apt_download_options[@]}" "$package" >/dev/null 2>&1; then
            packages+=("$package")
        else
            log "Paquet sans version installable, ignore: $package"
        fi
    done
    ((${#packages[@]} == 0)) || apt-get install -y --no-install-recommends \
        "${apt_download_options[@]}" "${packages[@]}"
}

download_github_archive() {
    local repository=$1 reference=$2 destination=$3
    local temporary archive_root
    temporary=$(mktemp -d)
    if ! curl --fail --location --silent --show-error \
        "https://github.com/$repository/archive/$reference.tar.gz" \
        | tar -xz -C "$temporary"; then
        rm -rf -- "$temporary"
        return 1
    fi
    archive_root=$(find "$temporary" -mindepth 1 -maxdepth 1 -type d -print -quit)
    if [[ -z $archive_root ]]; then
        rm -rf -- "$temporary"
        return 1
    fi
    rm -rf -- "$destination"
    install -d -m 0755 "$(dirname "$destination")"
    mv -- "$archive_root" "$destination"
    rm -rf -- "$temporary"
}

install_bundled_archive() {
    local archive=$1 destination=$2
    local source="/usr/share/hardenbox/assets/$archive" temporary archive_root
    [[ -r $source ]] || die "Ressource hors ligne absente: $source"
    temporary=$(mktemp -d)
    tar -xzf "$source" -C "$temporary"
    archive_root=$(find "$temporary" -mindepth 1 -maxdepth 1 -type d -print -quit)
    [[ -n $archive_root ]] || die "Archive hors ligne invalide: $source"
    rm -rf -- "$destination"
    install -d -m 0755 "$(dirname "$destination")"
    mv -- "$archive_root" "$destination"
    rm -rf -- "$temporary"
}

export DEBIAN_FRONTEND=noninteractive

apt_download_options=()
if [[ ${HARDENBOX_INSTALLER:-0} == 1 ]]; then
    apt_download_options+=(--no-download)
    log "Mode installation hors ligne: utilisation exclusive des paquets deja presents"
else
    log "Mise a niveau de Debian 13"
    apt-get update
    apt-get full-upgrade -y
fi

log "Installation des outils et composants de securite"
apt-get install -y --no-install-recommends "${apt_download_options[@]}" \
    acl apt-listchanges auditd ca-certificates console-setup curl fail2ban \
    keyboard-configuration locales lynis needrestart nftables openssh-server \
    plocate sudo unattended-upgrades vim

log "Configuration de la locale et du fuseau horaire"
grep -Eq "^[# ]*${LOCALE_VALUE//./\.}[[:space:]]+UTF-8$" /etc/locale.gen || \
    printf '%s UTF-8\n' "$LOCALE_VALUE" >>/etc/locale.gen
sed -i -E "s|^[# ]*(${LOCALE_VALUE//./\.}[[:space:]]+UTF-8)$|\1|" /etc/locale.gen
locale-gen
update-locale LANG="$LOCALE_VALUE"
if [[ ${HARDENBOX_INSTALLER:-0} == 1 ]]; then
    ln -sfn "/usr/share/zoneinfo/$TIMEZONE" /etc/localtime
    printf '%s\n' "$TIMEZONE" >/etc/timezone
else
    timedatectl set-timezone "$TIMEZONE"
fi

log "Configuration du clavier francais"
install_content /etc/default/keyboard 0644 <<'EOF'
XKBMODEL="pc105"
XKBLAYOUT="fr"
XKBVARIANT="latin9"
XKBOPTIONS=""
BACKSPACE="guess"
EOF
setupcon --force --keyboard-only || true

if [[ -n $HOSTNAME_VALUE ]]; then
    [[ $HOSTNAME_VALUE =~ ^[a-zA-Z0-9]([a-zA-Z0-9.-]{0,251}[a-zA-Z0-9])?$ ]] || \
        die "Nom d'hote invalide: $HOSTNAME_VALUE"
    log "Configuration du nom d'hote: $HOSTNAME_VALUE"
    if [[ ${HARDENBOX_INSTALLER:-0} == 1 ]]; then
        printf '%s\n' "$HOSTNAME_VALUE" >/etc/hostname
    else
        hostnamectl set-hostname "$HOSTNAME_VALUE"
    fi
    short_hostname=${HOSTNAME_VALUE%%.*}
    if grep -qE '^127\.0\.1\.1[[:space:]]' /etc/hosts; then
        sed -i -E "s|^127\.0\.1\.1[[:space:]].*$|127.0.1.1\t$HOSTNAME_VALUE $short_hostname|" /etc/hosts
    else
        printf '127.0.1.1\t%s %s\n' "$HOSTNAME_VALUE" "$short_hostname" >>/etc/hosts
    fi
fi

if [[ -n $ADMIN_USER ]]; then
    log "Ajout de $ADMIN_USER au groupe sudo"
    usermod -aG sudo "$ADMIN_USER"
fi

log "Durcissement du service SSH"
install_content /etc/ssh/sshd_config.d/90-hardenbox.conf 0644 <<EOF
# Gere par $SCRIPT_NAME
Port $SSH_PORT
PermitRootLogin no
PubkeyAuthentication yes
PasswordAuthentication yes
KbdInteractiveAuthentication no
PermitEmptyPasswords no
X11Forwarding no
MaxAuthTries 4
LoginGraceTime 30
EOF
install -d -o root -g root -m 0755 /run/sshd
sshd -t
enable_service ssh
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    systemctl reload ssh
fi

log "Configuration du pare-feu nftables"
backup_once /etc/nftables.conf
install_content /etc/nftables.conf 0644 <<EOF
#!/usr/sbin/nft -f

flush ruleset

table inet filter {
    chain input {
        type filter hook input priority filter; policy drop;

        iifname "lo" accept
        ct state invalid drop
        ct state established,related accept
        ip protocol icmp accept
        ip6 nexthdr ipv6-icmp accept
        ip saddr $SSH_ALLOWED_CIDR tcp dport $SSH_PORT ct state new accept
    }

    chain forward {
        type filter hook forward priority filter; policy drop;
    }

    chain output {
        type filter hook output priority filter; policy accept;
    }
}
EOF
nft --check --file /etc/nftables.conf
enable_service nftables
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    nft --file /etc/nftables.conf
fi

log "Activation des mises a jour de securite automatiques"
install_content /etc/apt/apt.conf.d/52hardenbox-unattended-upgrades 0644 <<'EOF'
APT::Periodic::Enable "1";
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
APT::Periodic::AutocleanInterval "7";
Unattended-Upgrade::Remove-Unused-Dependencies "true";
Unattended-Upgrade::Automatic-Reboot "false";
EOF
enable_service unattended-upgrades

log "Desactivation de mDNS"
install_content /etc/systemd/resolved.conf.d/90-disable-mdns.conf 0644 <<'EOF'
[Resolve]
MulticastDNS=no
LLMNR=no
EOF
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]] && \
    systemctl list-unit-files systemd-resolved.service >/dev/null 2>&1; then
    systemctl try-restart systemd-resolved.service
fi
if systemctl list-unit-files avahi-daemon.service >/dev/null 2>&1; then
    disable_service avahi-daemon.service
    systemctl mask avahi-daemon.service avahi-daemon.socket
fi

log "Activation de l'audit et de la protection contre les tentatives SSH"
enable_service auditd
enable_service fail2ban
updatedb

if ((REGENERATE_MACHINE_ID)); then
    log "Regeneration de machine-id"
    rm -f /etc/machine-id /var/lib/dbus/machine-id
    systemd-machine-id-setup
    ln -sf /etc/machine-id /var/lib/dbus/machine-id
fi

if [[ $PROFILE == "gnome" ]]; then
    log "Installation du profil GNOME minimal"
    apt-get install -y --no-install-recommends "${apt_download_options[@]}" \
        dconf-cli flatpak gir1.2-gmenu-3.0 gir1.2-gnomedesktop-3.0 \
        gnome-core gnome-remote-desktop gnome-shell-extension-manager \
        gnome-shell-extensions gnome-software-plugin-flatpak gnome-tweaks \
        network-manager network-manager-gnome xdg-user-dirs

    systemctl enable gdm3.service
    systemctl set-default graphical.target

    apt_install_available \
        eiciel gnome-shell-extension-arc-menu gnome-shell-extension-blur-my-shell \
        gnome-shell-extension-dash-to-panel gnome-shell-extension-desktop-icons-ng nautilus-admin \
        nautilus-gtkhash nautilus-image-converter seahorse-nautilus

    log "Installation des themes Windows 10 et des icones We10X"
    if [[ ${HARDENBOX_OFFLINE:-0} == 1 ]]; then
        install_bundled_archive windows-10-3.2.1.tar.gz /usr/share/themes/Windows-10
        install_bundled_archive we10x-0f52ff2.tar.gz /usr/local/src/We10X-icon-theme
    else
        download_github_archive \
            B00merang-Project/Windows-10 3.2.1 /usr/share/themes/Windows-10
        download_github_archive \
            yeyushengfan258/We10X-icon-theme 0f52ff2 /usr/local/src/We10X-icon-theme
    fi
    bash /usr/local/src/We10X-icon-theme/install.sh \
        --dest /usr/share/icons --theme default --alternative

    log "Installation de l'extension Add to Desktop"
    extension_path=/usr/share/gnome-shell/extensions/add-to-desktop@tommimon.github.com
    extension_installed=0
    if [[ ${HARDENBOX_OFFLINE:-0} == 1 ]]; then
        install_bundled_archive add-to-desktop-v16.tar.gz "$extension_path"
        extension_installed=1
    elif download_github_archive Tommimon/add-to-desktop v16 "$extension_path"; then
        extension_installed=1
    fi
    if ((extension_installed)); then
        find /usr/share/themes/Windows-10 /usr/share/icons/We10X* "$extension_path" \
            -type d -exec chmod 0755 {} +
        find /usr/share/themes/Windows-10 /usr/share/icons/We10X* "$extension_path" \
            -type f -exec chmod a+r {} +
    else
        log "Extension Add to Desktop indisponible, installation poursuivie sans elle"
    fi

    log "Configuration systeme de GNOME avec dconf"
    install_content /etc/dconf/profile/user 0644 <<'EOF'
user-db:user
system-db:local
EOF
    install_content /etc/dconf/db/local.d/00-hardenbox 0644 <<'EOF'
[org/gnome/desktop/interface]
enable-hot-corners=false
color-scheme='prefer-light'
gtk-theme='Windows-10'
icon-theme='We10X'

[org/gnome/desktop/wm/preferences]
button-layout='appmenu:minimize,maximize,close'

[org/gnome/desktop/background]
picture-options='none'
primary-color='#0078d4'
secondary-color='#0078d4'

[org/gnome/nautilus/preferences]
show-hidden-files=true

[org/gnome/shell]
disable-user-extensions=false
enabled-extensions=['arcmenu@arcmenu.com', 'blur-my-shell@aunetx', 'dash-to-panel@jderose9.github.com', 'ding@rastersoft.com', 'add-to-desktop@tommimon.github.com']

[org/gnome/shell/extensions/dash-to-panel]
panel-position='BOTTOM'
panel-size=32
show-activities-button=false
show-window-previews=true
group-apps=true
click-action='CYCLE-MIN'
dot-style-focused='METRO'
dot-style-unfocused='METRO'

[org/gnome/shell/extensions/ding]
show-home=true
show-trash=true
show-volumes=true
show-network-volumes=true
EOF
    dconf update

    log "Creation du bureau Windows pour $DESKTOP_USER"
    desktop_home=$(getent passwd "$DESKTOP_USER" | cut -d: -f6)
    [[ -n $desktop_home && -d $desktop_home ]] || \
        die "Repertoire personnel introuvable pour $DESKTOP_USER."
    install -d -o "$DESKTOP_USER" -g "$DESKTOP_USER" -m 0755 \
        "$desktop_home/.config" "$desktop_home/Bureau"
    install_content "$desktop_home/.config/user-dirs.dirs" 0644 <<'EOF'
XDG_DESKTOP_DIR="$HOME/Bureau"
XDG_DOWNLOAD_DIR="$HOME/Telechargements"
XDG_TEMPLATES_DIR="$HOME/Modeles"
XDG_PUBLICSHARE_DIR="$HOME/Public"
XDG_DOCUMENTS_DIR="$HOME/Documents"
XDG_MUSIC_DIR="$HOME/Musique"
XDG_PICTURES_DIR="$HOME/Images"
XDG_VIDEOS_DIR="$HOME/Videos"
EOF
    install_content "$desktop_home/Bureau/poste-de-travail.desktop" 0755 <<'EOF'
[Desktop Entry]
Name=Poste de travail
Comment=Acceder a tous les disques et dossiers
Exec=nautilus computer:///
Icon=computer
Terminal=false
Type=Application
EOF
    chown -R "$DESKTOP_USER:$DESKTOP_USER" \
        "$desktop_home/.config/user-dirs.dirs" "$desktop_home/Bureau"

    log "Activation de NetworkManager"
    backup_once /etc/NetworkManager/NetworkManager.conf
    install_content /etc/NetworkManager/conf.d/90-hardenbox.conf 0644 <<'EOF'
[ifupdown]
managed=true
EOF
    enable_service NetworkManager

    if ((network_values == 4)); then
        log "Configuration statique de $NETWORK_INTERFACE"
        ip link show "$NETWORK_INTERFACE" >/dev/null 2>&1 || \
            die "Interface introuvable: $NETWORK_INTERFACE"
        connection_name="hardenbox-$NETWORK_INTERFACE"
        if nmcli -t -f NAME connection show | grep -Fxq "$connection_name"; then
            nmcli connection modify "$connection_name" \
                connection.interface-name "$NETWORK_INTERFACE" \
                ipv4.method manual ipv4.addresses "$IP_ADDRESS" \
                ipv4.gateway "$GATEWAY" ipv4.dns "${DNS_SERVERS//,/ }" \
                ipv4.ignore-auto-dns yes ipv6.method auto
        else
            nmcli connection add type ethernet ifname "$NETWORK_INTERFACE" \
                con-name "$connection_name" ipv4.method manual \
                ipv4.addresses "$IP_ADDRESS" ipv4.gateway "$GATEWAY" \
                ipv4.dns "${DNS_SERVERS//,/ }" ipv4.ignore-auto-dns yes \
                ipv6.method auto
        fi
        nmcli connection up "$connection_name"
    fi

    log "Configuration de Flathub et installation de SysD Manager"
    if [[ ${HARDENBOX_OFFLINE:-0} == 1 ]]; then
        flatpak remote-add --system --if-not-exists --no-gpg-verify hardenbox \
            file:///usr/share/hardenbox/assets/flatpak/.ostree/repo
        flatpak install --system --noninteractive -y hardenbox io.github.plrigaux.sysd-manager
    else
        flatpak remote-add --system --if-not-exists flathub \
            https://dl.flathub.org/repo/flathub.flatpakrepo
        flatpak install --system --noninteractive -y flathub io.github.plrigaux.sysd-manager
    fi
fi

log "Validation finale"
nft --check --file /etc/nftables.conf
sshd -t
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    systemctl is-active --quiet nftables
    systemctl is-active --quiet ssh
    systemctl is-active --quiet auditd
    systemctl is-active --quiet fail2ban
fi

log "Deploiement termine. Rapport Lynis disponible avec: sudo lynis audit system"
log "Un redemarrage est recommande pour appliquer le noyau et les composants GNOME."

if ((REBOOT)); then
    log "Redemarrage du systeme"
    systemctl reboot
fi
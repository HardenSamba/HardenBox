#!/usr/bin/env bash

set -Eeuo pipefail
IFS=$'\n\t'
umask 077

SCRIPT_NAME=${0##*/}
NON_INTERACTIVE=0
PASSWORD_STDIN=0
CLEANUP_PARTIAL=0
RESUME=0
DEPLOYMENT="forest"
REALM=""
DOMAIN=""
DOMAIN_ADMIN="Administrator"
DOMAIN_DNS=""
DC_HOSTNAME=""
NETWORK_INTERFACE=""
IP_ADDRESS=""
GATEWAY=""
DNS_FORWARDER="1.1.1.1"
ALLOWED_CIDR=""
FUNCTION_LEVEL="2016"
SAMBA_VERSION="4.24"
OPENRSAT_VERSION="0.4.386"
OPENRSAT_SHA256="941130ddfeb58bdfc2e4967a7e3604ed464bebbadb550a0ca26d7dbb6b0f40ed"
ADMIN_PASSWORD="${SAMBA_ADMIN_PASSWORD:-}"
TLS_CERT_SOURCE=""
TLS_KEY_SOURCE=""
TLS_CA_SOURCE=""
ENABLE_MDB=1
ENABLE_SMB_HARDENING=1
ENABLE_WINBIND=1
ENABLE_AUDIT=1
ENABLE_TRUSTED_LDAPS=0
ENABLE_ONLINE_BACKUP=1
ENABLE_PASSWORD_CHECK=0
ENABLE_HIBP=0
ENABLE_OPENRSAT=0
ENABLE_POWERSHELL=0
BACKUP_DIR=""
KEY_FILE=""
CREDENTIAL_DIR=""
OPENRSAT_FILE=""
MICROSOFT_REPO_FILE=""

cleanup() {
    ADMIN_PASSWORD=""
    unset SAMBA_ADMIN_PASSWORD
    [[ -z $KEY_FILE ]] || rm -f -- "$KEY_FILE"
    [[ -z $CREDENTIAL_DIR ]] || rm -rf -- "$CREDENTIAL_DIR"
    [[ -z $OPENRSAT_FILE ]] || rm -f -- "$OPENRSAT_FILE"
    [[ -z $MICROSOFT_REPO_FILE ]] || rm -f -- "$MICROSOFT_REPO_FILE"
}
trap cleanup EXIT

report_error() {
    local line=$1 command=$2 status=$3
    if [[ -n $ADMIN_PASSWORD ]]; then
        command=${command//"$ADMIN_PASSWORD"/'[MASQUE]'}
    fi
    printf 'ERREUR: statut %s a la ligne %s: %s\n' "$status" "$line" "$command" >&2
}
trap 'report_error "$LINENO" "$BASH_COMMAND" "$?"' ERR

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

Assistant de creation d'une foret ou d'ajout d'un DC Samba Active Directory sur Debian 13.
Sans option, une interface whiptail (ou texte) collecte les parametres.

Options non interactives:
  --non-interactive          N'affiche aucun wizard
    --offline                  Interdit tout acces aux depots en ligne
    --password-stdin           Lit le mot de passe Administrator sur l'entree standard
    --cleanup-partial          Supprime un provisionnement incomplet apres sauvegarde
    --resume                   Reprend la post-configuration d'un domaine provisionne
    --deployment forest|dc     Cree une foret ou ajoute un DC (defaut: forest)
  --realm DOMAINE.DNS        Royaume Kerberos, par exemple AD.EXEMPLE.FR
  --domain NETBIOS           Nom NetBIOS, par exemple EXEMPLE
    --domain-admin COMPTE      Compte autorise a joindre un DC (defaut: Administrator)
    --domain-dns IP            DNS/DC existant utilise pour decouvrir et joindre le domaine
  --hostname NOM             Nom court du DC (15 caracteres maximum)
  --interface IFACE          Interface reseau, par exemple ens18
  --ip-address CIDR          Adresse statique, par exemple 192.168.10.10/24
  --gateway IP               Passerelle IPv4
  --dns-forwarder IP         Redirecteur DNS (defaut: 1.1.1.1)
  --allowed-cidr CIDR        Reseau autorise vers AD, par exemple 192.168.10.0/24
  --function-level NIVEAU    2008_R2 ou 2016 (defaut: 2016)
  --hardening LISTE          Options separees par des virgules:
                                 mdb,smb,winbind,audit,ldaps,backup,
                                 password,hibp,openrsat,powershell ou none
    --tls-cert FICHIER         Certificat PEM LDAPS emis pour le FQDN du DC
    --tls-key FICHIER          Cle privee PEM du certificat LDAPS
    --tls-ca FICHIER           Chaine PEM de l'autorite de certification
  -h, --help                 Affiche cette aide

En mode non interactif, le mot de passe Administrator doit etre fourni dans
la variable d'environnement SAMBA_ADMIN_PASSWORD. Il n'est jamais accepte en
argument afin de ne pas l'enregistrer dans l'historique du shell.
EOF
}

require_value() {
    [[ $# -ge 2 && -n $2 ]] || die "L'option $1 exige une valeur."
}

set_hardening_options() {
    local options=$1 option
    ENABLE_MDB=0
    ENABLE_SMB_HARDENING=0
    ENABLE_WINBIND=0
    ENABLE_AUDIT=0
    ENABLE_TRUSTED_LDAPS=0
    ENABLE_ONLINE_BACKUP=0
    ENABLE_PASSWORD_CHECK=0
    ENABLE_HIBP=0
    ENABLE_OPENRSAT=0
    ENABLE_POWERSHELL=0
    [[ $options == "none" ]] && return
    while IFS= read -r option; do
        case "$option" in
            mdb) ENABLE_MDB=1 ;;
            smb) ENABLE_SMB_HARDENING=1 ;;
            winbind) ENABLE_WINBIND=1 ;;
            audit) ENABLE_AUDIT=1 ;;
            ldaps) ENABLE_TRUSTED_LDAPS=1 ;;
            backup) ENABLE_ONLINE_BACKUP=1 ;;
            password) ENABLE_PASSWORD_CHECK=1 ;;
            hibp) ENABLE_HIBP=1; ENABLE_PASSWORD_CHECK=1 ;;
            openrsat) ENABLE_OPENRSAT=1 ;;
            powershell) ENABLE_POWERSHELL=1 ;;
            *) die "Option de durcissement inconnue: $option" ;;
        esac
    done < <(tr ',' '\n' <<<"$options")
}

while (($#)); do
    case "$1" in
        --non-interactive) NON_INTERACTIVE=1; shift ;;
        --offline) HARDENBOX_OFFLINE=1; shift ;;
        --password-stdin) PASSWORD_STDIN=1; shift ;;
        --cleanup-partial) CLEANUP_PARTIAL=1; shift ;;
        --resume) RESUME=1; shift ;;
        --deployment) require_value "$@"; DEPLOYMENT=$2; shift 2 ;;
        --realm) require_value "$@"; REALM=$2; shift 2 ;;
        --domain) require_value "$@"; DOMAIN=$2; shift 2 ;;
        --domain-admin) require_value "$@"; DOMAIN_ADMIN=$2; shift 2 ;;
        --domain-dns) require_value "$@"; DOMAIN_DNS=$2; shift 2 ;;
        --hostname) require_value "$@"; DC_HOSTNAME=$2; shift 2 ;;
        --interface) require_value "$@"; NETWORK_INTERFACE=$2; shift 2 ;;
        --ip-address) require_value "$@"; IP_ADDRESS=$2; shift 2 ;;
        --gateway) require_value "$@"; GATEWAY=$2; shift 2 ;;
        --dns-forwarder) require_value "$@"; DNS_FORWARDER=$2; shift 2 ;;
        --allowed-cidr) require_value "$@"; ALLOWED_CIDR=$2; shift 2 ;;
        --function-level) require_value "$@"; FUNCTION_LEVEL=$2; shift 2 ;;
        --hardening) require_value "$@"; set_hardening_options "$2"; shift 2 ;;
        --tls-cert) require_value "$@"; TLS_CERT_SOURCE=$2; shift 2 ;;
        --tls-key) require_value "$@"; TLS_KEY_SOURCE=$2; shift 2 ;;
        --tls-ca) require_value "$@"; TLS_CA_SOURCE=$2; shift 2 ;;
        -h|--help) usage; exit 0 ;;
        *) die "Option inconnue: $1" ;;
    esac
done

[[ $EUID -eq 0 ]] || die "Ce script doit etre execute en root."
if ((PASSWORD_STDIN)); then
    ((NON_INTERACTIVE)) || die "--password-stdin exige --non-interactive."
    IFS= read -r ADMIN_PASSWORD || die "Impossible de lire le mot de passe sur l'entree standard."
fi
[[ $DEPLOYMENT == "forest" || $DEPLOYMENT == "dc" ]] || \
    die "Le deploiement doit etre forest ou dc."
[[ -r /etc/os-release ]] || die "Impossible d'identifier le systeme."
# shellcheck disable=SC1091
source /etc/os-release
[[ ${ID:-} == "debian" && ${VERSION_ID:-} == "13" ]] || \
    die "Ce script est reserve a Debian 13 (detecte: ${PRETTY_NAME:-inconnu})."

memory_mib=$(awk '/^MemTotal:/ {printf "%d", $2 / 1024}' /proc/meminfo)
cpu_count=$(nproc)
disk_free_gib=$(df --output=avail -BG / | awk 'NR == 2 {gsub(/G/, "", $1); print $1}')
((memory_mib >= 4096)) || log "AVERTISSEMENT: ${memory_mib} Mio de RAM detectes; le document recommande au moins 4 Gio."
((cpu_count >= 4)) || log "AVERTISSEMENT: $cpu_count vCPU detectes; le document recommande au moins 4 vCPU."
((disk_free_gib >= 10)) || log "AVERTISSEMENT: ${disk_free_gib} Gio libres sur /; le document recommande au moins 10 Gio."

((CLEANUP_PARTIAL == 0 || RESUME == 0)) || die "--cleanup-partial et --resume sont incompatibles."

if [[ -e /var/lib/samba/private/sam.ldb ]]; then
    if samba-tool domain level show >/dev/null 2>&1; then
        ((RESUME)) || die \
            "Un controleur Samba AD provisionne existe deja. Utilisez --resume pour terminer sa post-configuration."
        log "Domaine existant valide: reprise de la post-configuration sans reprovisionnement."
    else
        ((CLEANUP_PARTIAL)) || die \
            "Un provisionnement Samba incomplet existe. Verifiez son etat, puis relancez avec --cleanup-partial pour le sauvegarder et le supprimer."

        partial_backup="/root/samba-partial-backup-$(date '+%Y%m%d-%H%M%S')"
        install -d -m 0700 "$partial_backup"
        systemctl stop samba-ad-dc samba winbind nmbd smbd 2>/dev/null || true
        for partial_path in /var/lib/samba /var/cache/samba /etc/samba /etc/krb5.keytab; do
            if [[ -e $partial_path ]]; then
                partial_destination="$partial_backup/${partial_path#/}"
                install -d "$(dirname "$partial_destination")"
                cp -a -- "$partial_path" "$partial_destination"
            fi
        done
        rm -rf /var/lib/samba /var/cache/samba /run/samba
        rm -f /etc/samba/smb.conf /etc/krb5.keytab
        log "Provisionnement incomplet sauvegarde dans $partial_backup puis nettoye."
    fi
elif ((RESUME)); then
    die "--resume exige un domaine deja provisionne."
fi

if ((RESUME)); then
    REALM=$(testparm -s --parameter-name='realm' 2>/dev/null | tr '[:lower:]' '[:upper:]')
    DOMAIN=$(testparm -s --parameter-name='workgroup' 2>/dev/null | tr '[:lower:]' '[:upper:]')
    DC_HOSTNAME=$(testparm -s --parameter-name='netbios name' 2>/dev/null | tr '[:upper:]' '[:lower:]')
    [[ -n $REALM && -n $DOMAIN && -n $DC_HOSTNAME ]] || \
        die "Impossible de lire l'identite du domaine existant pour la reprise."
fi

if [[ ${HARDENBOX_OFFLINE:-0} != 1 ]] && ((NON_INTERACTIVE == 0)) && \
    [[ -t 0 && -t 1 ]] && ! command -v whiptail >/dev/null 2>&1; then
    log "Installation de whiptail pour l'interface du wizard"
    if ! (apt-get update && apt-get install -y --no-install-recommends whiptail); then
        log "Whiptail indisponible; poursuite avec l'assistant en mode texte."
    fi
fi

default_interface=$(ip -4 route show default 2>/dev/null | awk 'NR == 1 {print $5}')
default_gateway=$(ip -4 route show default 2>/dev/null | awk 'NR == 1 {print $3}')
default_ip=""
if [[ -n $default_interface ]]; then
    default_ip=$(ip -o -4 address show dev "$default_interface" scope global 2>/dev/null | awk 'NR == 1 {print $4}')
fi

has_whiptail() {
    [[ -t 0 && -t 1 ]] && command -v whiptail >/dev/null 2>&1
}

ipv4_to_integer() {
    local address=$1 first second third fourth
    IFS=. read -r first second third fourth <<<"$address"
    [[ $first =~ ^[0-9]+$ && $second =~ ^[0-9]+$ && \
       $third =~ ^[0-9]+$ && $fourth =~ ^[0-9]+$ ]] || return 1
    ((10#$first <= 255 && 10#$second <= 255 && 10#$third <= 255 && 10#$fourth <= 255)) || return 1
    printf '%u' "$(((10#$first << 24) | (10#$second << 16) | (10#$third << 8) | 10#$fourth))"
}

validate_ipv4_cidr() {
    local value=$1 address prefix extra
    IFS=/ read -r address prefix extra <<<"$value"
    [[ -z $extra && $prefix =~ ^[0-9]+$ ]] || return 1
    ((prefix >= 1 && prefix <= 32)) || return 1
    ipv4_to_integer "$address" >/dev/null
}

network_from_cidr() {
    local value=$1 address prefix integer mask network
    IFS=/ read -r address prefix <<<"$value"
    integer=$(ipv4_to_integer "$address") || return 1
    mask=$(((0xFFFFFFFF << (32 - prefix)) & 0xFFFFFFFF))
    network=$((integer & mask))
    printf '%d.%d.%d.%d/%d' \
        "$(((network >> 24) & 255))" "$(((network >> 16) & 255))" \
        "$(((network >> 8) & 255))" "$((network & 255))" "$prefix"
}

ask_value() {
    local variable=$1 title=$2 prompt=$3 default_value=$4 value
    if has_whiptail; then
        value=$(whiptail --title "$title" --inputbox "$prompt" 10 72 "$default_value" 3>&1 1>&2 2>&3) || exit 1
    else
        read -r -p "$prompt [$default_value] : " value
        value=${value:-$default_value}
    fi
    printf -v "$variable" '%s' "$value"
}

ask_password() {
    local first second
    while true; do
        if has_whiptail; then
            first=$(whiptail --title "Mot de passe du domaine" --passwordbox \
                "Mot de passe du compte Administrator (14 caracteres minimum)." 10 72 3>&1 1>&2 2>&3) || exit 1
            second=$(whiptail --title "Confirmation" --passwordbox \
                "Confirmez le mot de passe Administrator." 10 72 3>&1 1>&2 2>&3) || exit 1
        else
            read -r -s -p "Mot de passe Administrator : " first; printf '\n'
            read -r -s -p "Confirmation : " second; printf '\n'
        fi
        if [[ $first == "$second" ]]; then
            ADMIN_PASSWORD=$first
            return
        fi
        has_whiptail && whiptail --msgbox "Les mots de passe sont differents." 8 50
        has_whiptail || printf 'Les mots de passe sont differents.\n' >&2
    done
}

confirm() {
    local title=$1 message=$2 answer
    if has_whiptail; then
        whiptail --title "$title" --yesno "$message" 20 78
    else
        read -r -p "$message [oui/NON] : " answer
        [[ ${answer,,} == "oui" || ${answer,,} == "o" ]]
    fi
}

ask_hardening_options() {
    local selections selection
    if has_whiptail; then
        selections=$(whiptail --title "Options de durcissement" --checklist \
            "Selectionnez les fonctions a appliquer. Espace coche ou decoche une option." \
            24 82 13 \
            mdb "Backend LMDB/MDB pour un nouveau domaine" ON \
            smb "Durcissement SMB, NTLM, NetBIOS et impression" ON \
            winbind "Integration Winbind dans NSS et PAM" ON \
            audit "Journaux d'audit AD et SYSVOL" ON \
            backup "Sauvegarde AD en ligne apres validation" ON \
            ldaps "Certificat LDAPS approuve fourni en PEM" OFF \
            password "Controle local renforce des mots de passe" OFF \
            hibp "Refuser les mots de passe compromis via HIBP" OFF \
            openrsat "Installer OpenRSAT et ses dependances GTK" OFF \
            powershell "Installer PowerShell Core depuis Microsoft" OFF \
            3>&1 1>&2 2>&3) || exit 1
        selections=${selections//\"/}
        selections=${selections// /,}
        [[ -n $selections ]] && set_hardening_options "$selections" || set_hardening_options none
    else
        confirm "Durcissement" "Utiliser le backend LMDB/MDB ?" || ENABLE_MDB=0
        confirm "Durcissement" "Appliquer le durcissement SMB et NTLM ?" || ENABLE_SMB_HARDENING=0
        confirm "Durcissement" "Integrer Winbind dans NSS et PAM ?" || ENABLE_WINBIND=0
        confirm "Durcissement" "Activer les journaux d'audit Samba ?" || ENABLE_AUDIT=0
        confirm "Durcissement" "Creer une sauvegarde AD en ligne ?" || ENABLE_ONLINE_BACKUP=0
        confirm "Durcissement" "Installer un certificat LDAPS approuve ?" && ENABLE_TRUSTED_LDAPS=1
        confirm "Durcissement" "Activer le controle local renforce des mots de passe ?" && ENABLE_PASSWORD_CHECK=1
        if confirm "Durcissement" "Verifier aussi les mots de passe avec Have I Been Pwned ?"; then
            ENABLE_HIBP=1
            ENABLE_PASSWORD_CHECK=1
        fi
        confirm "Administration" "Installer OpenRSAT et ses dependances GTK ?" && ENABLE_OPENRSAT=1
        confirm "Administration" "Installer PowerShell Core depuis Microsoft ?" && ENABLE_POWERSHELL=1
    fi
    return 0
}

if ((NON_INTERACTIVE == 0)); then
    if has_whiptail; then
        if [[ $DEPLOYMENT == "forest" ]]; then
            deployment_message="Cet assistant cree le PREMIER controleur d'une nouvelle foret."
        else
            deployment_message="Cet assistant ajoute un controleur a un domaine Active Directory existant."
        fi
        whiptail --title "Provisionnement Samba AD" --msgbox \
            "$deployment_message\n\nUne sauvegarde de la configuration sera creee avant modification." 13 76
    fi
    ask_value REALM "Domaine Active Directory" \
        "Nom DNS du domaine (evitez .local) :" "${REALM:-ad.exemple.fr}"
    REALM=${REALM^^}
    suggested_domain=${REALM%%.*}
    ask_value DOMAIN "Nom NetBIOS" \
        "Nom court NetBIOS du domaine :" "${DOMAIN:-${suggested_domain^^}}"
    if [[ $DEPLOYMENT == "dc" ]]; then
        ask_value DOMAIN_ADMIN "Compte du domaine" \
            "Compte autorise a ajouter un controleur de domaine :" "$DOMAIN_ADMIN"
        ask_value DOMAIN_DNS "DNS du domaine" \
            "Adresse IPv4 d'un DNS/controleur existant :" "$DOMAIN_DNS"
    fi
    ask_value DC_HOSTNAME "Controleur de domaine" \
        "Nom court du serveur (15 caracteres maximum) :" "${DC_HOSTNAME:-dc1}"
    ask_value NETWORK_INTERFACE "Interface reseau" \
        "Interface a configurer en IP statique :" "${NETWORK_INTERFACE:-$default_interface}"
    ask_value IP_ADDRESS "Adresse IPv4" \
        "Adresse IPv4 statique avec prefixe CIDR :" "${IP_ADDRESS:-$default_ip}"
    ask_value GATEWAY "Passerelle" \
        "Passerelle IPv4 :" "${GATEWAY:-$default_gateway}"
    ask_value DNS_FORWARDER "DNS externe" \
        "Redirecteur DNS utilise par Samba :" "$DNS_FORWARDER"
    default_network=$(network_from_cidr "$IP_ADDRESS" 2>/dev/null || true)
    ask_value ALLOWED_CIDR "Reseau autorise" \
        "Reseau autorise a contacter les services AD :" "${ALLOWED_CIDR:-$default_network}"
    ask_hardening_options
    if ((ENABLE_TRUSTED_LDAPS)); then
        ask_value TLS_CERT_SOURCE "Certificat LDAPS" "Chemin du certificat PEM du DC :" "$TLS_CERT_SOURCE"
        ask_value TLS_KEY_SOURCE "Cle LDAPS" "Chemin de la cle privee PEM :" "$TLS_KEY_SOURCE"
        ask_value TLS_CA_SOURCE "Autorite LDAPS" "Chemin de la chaine CA PEM :" "$TLS_CA_SOURCE"
    fi
    [[ -n $ADMIN_PASSWORD ]] || ask_password
fi

REALM=${REALM^^}
DOMAIN=${DOMAIN^^}
DC_HOSTNAME=${DC_HOSTNAME,,}

[[ -n $REALM && -n $DOMAIN && -n $DC_HOSTNAME && -n $NETWORK_INTERFACE && \
   -n $IP_ADDRESS && -n $GATEWAY && -n $DNS_FORWARDER && -n $ALLOWED_CIDR ]] || \
    die "Tous les parametres sont obligatoires. Consultez --help."
[[ -n $ADMIN_PASSWORD ]] || die "Definissez SAMBA_ADMIN_PASSWORD en mode non interactif."
if [[ $DEPLOYMENT == "dc" ]]; then
    [[ -n $DOMAIN_ADMIN && -n $DOMAIN_DNS ]] || \
        die "L'ajout d'un DC exige --domain-admin et --domain-dns."
fi
[[ $REALM == *.* && $REALM != *.LOCAL ]] || die "Le realm doit etre un domaine DNS et ne doit pas finir par .local."
[[ $REALM =~ ^[A-Z0-9]([A-Z0-9.-]*[A-Z0-9])$ ]] || die "Realm invalide: $REALM"
[[ $DOMAIN =~ ^[A-Z0-9][A-Z0-9-]{0,14}$ ]] || die "Nom NetBIOS invalide (1 a 15 caracteres)."
[[ $DC_HOSTNAME =~ ^[a-z0-9][a-z0-9-]{0,14}$ ]] || die "Nom d'hote invalide (1 a 15 caracteres)."
[[ $FUNCTION_LEVEL == "2008_R2" || $FUNCTION_LEVEL == "2016" ]] || die "Niveau fonctionnel invalide."
if [[ -n $TLS_CERT_SOURCE || -n $TLS_KEY_SOURCE || -n $TLS_CA_SOURCE ]]; then
    ENABLE_TRUSTED_LDAPS=1
fi
if ((ENABLE_TRUSTED_LDAPS)); then
    [[ -n $TLS_CERT_SOURCE && -n $TLS_KEY_SOURCE && -n $TLS_CA_SOURCE ]] || \
        die "Le durcissement LDAPS exige --tls-cert, --tls-key et --tls-ca."
    [[ -r $TLS_CERT_SOURCE && -r $TLS_KEY_SOURCE && -r $TLS_CA_SOURCE ]] || \
        die "Un des fichiers TLS est absent ou illisible."
fi
ip link show "$NETWORK_INTERFACE" >/dev/null 2>&1 || die "Interface introuvable: $NETWORK_INTERFACE"
validate_ipv4_cidr "$IP_ADDRESS" || die "Adresse statique CIDR invalide: $IP_ADDRESS"
ipv4_to_integer "$GATEWAY" >/dev/null || die "Passerelle IPv4 invalide: $GATEWAY"
ipv4_to_integer "$DNS_FORWARDER" >/dev/null || die "Redirecteur DNS IPv4 invalide: $DNS_FORWARDER"
if [[ $DEPLOYMENT == "dc" ]]; then
    ipv4_to_integer "$DOMAIN_DNS" >/dev/null || die "DNS du domaine invalide: $DOMAIN_DNS"
fi
validate_ipv4_cidr "$ALLOWED_CIDR" || die "Reseau autorise invalide: $ALLOWED_CIDR"

address_integer=$(ipv4_to_integer "${IP_ADDRESS%/*}")
gateway_integer=$(ipv4_to_integer "$GATEWAY")
prefix=${IP_ADDRESS#*/}
network_mask=$(((0xFFFFFFFF << (32 - prefix)) & 0xFFFFFFFF))
(( (address_integer & network_mask) == (gateway_integer & network_mask) )) || \
    die "La passerelle n'appartient pas au reseau de l'adresse statique."

if [[ $DEPLOYMENT == "forest" ]] && { ((${#ADMIN_PASSWORD} < 14)) || \
   [[ ! $ADMIN_PASSWORD =~ [A-Z] || ! $ADMIN_PASSWORD =~ [a-z] || \
      ! $ADMIN_PASSWORD =~ [0-9] || ! $ADMIN_PASSWORD =~ [^A-Za-z0-9] ]]; }; then
    die "Le mot de passe doit avoir au moins 14 caracteres, avec majuscule, minuscule, chiffre et symbole."
fi

if [[ $DEPLOYMENT == "dc" ]]; then
    log "Verification DNS du domaine $REALM via $DOMAIN_DNS"
    host -W 3 -t SOA "${REALM,,}" "$DOMAIN_DNS" >/dev/null || \
        die "La zone DNS ${REALM,,} est introuvable via $DOMAIN_DNS."
    host -W 3 -t SRV "_ldap._tcp.dc._msdcs.${REALM,,}" "$DOMAIN_DNS" >/dev/null || \
        die "Les controleurs LDAP du domaine sont introuvables via $DOMAIN_DNS."
    host -W 3 -t SRV "_kerberos._tcp.${REALM,,}" "$DOMAIN_DNS" >/dev/null || \
        die "Le service Kerberos du domaine est introuvable via $DOMAIN_DNS."
fi

if [[ ${HARDENBOX_OFFLINE:-0} == 1 ]] && ((ENABLE_HIBP)); then
    log "HIBP desactive: ce controle exige un service web externe."
    ENABLE_HIBP=0
fi

FQDN="$DC_HOSTNAME.${REALM,,}"
SERVER_IP=${IP_ADDRESS%/*}
if ((RESUME)); then
    existing_realm=$(testparm -s --parameter-name='realm' 2>/dev/null | tr '[:lower:]' '[:upper:]')
    existing_domain=$(testparm -s --parameter-name='workgroup' 2>/dev/null | tr '[:lower:]' '[:upper:]')
    [[ $REALM == "$existing_realm" && $DOMAIN == "$existing_domain" ]] || \
        die "Les parametres de reprise ne correspondent pas au domaine existant."
fi
selected_hardening="MDB=$ENABLE_MDB, SMB=$ENABLE_SMB_HARDENING, Winbind=$ENABLE_WINBIND, Audit=$ENABLE_AUDIT, LDAPS=$ENABLE_TRUSTED_LDAPS, Sauvegarde=$ENABLE_ONLINE_BACKUP, MotsDePasse=$ENABLE_PASSWORD_CHECK, HIBP=$ENABLE_HIBP, OpenRSAT=$ENABLE_OPENRSAT, PowerShell=$ENABLE_POWERSHELL"
operation_label="Nouvelle foret Samba AD"
[[ $DEPLOYMENT == "forest" ]] || operation_label="Nouveau controleur du domaine existant"
summary="$operation_label\n\nRealm : $REALM\nNetBIOS : $DOMAIN\nDC : $FQDN\nIP : $IP_ADDRESS\nPasserelle : $GATEWAY\nDNS externe : $DNS_FORWARDER\nReseau autorise : $ALLOWED_CIDR\nNiveau fonctionnel : $FUNCTION_LEVEL\nDurcissement : $selected_hardening\n\nATTENTION : le reseau, DNS, Samba, nftables et Chrony seront modifies."

if ((NON_INTERACTIVE == 0)); then
    confirm "Confirmation finale" "$summary\n\nLancer le provisionnement ?" || die "Operation annulee."
else
    log "Deploiement non interactif de $FQDN pour le domaine $REALM ($DEPLOYMENT)"
fi

backup_once() {
    local path=$1 relative
    [[ -e $path || -L $path ]] || return 0
    relative=${path#/}
    install -d "$BACKUP_DIR/$(dirname "$relative")"
    cp -a -- "$path" "$BACKUP_DIR/$relative"
}

install_content() {
    local path=$1 mode=$2 temporary
    temporary=$(mktemp)
    cat >"$temporary"
    install -D -o root -g root -m "$mode" "$temporary" "$path"
    rm -f "$temporary"
}

set_smb_option() {
    local section=$1 option=$2 value=$3 temporary
    temporary=$(mktemp)
    awk -v wanted_section="$section" -v wanted_option="$option" -v wanted_value="$value" '
        function trim(value) {
            sub(/^[[:space:]]+/, "", value)
            sub(/[[:space:]]+$/, "", value)
            return value
        }
        function emit_option() {
            print "\t" wanted_option " = " wanted_value
            option_written = 1
        }
        /^\[[^]]+\][[:space:]]*$/ {
            if (in_section && !option_written) emit_option()
            heading = tolower(trim($0))
            in_section = (heading == "[" tolower(wanted_section) "]")
            option_written = 0
            print
            next
        }
        {
            if (in_section && index($0, "=") > 0) {
                key = trim(substr($0, 1, index($0, "=") - 1))
                if (tolower(key) == tolower(wanted_option)) {
                    emit_option()
                    next
                }
            }
            print
        }
        END {
            if (in_section && !option_written) emit_option()
        }
    ' /etc/samba/smb.conf >"$temporary"
    install -o root -g root -m 0644 "$temporary" /etc/samba/smb.conf
    rm -f "$temporary"
}

BACKUP_DIR="/root/samba-ad-backup-$(date '+%Y%m%d-%H%M%S')"
install -d -m 0700 "$BACKUP_DIR"
for path in /etc/hostname /etc/hosts /etc/resolv.conf /etc/samba/smb.conf \
    /etc/krb5.conf /etc/nftables.conf /etc/chrony/chrony.conf \
    /etc/NetworkManager/NetworkManager.conf /etc/network/interfaces \
    /etc/nsswitch.conf; do
    backup_once "$path"
done

log "Configuration du nom d'hote et de /etc/hosts"
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    hostnamectl set-hostname "$FQDN"
fi
printf '%s\n' "$FQDN" >/etc/hostname
if grep -qE "^[[:space:]]*$SERVER_IP[[:space:]]" /etc/hosts; then
    sed -i -E "s|^[[:space:]]*$SERVER_IP[[:space:]].*$|$SERVER_IP $FQDN $DC_HOSTNAME|" /etc/hosts
else
    printf '%s %s %s\n' "$SERVER_IP" "$FQDN" "$DC_HOSTNAME" >>/etc/hosts
fi

log "Configuration de l'adresse statique sur $NETWORK_INTERFACE"
network_dns=$DNS_FORWARDER
[[ $DEPLOYMENT == "forest" ]] || network_dns=$DOMAIN_DNS
if command -v nmcli >/dev/null 2>&1 && systemctl is-active --quiet NetworkManager; then
    systemctl disable networking.service >/dev/null 2>&1 || true
    systemctl disable --now dhcpcd.service >/dev/null 2>&1 || true
    if [[ -f /etc/network/interfaces ]]; then
        sed -i -E \
            -e "/^[[:space:]]*(auto|allow-hotplug)[[:space:]]+$NETWORK_INTERFACE([[:space:]]|$)/d" \
            -e "/^[[:space:]]*iface[[:space:]]+$NETWORK_INTERFACE[[:space:]]+inet[[:space:]]+dhcp([[:space:]]|$)/d" \
            /etc/network/interfaces
    fi
    rm -f /etc/network/interfaces.d/90-samba-ad
    nmcli device set "$NETWORK_INTERFACE" managed yes
    nmcli -t -f DEVICE device status | grep -Fxq "$NETWORK_INTERFACE" || \
        die "NetworkManager ne peut pas gerer l'interface $NETWORK_INTERFACE."
    connection_name="samba-ad-$NETWORK_INTERFACE"
    if nmcli -t -f NAME connection show | grep -Fxq "$connection_name"; then
        nmcli connection modify "$connection_name" connection.interface-name "$NETWORK_INTERFACE" \
            ipv4.method manual ipv4.addresses "$IP_ADDRESS" ipv4.gateway "$GATEWAY" \
            ipv4.dns "$network_dns" ipv4.ignore-auto-dns yes \
            ipv4.link-local disabled ipv6.method disabled
    else
        nmcli connection add type ethernet ifname "$NETWORK_INTERFACE" con-name "$connection_name" \
            ipv4.method manual ipv4.addresses "$IP_ADDRESS" ipv4.gateway "$GATEWAY" \
            ipv4.dns "$network_dns" ipv4.ignore-auto-dns yes \
            ipv4.link-local disabled ipv6.method disabled
    fi
    ip -4 address flush dev "$NETWORK_INTERFACE" scope global
    nmcli connection up "$connection_name"
else
    install_content "/etc/network/interfaces.d/90-samba-ad" 0644 <<EOF
auto $NETWORK_INTERFACE
iface $NETWORK_INTERFACE inet static
    address $IP_ADDRESS
    gateway $GATEWAY
EOF
fi

if [[ $DEPLOYMENT == "dc" ]]; then
    rm -f /etc/resolv.conf
    install_content /etc/resolv.conf 0644 <<EOF
search ${REALM,,}
nameserver $DOMAIN_DNS
EOF
fi

export DEBIAN_FRONTEND=noninteractive
packages=(samba samba-ad-dc winbind libnss-winbind krb5-user smbclient ldb-tools python3-cryptography)
((ENABLE_WINBIND == 0)) || packages+=(libpam-winbind)
if [[ ${HARDENBOX_OFFLINE:-0} == 1 ]]; then
    log "Installation hors ligne des paquets Samba fournis par le CD-ROM"
    apt-get install -y --no-download --no-install-recommends \
        ca-certificates chrony bind9-dnsutils nftables python3 "${packages[@]}"
else
    log "Installation des paquets Samba Debian"
    apt-get update
    apt-get install -y --no-install-recommends \
        ca-certificates chrony bind9-dnsutils nftables python3 "${packages[@]}"
fi

if ((ENABLE_OPENRSAT)); then
    [[ $(dpkg --print-architecture) == "amd64" ]] || \
        die "L'installation OpenRSAT automatisee prend actuellement en charge Debian amd64 uniquement."
    log "Verification d'OpenRSAT fourni par le CD-ROM"
    apt-get install -y --no-download libgtk2.0-0t64 gtk2-engines-murrine openrsat
    [[ -x /opt/openrsat/OpenRSAT ]] || die "Executable OpenRSAT introuvable apres installation."
    ln -sfn /opt/openrsat/OpenRSAT /usr/local/bin/openrsat
    ln -sfn /opt/openrsat/OpenRSAT /usr/local/bin/OpenRSAT
    command -v update-desktop-database >/dev/null 2>&1 && \
        update-desktop-database /usr/share/applications || true
fi

if ((ENABLE_POWERSHELL)); then
    log "Verification de PowerShell Core fourni par le CD-ROM"
    apt-get install -y --no-download powershell
    pwsh --noninteractive --nologo --command '$PSVersionTable.PSVersion.ToString()'
fi

if ((ENABLE_WINBIND)); then
    sed -i -E '/^passwd:/ { /(^|[[:space:]])winbind([[:space:]]|$)/! s/[[:space:]]*$/ winbind/; }' /etc/nsswitch.conf
    sed -i -E '/^group:/ { /(^|[[:space:]])winbind([[:space:]]|$)/! s/[[:space:]]*$/ winbind/; }' /etc/nsswitch.conf
fi

log "Arret des services Samba incompatibles"
if [[ ${HARDENBOX_INSTALLER:-0} == 1 ]]; then
    systemctl disable samba winbind nmbd smbd 2>/dev/null || true
else
    systemctl disable --now samba winbind nmbd smbd 2>/dev/null || true
fi
systemctl mask winbind nmbd smbd
systemctl unmask samba samba-ad-dc
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    systemctl daemon-reload
fi

if ((RESUME == 0)); then
    partial_state=0
    for path in /etc/samba/smb.conf /var/lib/samba/private/secrets.tdb \
        /var/lib/samba/private/passdb.tdb /var/lib/samba/sysvol; do
        [[ ! -e $path ]] || partial_state=1
    done
    if ((partial_state)); then
        ((CLEANUP_PARTIAL)) || die \
            "Un etat Samba existant doit etre sauvegarde avec --cleanup-partial avant la promotion."
        log "Sauvegarde et nettoyage de l'etat Samba existant"
        for path in /etc/samba /var/lib/samba /var/cache/samba; do
            backup_once "$path"
        done
        rm -rf /etc/samba /var/lib/samba /var/cache/samba /run/samba
    fi

    if [[ -s /etc/samba/smb.conf ]]; then
        rm -f /etc/samba/smb.conf
    fi

    log "Preparation de l'arborescence Samba"
    install -d -o root -g root -m 0755 \
        /etc/samba /var/lib/samba /var/cache/samba /run/samba

    if [[ $DEPLOYMENT == "forest" ]]; then
        log "Provisionnement de la foret $REALM"
        provision_options=(
            --realm="$REALM"
            --domain="$DOMAIN"
            --host-name="$DC_HOSTNAME"
            --host-ip="$SERVER_IP"
            --server-role=dc
            --dns-backend=SAMBA_INTERNAL
            --function-level="$FUNCTION_LEVEL"
            --use-rfc2307
            --adminpass="$ADMIN_PASSWORD"
        )
        ((ENABLE_MDB == 0)) || provision_options+=(--backend-store=mdb)
        if [[ $FUNCTION_LEVEL == "2016" ]]; then
            provision_options+=(--option="ad dc functional level = 2016")
        fi
        samba-tool domain provision "${provision_options[@]}"
    else
        log "Ajout de $FQDN comme controleur du domaine $REALM"
        samba-tool domain join "$REALM" DC \
            --server="$DOMAIN_DNS" \
            --username="$DOMAIN_ADMIN" \
            --password="$ADMIN_PASSWORD" \
            --dns-backend=SAMBA_INTERNAL
        joined_domain=$(testparm -s --parameter-name='workgroup' 2>/dev/null | tr '[:lower:]' '[:upper:]')
        [[ -n $joined_domain ]] || die "Impossible de lire le domaine NetBIOS apres la jointure."
        DOMAIN=$joined_domain
    fi
fi

set_smb_option global "dns forwarder" "$DNS_FORWARDER"
if [[ $DEPLOYMENT == "forest" && $FUNCTION_LEVEL == "2016" ]]; then
    set_smb_option global "ad dc functional level" "2016"
fi
if ((ENABLE_PASSWORD_CHECK)); then
    hibp_python_value=False
    ((ENABLE_HIBP == 0)) || hibp_python_value=True
    install_content /usr/local/sbin/samba-password-check 0755 <<EOF
#!/usr/bin/python3
import hashlib
import sys
import urllib.error
import urllib.request

HIBP_ENABLED = $hibp_python_value
FORBIDDEN_TERMS = ("harden", "samba", "admin", "password")


def reject(message):
    print(message, file=sys.stderr)
    raise SystemExit(1)


password = sys.stdin.readline().rstrip("\r\n")
if len(password) < 15:
    reject("Le mot de passe doit contenir au moins 15 caracteres.")
if not all((any(char.isupper() for char in password),
            any(char.islower() for char in password),
            any(char.isdigit() for char in password),
            any(not char.isalnum() for char in password))):
    reject("Le mot de passe doit contenir majuscule, minuscule, chiffre et symbole.")
if any(term in password.casefold() for term in FORBIDDEN_TERMS):
    reject("Le mot de passe contient un terme interdit.")

if HIBP_ENABLED:
    digest = hashlib.sha1(password.encode("utf-8"), usedforsecurity=False).hexdigest().upper()
    request = urllib.request.Request(
        "https://api.pwnedpasswords.com/range/" + digest[:5],
        headers={"Add-Padding": "true", "User-Agent": "HardenBox-Samba-Password-Check"},
    )
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            compromised = any(
                line.partition(":")[0] == digest[5:]
                for line in response.read().decode("ascii").splitlines()
            )
    except (OSError, urllib.error.URLError):
        reject("Verification HIBP indisponible; changement refuse par securite.")
    if compromised:
        reject("Ce mot de passe figure dans une fuite de donnees connue.")
EOF
    set_smb_option global "check password script" "/usr/local/sbin/samba-password-check"
fi
if ((ENABLE_SMB_HARDENING)); then
    set_smb_option global "restrict anonymous" "2"
    set_smb_option global "disable netbios" "yes"
    set_smb_option global "smb ports" "445"
    set_smb_option global "printcap name" "/dev/null"
    set_smb_option global "load printers" "no"
    set_smb_option global "disable spoolss" "yes"
    set_smb_option global "printing" "bsd"
    set_smb_option global "ntlm auth" "mschapv2-and-ntlmv2-only"
    set_smb_option global "server min protocol" "SMB2"
fi
if ((ENABLE_WINBIND)); then
    set_smb_option global "idmap_ldb:use rfc2307" "yes"
    set_smb_option global "winbind enum users" "yes"
    set_smb_option global "winbind enum groups" "yes"
    set_smb_option global "winbind nested groups" "yes"
fi

if ((ENABLE_TRUSTED_LDAPS)); then
    install -o root -g root -m 0600 "$TLS_KEY_SOURCE" /var/lib/samba/private/tls/key.pem
    install -o root -g root -m 0644 "$TLS_CERT_SOURCE" /var/lib/samba/private/tls/cert.pem
    install -o root -g root -m 0644 "$TLS_CA_SOURCE" /var/lib/samba/private/tls/ca.pem
    set_smb_option global "tls enabled" "yes"
    set_smb_option global "tls keyfile" "/var/lib/samba/private/tls/key.pem"
    set_smb_option global "tls certfile" "/var/lib/samba/private/tls/cert.pem"
    set_smb_option global "tls cafile" "/var/lib/samba/private/tls/ca.pem"
fi
if ((ENABLE_AUDIT)); then
    set_smb_option global "log level" "1 auth_json_audit:3@/var/log/samba/auth_json_audit.log dsdb_json_audit:5@/var/log/samba/dsdb_json_audit.log dsdb_password_json_audit:9@/var/log/samba/dsdb_password_json_audit.log dsdb_group_json_audit:9@/var/log/samba/dsdb_group_json_audit.log kerberos:3@/var/log/samba/kerberos.log dns:0"
    for audit_share in netlogon sysvol; do
        set_smb_option "$audit_share" "vfs objects" "dfs_samba4 acl_xattr full_audit"
        set_smb_option "$audit_share" "full_audit:failure" "none"
        set_smb_option "$audit_share" "full_audit:success" "pwrite write renameat"
        set_smb_option "$audit_share" "full_audit:prefix" "IP=%I|USER=%u|MACHINE=%m|VOLUME=%S"
        set_smb_option "$audit_share" "full_audit:facility" "local7"
        set_smb_option "$audit_share" "full_audit:priority" "NOTICE"
    done
fi

log "Configuration de Kerberos et du DNS local"
install_content /etc/krb5.conf 0644 <<EOF
[libdefaults]
    default_realm = $REALM
    dns_lookup_kdc = true
    dns_lookup_realm = false
EOF
rm -f /var/lib/samba/private/krb5.conf
install -o root -g root -m 0644 /etc/krb5.conf /var/lib/samba/private/krb5.conf

if systemctl list-unit-files systemd-resolved.service >/dev/null 2>&1; then
    systemctl disable --now systemd-resolved.service || true
fi
install_content /etc/NetworkManager/conf.d/91-samba-ad-dns.conf 0644 <<'EOF'
[main]
dns=none
EOF
if command -v nmcli >/dev/null 2>&1 && systemctl is-active --quiet NetworkManager; then
    nmcli general reload
fi
rm -f /etc/resolv.conf
install_content /etc/resolv.conf 0644 <<EOF
search ${REALM,,}
nameserver 127.0.0.1
nameserver $DNS_FORWARDER
EOF

log "Demarrage du controleur de domaine"
systemctl unmask samba samba-ad-dc
systemctl enable samba-ad-dc
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    systemctl daemon-reload
    systemctl start samba-ad-dc
fi

log "Configuration de Chrony pour le temps signe Active Directory"
grep -qF 'ntpsigndsocket /var/lib/samba/ntp_signd' /etc/chrony/chrony.conf || \
    printf '\nallow %s\nntpsigndsocket /var/lib/samba/ntp_signd\n' "$ALLOWED_CIDR" >>/etc/chrony/chrony.conf
chown root:_chrony /var/lib/samba/ntp_signd
chmod 0750 /var/lib/samba/ntp_signd
systemctl enable chrony
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    systemctl restart chrony
fi

log "Ajout des regles nftables Samba AD"
if [[ ${HARDENBOX_INSTALLER:-0} == 1 ]]; then
    log "Regles nftables conservees pour application au premier demarrage."
elif command -v nft >/dev/null 2>&1 && nft list chain inet filter input >/dev/null 2>&1; then
    if ! nft -a list chain inet filter input | grep -q 'hardenbox-samba-ad-tcp'; then
        nft insert rule inet filter input ip saddr "$ALLOWED_CIDR" tcp dport \
            '{ 53, 88, 135, 139, 389, 445, 464, 636, 3268, 3269, 49152-65535 }' \
            ct state new accept comment 'hardenbox-samba-ad-tcp'
        nft insert rule inet filter input ip saddr "$ALLOWED_CIDR" udp dport \
            '{ 53, 88, 123, 137, 138, 389, 464 }' \
            accept comment 'hardenbox-samba-ad-udp'
        ruleset_file=$(mktemp)
        printf 'flush ruleset\n\n' >"$ruleset_file"
        nft list ruleset >>"$ruleset_file"
        install -o root -g root -m 0644 "$ruleset_file" /etc/nftables.conf
        rm -f "$ruleset_file"
    fi
    nft --check --file /etc/nftables.conf
else
    log "AVERTISSEMENT: chaine nftables 'inet filter input' absente; configurez le pare-feu manuellement."
fi

log "Application de la politique de mot de passe"
if [[ $DEPLOYMENT == "forest" ]]; then
    samba-tool domain passwordsettings set \
        --complexity=on --history-length=24 --min-pwd-length=14 --max-pwd-age=360
fi

log "Validation de Samba AD, DNS, Kerberos et SYSVOL"
testparm --suppress-prompt >/dev/null
if [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    systemctl is-active --quiet samba-ad-dc
    samba-tool domain info 127.0.0.1
    host -t A "$FQDN" 127.0.0.1
    host -t SRV "_ldap._tcp.${REALM,,}" 127.0.0.1
    host -t SRV "_kerberos._udp.${REALM,,}" 127.0.0.1
    CREDENTIAL_DIR=$(mktemp -d)
    credential_cache="$CREDENTIAL_DIR/krb5cc"
    kerberos_cache="FILE:$credential_cache"
    kerberos_user=${DOMAIN_ADMIN##*\\}
    if [[ $kerberos_user == *@* ]]; then
        kerberos_principal=$kerberos_user
    else
        kerberos_principal="$kerberos_user@$REALM"
    fi
    KRB5CCNAME="$kerberos_cache" kinit "$kerberos_principal" <<<"$ADMIN_PASSWORD"
    KRB5CCNAME="$kerberos_cache" klist
    KRB5CCNAME="$kerberos_cache" smbclient -L "$FQDN" --use-kerberos=required </dev/null \
        | grep -Eq 'sysvol|netlogon'
fi

if ((ENABLE_ONLINE_BACKUP)) && [[ ${HARDENBOX_INSTALLER:-0} != 1 ]]; then
    log "Creation d'une sauvegarde en ligne de l'annuaire"
    ad_backup_dir="/var/backups/samba-ad"
    install -d -o root -g root -m 0700 "$ad_backup_dir"
    KRB5CCNAME="$kerberos_cache" samba-tool domain backup online \
        --server="$FQDN" --targetdir="$ad_backup_dir" --use-kerberos=required
fi
KRB5CCNAME="$kerberos_cache" kdestroy
rm -rf -- "$CREDENTIAL_DIR"
CREDENTIAL_DIR=""
ADMIN_PASSWORD=""
unset SAMBA_ADMIN_PASSWORD

log "Provisionnement termine avec succes."
log "Sauvegarde des fichiers d'origine: $BACKUP_DIR"
((ENABLE_ONLINE_BACKUP == 0)) || log "Sauvegarde en ligne de l'annuaire: $ad_backup_dir"
log "Configurez maintenant les clients avec $SERVER_IP comme serveur DNS, puis testez \\\\${REALM,,}\\sysvol."
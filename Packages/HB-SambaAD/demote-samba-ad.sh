#!/usr/bin/env bash

# Retrograde ce controleur de domaine Samba AD en serveur autonome (#14) :
# transfert de tous les roles FSMO vers un autre controleur, demotion, puis
# arret de Samba AD et retrait de sa configuration (sauvegardee avant).

set -Eeuo pipefail
IFS=$'\n\t'
umask 077

TARGET_DC=""
DOMAIN_ADMIN="Administrator"
PASSWORD_STDIN=0
ASSUME_YES=0
ADMIN_PASSWORD=""
CREDENTIAL_DIR=""

usage() {
    cat <<'EOF'
Usage: demote-samba-ad.sh --target-dc FQDN [--domain-admin COMPTE] --password-stdin --yes

  --target-dc FQDN        Controleur qui recoit les roles FSMO et sert de reference
  --domain-admin COMPTE   Compte d'administration du domaine (defaut: Administrator)
  --password-stdin        Lit le mot de passe du compte sur l'entree standard
  --yes                   Confirme l'operation, qui est irreversible
EOF
}

log() {
    printf '[%s] %s\n' "$(date '+%Y-%m-%d %H:%M:%S')" "$*"
}

die() {
    printf 'ERREUR: %s\n' "$*" >&2
    exit 1
}

cleanup() {
    ADMIN_PASSWORD=""
    [[ -z $CREDENTIAL_DIR ]] || rm -rf -- "$CREDENTIAL_DIR"
}
trap cleanup EXIT
trap 'printf "ERREUR: statut %s a la ligne %s: %s\n" "$?" "$LINENO" "$BASH_COMMAND" >&2' ERR

while (($#)); do
    case $1 in
        --target-dc) TARGET_DC=${2-}; shift 2 ;;
        --domain-admin) DOMAIN_ADMIN=${2-}; shift 2 ;;
        --password-stdin) PASSWORD_STDIN=1; shift ;;
        --yes) ASSUME_YES=1; shift ;;
        -h|--help) usage; exit 0 ;;
        *) usage >&2; die "Option inconnue: $1" ;;
    esac
done

[[ $EUID -eq 0 ]] || die "Ce script doit etre execute en root."
install -d -o root -g root -m 0700 /var/log/hardenbox
LOG_FILE=/var/log/hardenbox/demote-samba-ad.log
exec > >(tee -a "$LOG_FILE") 2> >(tee -a "$LOG_FILE" >&2)
log "Journal complet: $LOG_FILE"

((ASSUME_YES)) || die "La retrogradation est irreversible : confirmez avec --yes."
[[ -n $TARGET_DC ]] || die "Indiquez le controleur qui recoit les roles avec --target-dc."
((PASSWORD_STDIN)) && IFS= read -r ADMIN_PASSWORD || true
[[ -n $ADMIN_PASSWORD ]] || die "Mot de passe du compte du domaine absent (--password-stdin)."

[[ -e /var/lib/samba/private/sam.ldb ]] || die "Ce serveur n'est pas un controleur de domaine Samba AD."
systemctl is-active --quiet samba-ad-dc || die "Le service samba-ad-dc doit etre actif pour la retrogradation."

REALM=$(testparm -s --parameter-name='realm' 2>/dev/null | tr '[:lower:]' '[:upper:]')
[[ -n $REALM ]] || die "Impossible de lire le domaine de ce controleur."
LOCAL_FQDN=$(hostname -f 2>/dev/null || hostname)
TARGET_DC=${TARGET_DC%.}
[[ ${TARGET_DC,,} != "${LOCAL_FQDN,,}" ]] || die "Le controleur cible doit etre un autre controleur que celui-ci."

# Le controleur cible doit etre un DC annonce du domaine.
mapfile -t domain_dcs < <(host -W 3 -t SRV "_ldap._tcp.dc._msdcs.${REALM,,}" 127.0.0.1 2>/dev/null \
    | awk '/has SRV record/ {sub(/\.$/, "", $NF); print tolower($NF)}' | sort -u)
other_dcs=0
for dc in "${domain_dcs[@]}"; do
    [[ $dc == "${LOCAL_FQDN,,}" ]] || other_dcs=$((other_dcs + 1))
done
((other_dcs > 0)) || die "Ce controleur est le dernier du domaine ${REALM,,} : il ne peut pas etre retrograde."
printf '%s\n' "${domain_dcs[@]}" | grep -Fxq "${TARGET_DC,,}" || \
    die "$TARGET_DC n'est pas un controleur annonce du domaine ${REALM,,}."
TARGET_IP=$(host -W 3 -t A "$TARGET_DC" 127.0.0.1 2>/dev/null | awk '/has address/ {print $NF; exit}')
[[ -n $TARGET_IP ]] || die "Adresse de $TARGET_DC introuvable."

log "Authentification Kerberos du compte $DOMAIN_ADMIN"
CREDENTIAL_DIR=$(mktemp -d)
ccache="FILE:$CREDENTIAL_DIR/krb5cc"
admin_user=${DOMAIN_ADMIN##*\\}
[[ $admin_user == *@* ]] || admin_user="$admin_user@$REALM"
KRB5CCNAME="$ccache" kinit "$admin_user" <<<"$ADMIN_PASSWORD" >/dev/null || \
    die "Authentification de $admin_user refusee."
ADMIN_PASSWORD=""

log "Transfert des roles FSMO vers $TARGET_DC"
samba-tool fsmo transfer --role=all -H "ldap://$TARGET_DC" \
    --use-kerberos=required --use-krb5-ccache="$ccache"
samba-tool fsmo show -H "ldap://$TARGET_DC" --use-kerberos=required --use-krb5-ccache="$ccache"

log "Retrogradation de $LOCAL_FQDN"
samba-tool domain demote --server="$TARGET_DC" \
    --use-kerberos=required --use-krb5-ccache="$ccache"

log "Arret de Samba AD et des services associes"
systemctl disable --now samba-adws.service 2>/dev/null || true
systemctl disable --now samba-ad-dc.service
systemctl mask samba-ad-dc.service

backup_dir="/var/backups/hardenbox-demote-$(date '+%Y%m%d-%H%M%S')"
log "Sauvegarde puis retrait de la configuration Samba AD dans $backup_dir"
install -d -o root -g root -m 0700 "$backup_dir"
for path in /etc/samba /var/lib/samba /var/cache/samba /etc/krb5.conf /etc/resolv.conf; do
    [[ -e $path ]] || continue
    install -d -m 0700 "$backup_dir$(dirname "$path")"
    cp -a -- "$path" "$backup_dir$path"
done
rm -rf -- /var/lib/samba/private /var/lib/samba/sysvol /var/cache/samba
rm -f -- /etc/samba/smb.conf /etc/krb5.keytab

log "DNS du serveur : $TARGET_DC ($TARGET_IP)"
rm -f /etc/resolv.conf
printf 'search %s\nnameserver %s\n' "${REALM,,}" "$TARGET_IP" >/etc/resolv.conf
chmod 0644 /etc/resolv.conf
printf '[libdefaults]\n    default_realm = %s\n    dns_lookup_kdc = true\n    dns_lookup_realm = false\n' \
    "$REALM" >/etc/krb5.conf
chmod 0644 /etc/krb5.conf

if [[ -f /etc/chrony/chrony.conf ]]; then
    sed -i -e '/^ntpsigndsocket \/var\/lib\/samba\/ntp_signd$/d' -e '/^allow$/d' /etc/chrony/chrony.conf
    systemctl restart chrony 2>/dev/null || true
fi

if [[ -x /usr/sbin/secop-harden && -f /etc/secop/hardening.conf ]]; then
    log "Durcissement secop-hardening : role serveur generique"
    sed -i 's/^SECOP_ROLE=.*/SECOP_ROLE="generic"/' /etc/secop/hardening.conf
    (umask 022; /usr/sbin/secop-harden apply) || \
        log "AVERTISSEMENT: secop-harden apply a echoue; relancez: secop-harden apply"
fi

rm -f -- /var/lib/hardenbox/hb-sambaad.completed
log "Retrogradation terminee : ce serveur est autonome. Sauvegarde : $backup_dir"
log "Redemarrez le serveur."

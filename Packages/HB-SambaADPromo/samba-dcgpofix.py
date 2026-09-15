#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
samba-dcgpofix — équivalent de dcgpofix.exe pour Samba AD DC.

Recrée (ou remet à l'état par défaut) les deux GPO par défaut d'un domaine :
  - Default Domain Policy              {31B2F340-016D-11D2-945F-00C04FB984F9}
  - Default Domain Controllers Policy  {6AC1786C-016F-11D2-945F-00C04FB984F9}

Pour chaque GPO ciblée, l'outil :
  1. sauvegarde l'existant (LDIF de l'objet + tar.gz du dossier SYSVOL) ;
  2. recrée / normalise l'objet groupPolicyContainer et ses conteneurs
     CN=Machine / CN=User dans CN=Policies,CN=System ;
  3. remet le nTSecurityDescriptor de l'objet à la valeur par défaut du schéma
     (comportement de dcgpofix), sauf --keep-ds-acl ;
  4. remplace intégralement le dossier SYSVOL Policies\{GUID} :
       - profil "windows" (défaut) : GptTmpl.inf avec les valeurs par défaut Windows
       - profil "samba"   : structure vide, identique à un provision Samba
                            (dcgpofix / defltdc.inf : mots de passe, Kerberos,
                            User Rights Assignment, options de sécurité) ;
  5. incrémente versionNumber (AD) et GPT.INI de façon cohérente pour forcer
     la réapplication par les clients ;
  6. ré-applique les NT ACL SYSVOL du dossier à partir du descripteur AD
     (même logique que `samba-tool ntacl sysvolreset`, limitée à la GPO) ;
  7. s'assure que le gPLink existe sur la racine du domaine (DDP) et sur
     OU=Domain Controllers (DDCP).

À exécuter en root, sur un DC Samba, avec accès local à sam.ldb et SYSVOL.
Testé sur Samba 4.19 ; utilise uniquement des API Python stables de Samba
(samba.samdb, samba.ntacls, samba.dcerpc.security).

ATTENTION : outil de dernier recours, comme dcgpofix. Tous les paramètres
personnalisés des deux GPO sont perdus (ils restent dans la sauvegarde).
Après exécution, répliquez SYSVOL vers les autres DC (rsync/osync/...) puis
lancez `samba-gpupdate --force` sur chaque DC.
"""

import argparse
import os
import re
import shutil
import subprocess
import sys
import tarfile
import time
from datetime import datetime

try:
    import ldb
    from samba import param, ntacls
    from samba.auth import system_session
    try:
        from samba.auth_util import system_session_unix
    except ImportError:  # Samba < 4.16
        from samba.auth import system_session_unix
    from samba.dcerpc import security
    from samba.ndr import ndr_pack, ndr_unpack
    from samba.samdb import SamDB
    from samba.ntacls import dsacl2fsacl
    from samba.samba3 import param as s3param
    from samba.samba3 import passdb
except ImportError as e:  # pragma: no cover
    sys.stderr.write("Bindings Python Samba introuvables (%s). "
                     "Installez python3-samba.\n" % e)
    sys.exit(2)

# ---------------------------------------------------------------------------
# Constantes : identiques à source4/setup/provision_group_policy.ldif
# ---------------------------------------------------------------------------

DDP_GUID = "{31B2F340-016D-11D2-945F-00C04FB984F9}"
DDCP_GUID = "{6AC1786C-016F-11D2-945F-00C04FB984F9}"

# CSE GUIDs (Client-Side Extensions)
CSE_SECURITY = "[{827D319E-6EAC-11D2-A4EA-00C04F79F83A}{803E14A0-B4FB-11D0-A0D0-00A0C90F574B}]"
CSE_REGISTRY_ADMX = "[{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{53D6AB1B-2488-11D1-A28C-00C04FB94F17}]"
CSE_EFS = "[{B1BE8D72-6EAC-11D2-A4EA-00C04F79F83A}{53D6AB1B-2488-11D1-A28C-00C04FB94F17}]"
CSE_USER_IE = "[{3060E8D0-7020-11D2-842D-00C04FA372D4}{3060E8CE-7020-11D2-842D-00C04FA372D4}]"
CSE_USER_REG = "[{35378EAC-683F-11D2-A89A-00C04FBBCFA2}{0F6B957E-509E-11D1-A7CC-0000F87571E3}]"

DEFAULT_GPOS = {
    "domain": {
        "guid": DDP_GUID,
        "displayName": "Default Domain Policy",
        "gPCMachineExtensionNames": CSE_REGISTRY_ADMX + CSE_SECURITY + CSE_EFS,
        "gPCUserExtensionNames": CSE_USER_IE + CSE_USER_REG,
        "link_target": "{domaindn}",
    },
    "dc": {
        "guid": DDCP_GUID,
        "displayName": "Default Domain Controllers Policy",
        "gPCMachineExtensionNames": CSE_SECURITY,
        "gPCUserExtensionNames": None,
        "link_target": "OU=Domain Controllers,{domaindn}",
    },
}

# systemFlags = FLAG_DISALLOW_DELETE | FLAG_DOMAIN_DISALLOW_RENAME
#             | FLAG_DOMAIN_DISALLOW_MOVE  (0x8C000000 signé)
SYSTEM_FLAGS = "-1946157056"

# ---------------------------------------------------------------------------
# Contenu GptTmpl.inf — profil "windows"
# Source : valeurs par défaut de dcgpofix / defltdc.inf (Windows Server 2016+),
# uniquement des SID bien connus (aucun SID spécifique au domaine).
# ---------------------------------------------------------------------------

GPTTMPL_DDP_WINDOWS = """[Unicode]
Unicode=yes
[System Access]
MinimumPasswordAge = 0
MaximumPasswordAge = 42
MinimumPasswordLength = 12
PasswordComplexity = 1
PasswordHistorySize = 24
LockoutBadCount = 0
RequireLogonToChangePassword = 0
ForceLogoffWhenHourExpire = 0
ClearTextPassword = 0
LSAAnonymousNameLookup = 0
[Kerberos Policy]
MaxTicketAge = 10
MaxRenewAge = 7
MaxServiceAge = 600
MaxClockSkew = 5
TicketValidateClient = 1
[Registry Values]
MACHINE\\System\\CurrentControlSet\\Control\\Lsa\\NoLMHash=4,1
[Version]
signature="$CHICAGO$"
Revision=1
"""

GPTTMPL_DDCP_WINDOWS = """[Unicode]
Unicode=yes
[Registry Values]
MACHINE\\System\\CurrentControlSet\\Services\\NTDS\\Parameters\\LDAPServerIntegrity=4,1
MACHINE\\System\\CurrentControlSet\\Services\\Netlogon\\Parameters\\RequireSignOrSeal=4,1
MACHINE\\System\\CurrentControlSet\\Services\\LanManServer\\Parameters\\RequireSecuritySignature=4,1
MACHINE\\System\\CurrentControlSet\\Services\\LanManServer\\Parameters\\EnableSecuritySignature=4,1
[Privilege Rights]
SeAssignPrimaryTokenPrivilege = *S-1-5-20,*S-1-5-19
SeAuditPrivilege = *S-1-5-20,*S-1-5-19
SeBackupPrivilege = *S-1-5-32-549,*S-1-5-32-551,*S-1-5-32-544
SeBatchLogonRight = *S-1-5-32-559,*S-1-5-32-551,*S-1-5-32-544
SeChangeNotifyPrivilege = *S-1-5-32-554,*S-1-5-11,*S-1-5-32-544,*S-1-5-20,*S-1-5-19,*S-1-1-0
SeCreatePagefilePrivilege = *S-1-5-32-544
SeDebugPrivilege = *S-1-5-32-544
SeIncreaseBasePriorityPrivilege = *S-1-5-32-544
SeIncreaseQuotaPrivilege = *S-1-5-32-544,*S-1-5-20,*S-1-5-19
SeInteractiveLogonRight = *S-1-5-32-549,*S-1-5-32-550,*S-1-5-9,*S-1-5-32-551,*S-1-5-32-544,*S-1-5-32-548
SeLoadDriverPrivilege = *S-1-5-32-550,*S-1-5-32-544
SeMachineAccountPrivilege = *S-1-5-11
SeNetworkLogonRight = *S-1-5-32-554,*S-1-5-9,*S-1-5-11,*S-1-5-32-544,*S-1-1-0
SeProfileSingleProcessPrivilege = *S-1-5-32-544
SeRemoteShutdownPrivilege = *S-1-5-32-549,*S-1-5-32-544
SeRestorePrivilege = *S-1-5-32-549,*S-1-5-32-551,*S-1-5-32-544
SeSecurityPrivilege = *S-1-5-32-544
SeShutdownPrivilege = *S-1-5-32-550,*S-1-5-32-549,*S-1-5-32-551,*S-1-5-32-544
SeSystemEnvironmentPrivilege = *S-1-5-32-544
SeSystemProfilePrivilege = *S-1-5-80-3139157870-2983391045-3678747466-658725712-1809340420,*S-1-5-32-544
SeSystemTimePrivilege = *S-1-5-32-549,*S-1-5-32-544,*S-1-5-19
SeTakeOwnershipPrivilege = *S-1-5-32-544
SeUndockPrivilege = *S-1-5-32-544
SeEnableDelegationPrivilege = *S-1-5-32-544
SeRemoteInteractiveLogonRight = *S-1-5-32-544
[Version]
signature="$CHICAGO$"
Revision=1
"""

SECEDIT_REL = os.path.join("MACHINE", "Microsoft", "Windows NT", "SecEdit")


# ---------------------------------------------------------------------------
# Utilitaires
# ---------------------------------------------------------------------------

class Log:
    def __init__(self, quiet=False):
        self.quiet = quiet

    def info(self, msg):
        if not self.quiet:
            print("  " + msg)

    def head(self, msg):
        print("\n== " + msg)

    def warn(self, msg):
        print("  [!] " + msg, file=sys.stderr)


def die(msg, rc=1):
    print("ERREUR : " + msg, file=sys.stderr)
    sys.exit(rc)


def ldb_val(msg, attr, default=None):
    if attr in msg:
        return str(msg[attr][0])
    return default


def gpt_ini_version(path):
    """Lit Version= dans GPT.INI (0 si absent/illisible)."""
    try:
        with open(path, "rb") as f:
            data = f.read()
        try:
            text = data.decode("utf-16") if data.startswith(b"\xff\xfe") else data.decode("utf-8", "replace")
        except UnicodeDecodeError:
            text = data.decode("latin-1")
        m = re.search(r"^\s*Version\s*=\s*(\d+)", text, re.M | re.I)
        return int(m.group(1)) if m else 0
    except OSError:
        return 0


def next_version(old, bump_user, bump_machine):
    """versionNumber = (user << 16) | machine. Incrémente les moitiés touchées."""
    user = (old >> 16) & 0xFFFF
    machine = old & 0xFFFF
    if bump_machine:
        machine = (machine + 1) & 0xFFFF
    if bump_user:
        user = (user + 1) & 0xFFFF
    return (user << 16) | machine


# ---------------------------------------------------------------------------
# Cœur
# ---------------------------------------------------------------------------

class DcGpoFix:
    def __init__(self, args, log):
        self.args = args
        self.log = log
        self.lp = param.LoadParm()
        if args.configfile:
            self.lp.load(args.configfile)
        else:
            self.lp.load_default()

        try:
            self.samdb = SamDB(url=args.url, session_info=system_session(), lp=self.lp)
        except Exception as e:
            die("impossible d'ouvrir sam.ldb : %s" % e)

        self.domaindn = str(self.samdb.domain_dn())
        self.domain_sid = security.dom_sid(self.samdb.get_domain_sid())
        self.dnsdomain = self.lp.get("realm").lower()
        self.sysvol = args.sysvol or self.lp.get("path", "sysvol")
        if not self.sysvol or not os.path.isdir(self.sysvol):
            die("chemin SYSVOL introuvable (%r). Utilisez --sysvol." % self.sysvol)
        self.policies_dir = os.path.join(self.sysvol, self.dnsdomain, "Policies")
        self.policies_dn = "CN=Policies,CN=System,%s" % self.domaindn

        self.backup_dir = os.path.join(
            args.backup_dir, datetime.now().strftime("%Y%m%d-%H%M%S"))

        # Contexte s3 pour setntacl (comme samba-tool ntacl sysvolreset)
        self._passdb = None
        self._use_ntvfs = "smb" in self.lp.get("server services")

    # ----- sauvegarde ------------------------------------------------------

    def backup(self, name, guid):
        os.makedirs(self.backup_dir, exist_ok=True)
        gpo_dn = "CN=%s,%s" % (guid, self.policies_dn)
        # LDIF de l'objet + enfants
        try:
            res = self.samdb.search(base=gpo_dn, scope=ldb.SCOPE_SUBTREE,
                                    attrs=["*", "nTSecurityDescriptor"],
                                    controls=["sd_flags:1:15"])
            with open(os.path.join(self.backup_dir, "%s.ldif" % name), "w") as f:
                for m in res:
                    f.write(self.samdb.write_ldif(m, ldb.CHANGETYPE_NONE))
            self.log.info("LDIF sauvegardé (%d entrées)" % len(res))
        except ldb.LdbError as e:
            if e.args[0] == ldb.ERR_NO_SUCH_OBJECT:
                self.log.info("objet AD absent : rien à sauvegarder côté LDAP")
            else:
                raise
        # Liens
        for dn in (self.domaindn, "OU=Domain Controllers,%s" % self.domaindn):
            try:
                r = self.samdb.search(base=dn, scope=ldb.SCOPE_BASE, attrs=["gPLink", "gPOptions"])
                with open(os.path.join(self.backup_dir, "gplinks.ldif"), "a") as f:
                    f.write(self.samdb.write_ldif(r[0], ldb.CHANGETYPE_NONE))
            except ldb.LdbError:
                pass
        # SYSVOL
        src = os.path.join(self.policies_dir, guid)
        if os.path.isdir(src):
            tgz = os.path.join(self.backup_dir, "%s-sysvol.tar.gz" % name)
            with tarfile.open(tgz, "w:gz") as tar:
                tar.add(src, arcname=guid)
            # les NT ACL sont dans les xattr : on les dumpe aussi
            try:
                out = subprocess.run(["getfattr", "-R", "-d", "-m", "-", "--absolute-names", src],
                                     capture_output=True, text=True).stdout
                with open(os.path.join(self.backup_dir, "%s-xattr.txt" % name), "w") as f:
                    f.write(out)
            except FileNotFoundError:
                self.log.warn("getfattr absent : xattr (NT ACL) non sauvegardés")
            self.log.info("SYSVOL sauvegardé : %s" % tgz)
        else:
            self.log.info("dossier SYSVOL absent : rien à sauvegarder côté fichiers")

    # ----- LDAP ------------------------------------------------------------

    def ensure_gpo_object(self, spec):
        guid = spec["guid"]
        gpo_dn = "CN=%s,%s" % (guid, self.policies_dn)
        unc = "\\\\%s\\sysvol\\%s\\Policies\\%s" % (self.dnsdomain, self.dnsdomain, guid)
        exists = True
        old_version = 0
        try:
            res = self.samdb.search(base=gpo_dn, scope=ldb.SCOPE_BASE,
                                    attrs=["versionNumber", "displayName", "flags"])
            old_version = int(ldb_val(res[0], "versionNumber", "0"))
        except ldb.LdbError as e:
            if e.args[0] != ldb.ERR_NO_SUCH_OBJECT:
                raise
            exists = False

        if not exists:
            self.log.info("objet AD absent → création")
            if not self.args.dry_run:
                m = ldb.Message(ldb.Dn(self.samdb, gpo_dn))
                m["objectClass"] = ldb.MessageElement(
                    ["top", "container", "groupPolicyContainer"], ldb.FLAG_MOD_ADD, "objectClass")
                m["isCriticalSystemObject"] = "TRUE"
                m["systemFlags"] = SYSTEM_FLAGS
                self.samdb.add(m, controls=["relax:0"])
        else:
            self.log.info("objet AD présent (versionNumber=%d) → normalisation" % old_version)

        # Enfants CN=User / CN=Machine
        for child in ("User", "Machine"):
            cdn = "CN=%s,%s" % (child, gpo_dn)
            try:
                self.samdb.search(base=cdn, scope=ldb.SCOPE_BASE, attrs=["cn"])
            except ldb.LdbError as e:
                if e.args[0] != ldb.ERR_NO_SUCH_OBJECT:
                    raise
                self.log.info("conteneur CN=%s absent → création" % child)
                if not self.args.dry_run:
                    m = ldb.Message(ldb.Dn(self.samdb, cdn))
                    m["objectClass"] = ldb.MessageElement(["top", "container"], ldb.FLAG_MOD_ADD, "objectClass")
                    m["isCriticalSystemObject"] = "TRUE"
                    m["systemFlags"] = SYSTEM_FLAGS
                    self.samdb.add(m, controls=["relax:0"])

        # Attributs GPC
        new_version = next_version(old_version, bump_user=True, bump_machine=True) if exists else 0
        if self.args.profile == "samba" and not exists:
            new_version = 0
        m = ldb.Message(ldb.Dn(self.samdb, gpo_dn))
        m["displayName"] = ldb.MessageElement(spec["displayName"], ldb.FLAG_MOD_REPLACE, "displayName")
        m["gPCFileSysPath"] = ldb.MessageElement(unc, ldb.FLAG_MOD_REPLACE, "gPCFileSysPath")
        m["gPCFunctionalityVersion"] = ldb.MessageElement("2", ldb.FLAG_MOD_REPLACE, "gPCFunctionalityVersion")
        m["flags"] = ldb.MessageElement("0", ldb.FLAG_MOD_REPLACE, "flags")
        m["versionNumber"] = ldb.MessageElement(str(new_version), ldb.FLAG_MOD_REPLACE, "versionNumber")
        m["gPCMachineExtensionNames"] = ldb.MessageElement(
            spec["gPCMachineExtensionNames"], ldb.FLAG_MOD_REPLACE, "gPCMachineExtensionNames")
        if spec["gPCUserExtensionNames"]:
            m["gPCUserExtensionNames"] = ldb.MessageElement(
                spec["gPCUserExtensionNames"], ldb.FLAG_MOD_REPLACE, "gPCUserExtensionNames")
        else:
            m["gPCUserExtensionNames"] = ldb.MessageElement([], ldb.FLAG_MOD_DELETE, "gPCUserExtensionNames")
        # gPCWQLFilter : dcgpofix supprime tout filtre WMI
        m["gPCWQLFilter"] = ldb.MessageElement([], ldb.FLAG_MOD_DELETE, "gPCWQLFilter")
        self.log.info("attributs → displayName, gPCFileSysPath, flags=0, CSE, versionNumber=%d" % new_version)
        if not self.args.dry_run:
            self.samdb.modify(m, controls=["permissive_modify:0"])
        return gpo_dn, new_version

    def reset_ds_acl(self, gpo_dn):
        """Remet nTSecurityDescriptor à la valeur par défaut du schéma
        (groupPolicyContainer.defaultSecurityDescriptor), comme dcgpofix."""
        schema_dn = "CN=Group-Policy-Container,%s" % self.samdb.get_schema_basedn()
        res = self.samdb.search(base=schema_dn, scope=ldb.SCOPE_BASE, attrs=["defaultSecurityDescriptor"])
        sddl = ldb_val(res[0], "defaultSecurityDescriptor")
        if not sddl:
            self.log.warn("defaultSecurityDescriptor introuvable dans le schéma : ACL AD conservée")
            return
        # Owner/Group par défaut : Domain Admins (comme un objet créé par un admin)
        sddl = "O:DAG:DA" + sddl
        sd = security.descriptor.from_sddl(sddl, self.domain_sid)
        self.log.info("nTSecurityDescriptor → défaut du schéma (héritage recalculé par le module descriptor)")
        if self.args.dry_run:
            return
        m = ldb.Message(ldb.Dn(self.samdb, gpo_dn))
        m["nTSecurityDescriptor"] = ldb.MessageElement(
            ndr_pack(sd), ldb.FLAG_MOD_REPLACE, "nTSecurityDescriptor")
        # sd_flags = OWNER|GROUP|DACL (7) : on ne touche pas à la SACL
        self.samdb.modify(m, controls=["sd_flags:1:7"])

    def ensure_link(self, spec):
        target = spec["link_target"].format(domaindn=self.domaindn)
        gpo_dn = "CN=%s,%s" % (spec["guid"], self.policies_dn)
        try:
            res = self.samdb.search(base=target, scope=ldb.SCOPE_BASE, attrs=["gPLink"])
        except ldb.LdbError as e:
            self.log.warn("cible de lien %s introuvable (%s)" % (target, e))
            return
        current = ldb_val(res[0], "gPLink", "") or ""
        if gpo_dn.lower() in current.lower():
            self.log.info("gPLink déjà présent sur %s" % target)
            return
        # Ajout en fin de liste (= priorité la plus haute dans l'ordre d'application)
        new = "%s[LDAP://%s;0]" % (current, gpo_dn)
        self.log.info("gPLink absent sur %s → ajout" % target)
        if not self.args.dry_run:
            m = ldb.Message(ldb.Dn(self.samdb, target))
            m["gPLink"] = ldb.MessageElement(new, ldb.FLAG_MOD_REPLACE, "gPLink")
            self.samdb.modify(m)

    # ----- SYSVOL ----------------------------------------------------------

    def rebuild_sysvol(self, name, spec, version):
        guid = spec["guid"]
        path = os.path.join(self.policies_dir, guid)
        self.log.info("SYSVOL : remplacement intégral de %s" % path)
        if self.args.dry_run:
            return path
        if os.path.isdir(path):
            # déplacement plutôt que suppression : la sauvegarde tar existe déjà,
            # mais on garde une copie brute à côté jusqu'à la fin du run
            old = os.path.join(self.backup_dir, "%s-sysvol-raw" % name)
            shutil.move(path, old)
        os.makedirs(os.path.join(path, "MACHINE"), 0o775)
        os.makedirs(os.path.join(path, "USER"), 0o775)
        with open(os.path.join(path, "GPT.INI"), "w", newline="") as f:
            f.write("[General]\r\nVersion=%d\r\n" % version)
        if self.args.profile == "windows":
            secedit = os.path.join(path, SECEDIT_REL)
            os.makedirs(secedit, 0o775)
            content = GPTTMPL_DDP_WINDOWS if name == "domain" else GPTTMPL_DDCP_WINDOWS
            content = content.replace("\n", "\r\n")
            # Windows écrit GptTmpl.inf en UTF-16LE avec BOM
            with open(os.path.join(secedit, "GptTmpl.inf"), "wb") as f:
                f.write(b"\xff\xfe" + content.encode("utf-16-le"))
        return path

    def _get_passdb(self):
        if self._passdb is None:
            s3conf = s3param.get_context()
            s3conf.load(self.lp.configfile)
            s3conf.set("passdb backend", "samba_dsdb:%s" % self.samdb.url)
            self._passdb = passdb.PDB(s3conf.get("passdb backend"))
        return self._passdb

    def reset_fs_acl(self, gpo_dn, path):
        """NT ACL du dossier GPO dérivée du descripteur AD (dsacl2fsacl),
        exactement comme provision.set_gpos_acl / samba-tool ntacl sysvolreset."""
        res = self.samdb.search(base=gpo_dn, scope=ldb.SCOPE_BASE,
                                attrs=["nTSecurityDescriptor"], controls=["sd_flags:1:7"])
        ds_sd = ndr_unpack(security.descriptor, res[0]["nTSecurityDescriptor"][0]).as_sddl()
        fs_sddl = dsacl2fsacl(ds_sd, self.domain_sid)
        self.log.info("NT ACL SYSVOL → %s" % fs_sddl)
        if self.args.dry_run:
            return
        session = system_session_unix()
        pdb = self._get_passdb()
        targets = [path]
        for root, dirs, files in os.walk(path):
            targets += [os.path.join(root, d) for d in dirs]
            targets += [os.path.join(root, f) for f in files]
        for p in targets:
            ntacls.setntacl(self.lp, p, fs_sddl, str(self.domain_sid), session,
                            use_ntvfs=self._use_ntvfs, skip_invalid_chown=True,
                            passdb=pdb, service="sysvol")

    # ----- check -------------------------------------------------------------

    def check(self, name, spec):
        """Audit sans modification : retourne une liste d'écarts."""
        issues = []
        guid = spec["guid"]
        gpo_dn = "CN=%s,%s" % (guid, self.policies_dn)
        unc = "\\\\%s\\sysvol\\%s\\Policies\\%s" % (self.dnsdomain, self.dnsdomain, guid)
        try:
            res = self.samdb.search(base=gpo_dn, scope=ldb.SCOPE_BASE,
                                    attrs=["displayName", "gPCFileSysPath", "flags", "versionNumber",
                                           "gPCMachineExtensionNames", "gPCUserExtensionNames",
                                           "gPCFunctionalityVersion"])
            o = res[0]
            if ldb_val(o, "displayName") != spec["displayName"]:
                issues.append("displayName = %r" % ldb_val(o, "displayName"))
            if (ldb_val(o, "gPCFileSysPath") or "").lower() != unc.lower():
                issues.append("gPCFileSysPath = %r (attendu %r)" % (ldb_val(o, "gPCFileSysPath"), unc))
            if ldb_val(o, "flags", "0") != "0":
                issues.append("flags = %s (GPO désactivée partiellement/totalement)" % ldb_val(o, "flags"))
            if ldb_val(o, "gPCFunctionalityVersion") != "2":
                issues.append("gPCFunctionalityVersion = %r" % ldb_val(o, "gPCFunctionalityVersion"))
            if not ldb_val(o, "gPCMachineExtensionNames"):
                issues.append("gPCMachineExtensionNames absent (CSE sécurité non déclarée)")
            ad_ver = int(ldb_val(o, "versionNumber", "0"))
        except ldb.LdbError:
            issues.append("objet AD %s ABSENT" % gpo_dn)
            ad_ver = None
        for child in ("User", "Machine"):
            try:
                self.samdb.search(base="CN=%s,%s" % (child, gpo_dn), scope=ldb.SCOPE_BASE, attrs=["cn"])
            except ldb.LdbError:
                issues.append("conteneur CN=%s absent" % child)
        path = os.path.join(self.policies_dir, guid)
        if not os.path.isdir(path):
            issues.append("dossier SYSVOL %s ABSENT" % path)
        else:
            for sub in ("MACHINE", "USER"):
                if not any(d.lower() == sub.lower() for d in os.listdir(path)):
                    issues.append("sous-dossier %s absent" % sub)
            gpt = os.path.join(path, "GPT.INI")
            if not os.path.isfile(gpt):
                issues.append("GPT.INI absent")
            elif ad_ver is not None and gpt_ini_version(gpt) != ad_ver:
                issues.append("version GPT.INI (%d) ≠ versionNumber AD (%d)" % (gpt_ini_version(gpt), ad_ver))
        target = spec["link_target"].format(domaindn=self.domaindn)
        try:
            r = self.samdb.search(base=target, scope=ldb.SCOPE_BASE, attrs=["gPLink"])
            if gpo_dn.lower() not in (ldb_val(r[0], "gPLink", "") or "").lower():
                issues.append("gPLink absent sur %s" % target)
        except ldb.LdbError:
            issues.append("cible de lien %s introuvable" % target)
        return issues

    # ----- orchestration ---------------------------------------------------

    def run(self, targets):
        if self.args.check:
            rc = 0
            for name in targets:
                spec = DEFAULT_GPOS[name]
                self.log.head("%s %s" % (spec["displayName"], spec["guid"]))
                issues = self.check(name, spec)
                if issues:
                    rc = 3
                    for i in issues:
                        self.log.warn(i)
                else:
                    self.log.info("OK — conforme à l'état par défaut (structure)")
            return rc

        for name in targets:
            spec = DEFAULT_GPOS[name]
            self.log.head("%s %s" % (spec["displayName"], spec["guid"]))
            if self.args.dry_run:
                self.log.info("(dry-run : sauvegarde ignorée)")
            else:
                self.backup(name, spec["guid"])
            self.samdb.transaction_start()
            try:
                gpo_dn, version = self.ensure_gpo_object(spec)
                if not self.args.keep_ds_acl:
                    self.reset_ds_acl(gpo_dn)
                self.ensure_link(spec)
                self.samdb.transaction_commit()
            except Exception:
                self.samdb.transaction_cancel()
                raise
            path = self.rebuild_sysvol(name, spec, version)
            self.reset_fs_acl(gpo_dn, path)
            self.log.info("terminé")
        return 0


# ---------------------------------------------------------------------------

def main():
    p = argparse.ArgumentParser(
        prog="samba-dcgpofix",
        description="Recrée les GPO par défaut d'un domaine Samba AD (équivalent dcgpofix).",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="Sans paramètre : équivaut à --target both --profile windows.\n\n"
               "Exemples :\n"
               "  samba-dcgpofix --check\n"
               "  samba-dcgpofix --target dc --dry-run\n"
               "  samba-dcgpofix --profile samba\n")
    p.add_argument("--target", choices=["domain", "dc", "both"], default="both",
                   help="GPO à recréer : domain (DDP), dc (DDCP) ou both (défaut)")
    p.add_argument("--profile", choices=["samba", "windows"], default="windows",
                   help="windows = GptTmpl.inf avec les valeurs par défaut Windows/dcgpofix (défaut) ; "
                        "samba = GPO vides comme après un provision Samba")
    p.add_argument("--check", action="store_true",
                   help="audit seul : signale les écarts structurels, ne modifie rien (rc=3 si écarts)")
    p.add_argument("--dry-run", action="store_true", help="affiche les actions sans rien modifier")
    p.add_argument("--yes", "-y", action="store_true", help="ne pas demander de confirmation")
    p.add_argument("--keep-ds-acl", action="store_true",
                   help="ne pas réinitialiser le nTSecurityDescriptor de l'objet GPO")
    p.add_argument("--backup-dir", default="/var/backups/samba-dcgpofix",
                   help="racine des sauvegardes (défaut : %(default)s)")
    p.add_argument("-s", "--configfile", help="smb.conf à utiliser")
    p.add_argument("-H", "--url", help="URL LDB de sam.ldb (défaut : celui du smb.conf)")
    p.add_argument("--sysvol", help="chemin local de SYSVOL (défaut : path du partage sysvol)")
    p.add_argument("-q", "--quiet", action="store_true")
    args = p.parse_args()

    if os.geteuid() != 0 and not args.check:
        die("à exécuter en root (écriture sam.ldb, SYSVOL et xattr)")

    log = Log(args.quiet)
    tool = DcGpoFix(args, log)
    targets = ["domain", "dc"] if args.target == "both" else [args.target]

    if not (args.check or args.dry_run or args.yes):
        print("Domaine : %s (%s)" % (tool.dnsdomain, tool.domaindn))
        print("Cibles  : %s — profil %s" % (", ".join(DEFAULT_GPOS[t]["displayName"] for t in targets),
                                           args.profile))
        print("Les paramètres actuels de ces GPO seront PERDUS (sauvegarde dans %s)." % tool.backup_dir)
        if args.profile == "windows":
            print("Profil windows : les stratégies de mots de passe / Kerberos par défaut Windows seront")
            print("réappliquées au domaine par samba-gpupdate si 'apply group policies = yes' sur les DC.")
        ans = input("Continuer ? [oui/NON] ").strip().lower()
        if ans not in ("oui", "o", "yes", "y"):
            print("Abandon.")
            return 1

    rc = tool.run(targets)
    if not args.check and not args.dry_run:
        print("\nSauvegardes : %s" % tool.backup_dir)
        print("Étapes suivantes :")
        print("  1. répliquer SYSVOL vers les autres DC (rsync/osync + samba-tool ntacl sysvolcheck)")
        print("  2. samba-gpupdate --force sur chaque DC, puis gpupdate /force sur un client de test")
        print("  3. vérifier dans GPMC : onglet Settings des deux GPO, liens racine et OU=Domain Controllers")
    return rc


if __name__ == "__main__":
    sys.exit(main())

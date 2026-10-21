#!/usr/bin/env python3
"""
Exchange-Schema-Upgrade.py — extension du schéma Samba AD avec le schéma Exchange 2019.

Applique, fichier par fichier et dans l'ordre, les LDF de Setup\\Data d'Exchange via
`samba-tool domain schemaupgrade --ldf-file=... --base-dir=...`, en corrigeant tout ce
qui diffère entre ldifde/AD et samba-tool/Samba.

À exécuter en root, sur le DC Samba détenteur du rôle FSMO "Schema Master".

Déroulement par défaut :
  1. affichage d'un avertissement (modification IRRÉVERSIBLE) et confirmation
  2. sauvegarde offline de l'annuaire : samba-tool domain backup offline
  3. désactivation de la réplication entrante et sortante sur ce DC
  4. passe 1 : application de tous les fichiers, en retirant des classes les
     auto-références (possSuperiors/mayContain/... pointant vers la classe elle-même)
     que Samba refuse à la création
  5. passe 2 : nouvelle application ; les objets existants sont ignorés, les
     auto-références sont ajoutées par un modify différé
  6. réactivation de la réplication si tout s'est bien passé
En cas d'échec, la réplication reste désactivée : les commandes pour la réactiver
à la main sont affichées.

Source par défaut : /usr/share/hardenbox/exchange-data, installé par hb-sambaad à partir de
Packages/HB-SambaAD/exchange-data (copie du dossier Setup\\Data du média Exchange 2019).

Exemples :
  ./Exchange-Schema-Upgrade.py                  # procédure complète
  ./Exchange-Schema-Upgrade.py --dry-run        # prépare les fichiers, n'applique rien
  ./Exchange-Schema-Upgrade.py --start 84       # reprise à partir de schema84
  ./Exchange-Schema-Upgrade.py --only 84,85     # uniquement ces numéros
  ./Exchange-Schema-Upgrade.py --yes            # sans confirmation interactive
  ./Exchange-Schema-Upgrade.py --no-backup --no-replication-control   # mode "je sais ce que je fais"

Fichiers volontairement ignorés : schemaN.ldf sans préfixe (variante Windows 2000 de la même
série, jamais appliquée en plus de PostWindows2003_*) et schemaadam.ldf (AD LDS / Edge).

Traitements appliqués à chaque fichier LDF :
  - lecture (ASCII/UTF-8/UTF-16), CRLF -> LF, dépliage des lignes LDIF
  - remplacement des macros Exchange (<SchemaContainerDN>, <ConfigurationContainerDN>,
    <RootDomainDN>, <DomainDN>) par les DN réels lus dans le rootDSE
  - changetype ntdsschemaadd/ntdsschemamodify -> add/modify
  - entrées 'add' ignorées si l'objet existe déjà (DN, lDAPDisplayName ou OID)
  - entrées 'modify' rendues idempotentes (valeurs déjà présentes retirées, OID résolus)
  - contrôle des dépendances (subClassOf, possSuperiors, mayContain...) avant application
  - schemaUpdateNow inséré avant chaque 'add' (rafraîchit le cache de schéma de Samba)
  - ligne vide finale garantie (samba-tool ignore silencieusement la dernière entrée sinon)
"""

import argparse
import datetime
import os
import re
import socket
import subprocess
import sys

# ============================================================================
#  RÉGLAGES
# ============================================================================
# Dossier des LDF du schéma Exchange 2019 (Setup\\Data du média), installé par hb-sambaad.
EXCHANGE_LDF_DIR = "/usr/share/hardenbox/exchange-data"
# Répertoire de travail où sont écrits les LDF préparés pour samba-tool.
WORK_DIR = "/var/tmp/exchange-schema"
# Base Samba locale.
SAM_LDB = "/var/lib/samba/private/sam.ldb"
# Emplacement des sauvegardes offline.
BACKUP_DIR = "/var/backups"
# ============================================================================

UPDATE_NOW_BLOCK = (
    "dn:\n"
    "changetype: modify\n"
    "add: schemaUpdateNow\n"
    "schemaUpdateNow: 1\n"
    "-\n"
    "\n"
)

REPL_FLAGS = ("DISABLE_INBOUND_REPL", "DISABLE_OUTBOUND_REPL")


def run(cmd, check=True, quiet=False):
    if not quiet:
        print("  $ " + " ".join(cmd))
    r = subprocess.run(cmd, capture_output=True, text=True)
    if not quiet:
        if r.stdout.strip():
            print(r.stdout.rstrip())
        if r.stderr.strip():
            print(r.stderr.rstrip(), file=sys.stderr)
    if check and r.returncode != 0:
        raise SystemExit(f"Commande échouée (code {r.returncode}) : {' '.join(cmd)}")
    return r


def rootdse():
    """Lit les naming contexts dans le rootDSE du sam.ldb local."""
    r = run(["ldbsearch", "-H", SAM_LDB, "-b", "", "-s", "base",
             "schemaNamingContext", "configurationNamingContext",
             "defaultNamingContext", "rootDomainNamingContext"], quiet=True)
    out = {}
    for key in ("schemaNamingContext", "configurationNamingContext",
                "defaultNamingContext", "rootDomainNamingContext"):
        m = re.search(rf"^{key}:\s*(.+?)\s*$", r.stdout, re.M)
        if not m:
            raise SystemExit(f"Impossible de lire {key} dans le rootDSE")
        out[key] = m.group(1)
    return out


def schema_master():
    """Retourne le nom court du DC Schema Master (ou None)."""
    r = run(["samba-tool", "fsmo", "show"], check=False, quiet=True)
    m = re.search(r"^SchemaMasterRole owner:\s*CN=NTDS Settings,CN=([^,]+),", r.stdout, re.M)
    return m.group(1) if m else None


def check_schema_master(assume_yes):
    owner = schema_master()
    host = socket.gethostname().split(".")[0]
    if owner is None:
        print("AVERTISSEMENT : impossible de déterminer le Schema Master.", file=sys.stderr)
    elif owner.upper() != host.upper():
        print(f"AVERTISSEMENT : ce DC ({host}) n'est pas le Schema Master ({owner}). "
              f"Le schéma ne peut être modifié que sur le Schema Master.", file=sys.stderr)
        if assume_yes or input("Continuer quand même ? [o/N] ").strip().lower() != "o":
            raise SystemExit(1)
    return owner or host


def backup():
    target = os.path.join(BACKUP_DIR, f"samba-schema-{datetime.datetime.now():%Y%m%d-%H%M%S}")
    print(f"\nSauvegarde offline de l'annuaire dans {target} ...")
    run(["samba-tool", "domain", "backup", "offline", f"--targetdir={target}"])
    print(f"Sauvegarde OK. Restauration en cas de besoin : samba-tool domain backup restore "
          f"--backup-file={target}/<archive>.tar.bz2 --targetdir=/var/lib/samba --newservername=... "
          f"(cf. wiki Samba, 'Back up and Restoring a Samba AD DC').")


# ----------------------------------------------------------------------------
#  Contrôle de la réplication
# ----------------------------------------------------------------------------

def count_domain_controllers(ncs):
    """Nombre de DC de la forêt = nombre d'objets nTDSDSA (NTDS Settings) sous CN=Sites."""
    r = run(["ldbsearch", "-H", SAM_LDB, "-b", "CN=Sites," + ncs["configurationNamingContext"],
             "(objectClass=nTDSDSA)", "dn"], quiet=True)
    return len(re.findall(r"^dn: ", r.stdout, re.M))


def replication_options(dc):
    """Retourne l'ensemble des DSA options actuelles du DC."""
    r = run(["samba-tool", "drs", "options", dc], quiet=True)
    m = re.search(r"Current DSA options:\s*(.*)", r.stdout)
    if not m:
        raise SystemExit(f"Impossible de lire les DSA options de {dc} :\n{r.stdout}\n{r.stderr}")
    return {o.strip() for o in m.group(1).replace("|", " ").split() if o.strip()}


def set_replication(dc, disable, flags=REPL_FLAGS):
    sign = "+" if disable else "-"
    for flag in flags:
        run(["samba-tool", "drs", "options", dc, f"--dsa-option={sign}{flag}"])


def manual_reenable_instructions(dc):
    return (
        "Pour réactiver la réplication manuellement sur ce DC :\n"
        f"    samba-tool drs options {dc} --dsa-option=-DISABLE_INBOUND_REPL\n"
        f"    samba-tool drs options {dc} --dsa-option=-DISABLE_OUTBOUND_REPL\n"
        f"  puis vérifier :  samba-tool drs options {dc}   et   samba-tool drs showrepl\n"
        "  Ne réactive la réplication entrante qu'une fois le schéma dans un état cohérent :\n"
        "  un autre DC ne doit pas pouvoir répliquer par-dessus un schéma partiellement modifié."
    )


def banner(dc, files, args, dc_count):
    print("=" * 78)
    print(" Exchange-Schema-Upgrade — extension du schéma Samba AD (Exchange 2019)")
    print("=" * 78)
    print(f"""
ATTENTION : la modification du schéma Active Directory est IRRÉVERSIBLE.
Il n'existe pas de suppression d'attribut ou de classe dans AD ; la seule
marche arrière est la restauration complète de l'annuaire depuis la sauvegarde
prise par ce script (ou une restauration de forêt).

Ce que le script va faire :
  1. {'sauvegarde offline de la base Samba dans ' + BACKUP_DIR if not args.no_backup else 'PAS de sauvegarde (--no-backup)'}
  2. {('désactivation de la réplication entrante ET sortante sur ' + dc + f' ({dc_count} DC dans la forêt)') if not args.no_replication_control else ('PAS de contrôle de la réplication : ' + ('un seul DC dans la forêt, rien à isoler' if dc_count <= 1 else '--no-replication-control'))}
  3. passe 1 : application de {len(files)} fichier(s) LDF, auto-références de classes retirées
  4. passe 2 : nouvelle application, auto-références rétablies par modify différé
  5. {'réactivation de la réplication sur ' + dc + ' si tout s\'est bien passé' if not args.no_replication_control else '(rien)'}

Fichiers (dans l'ordre) : {', '.join(n for _, _, n in files)}
Dossier LDF : {args.src_dir}
Répertoire de travail : {args.work_dir}
""")
    if not args.no_replication_control:
        print("En cas d'échec, la réplication RESTE DÉSACTIVÉE sur ce DC (volontairement).")
        print(manual_reenable_instructions(dc))
    print()


def read_ldf(path):
    data = open(path, "rb").read()
    if data[:2] in (b"\xff\xfe", b"\xfe\xff"):
        return data.decode("utf-16")
    try:
        return data.decode("utf-8-sig")
    except UnicodeDecodeError:
        return data.decode("latin-1")


def _unfold_ldif(text):
    """Déplie les lignes LDIF repliées (continuation = ligne commençant par un espace).
    ldbsearch replie à 80 colonnes, ce qui tronque les DN longs."""
    return re.sub(r"\n ", "", text)


class SchemaChecker:
    """Test d'existence autoritaire, par recherche LDAP directe dans sam.ldb."""

    def __init__(self, schema_dn):
        self.schema_dn = schema_dn
        self.samdb = None
        try:
            import ldb
            from samba.samdb import SamDB
            from samba.param import LoadParm
            from samba.auth import system_session
            self.ldb = ldb
            lp = LoadParm()
            lp.load_default()
            self.samdb = SamDB(url=SAM_LDB, session_info=system_session(lp), lp=lp)
            n = len(self.samdb.search(base=schema_dn, scope=ldb.SCOPE_ONELEVEL,
                                      expression="(objectClass=*)", attrs=["dn"]))
            print(f"Schéma actuel : {n} objets (bindings samba)")
        except ImportError:
            print("Bindings samba indisponibles, repli sur ldbsearch")

    def dn_exists(self, dn):
        if self.samdb is not None:
            try:
                return len(self.samdb.search(base=dn, scope=self.ldb.SCOPE_BASE, attrs=["dn"])) > 0
            except self.ldb.LdbError as e:
                if e.args[0] == self.ldb.ERR_NO_SUCH_OBJECT:
                    return False
                raise
        r = subprocess.run(["ldbsearch", "-H", SAM_LDB, "-b", dn, "-s", "base", "dn"],
                           capture_output=True, text=True)
        return "# returned 1 records" in r.stdout

    def find_by_name_or_oid(self, name, oid):
        """Retourne le DN d'un objet portant ce lDAPDisplayName ou cet OID, sinon None."""
        parts = []
        if name:
            parts.append(f"(lDAPDisplayName={name})")
        if oid:
            parts.append(f"(attributeID={oid})(governsID={oid})")
        if not parts:
            return None
        expr = "(|" + "".join(parts) + ")"
        if self.samdb is not None:
            res = self.samdb.search(base=self.schema_dn, scope=self.ldb.SCOPE_ONELEVEL,
                                    expression=expr, attrs=["dn"])
            return str(res[0].dn) if len(res) else None
        r = subprocess.run(["ldbsearch", "-H", SAM_LDB, "-b", self.schema_dn, "-s", "one", expr, "dn"],
                           capture_output=True, text=True)
        m = re.search(r"^dn: (.+)$", _unfold_ldif(r.stdout), re.M)
        return m.group(1) if m else None

    _oid_cache = {}

    def resolve_oid(self, value):
        """Si `value` est un OID d'attribut/classe du schéma, retourne son lDAPDisplayName
        (Samba stocke mayContain/mustContain/possSuperiors... sous forme de noms).
        Sinon retourne `value` inchangé."""
        if not re.fullmatch(r"\d+(\.\d+)+", value):
            return value
        if value in self._oid_cache:
            return self._oid_cache[value]
        expr = f"(|(attributeID={value})(governsID={value}))"
        name = value
        if self.samdb is not None:
            res = self.samdb.search(base=self.schema_dn, scope=self.ldb.SCOPE_ONELEVEL,
                                    expression=expr, attrs=["lDAPDisplayName"])
            if len(res) and "lDAPDisplayName" in res[0]:
                name = str(res[0]["lDAPDisplayName"][0])
        else:
            r = subprocess.run(["ldbsearch", "-H", SAM_LDB, "-b", self.schema_dn, "-s", "one",
                                expr, "lDAPDisplayName"], capture_output=True, text=True)
            m = re.search(r"^lDAPDisplayName: (.+)$", r.stdout, re.M)
            if m:
                name = m.group(1).strip()
        self._oid_cache[value] = name
        return name

    def current_values(self, dn, attr):
        """Valeurs actuelles (minuscules) de `attr` sur `dn`, ou None si l'objet n'existe pas."""
        if self.samdb is not None:
            try:
                res = self.samdb.search(base=dn, scope=self.ldb.SCOPE_BASE, attrs=[attr])
            except self.ldb.LdbError as e:
                if e.args[0] == self.ldb.ERR_NO_SUCH_OBJECT:
                    return None
                raise
            if not len(res):
                return None
            return {str(v).lower() for v in res[0].get(attr, [])}
        r = subprocess.run(["ldbsearch", "-H", SAM_LDB, "-b", dn, "-s", "base", attr],
                           capture_output=True, text=True)
        if "# returned 1 records" not in r.stdout:
            return None
        return {line.split(":", 1)[1].strip().lower()
                for line in _unfold_ldif(r.stdout).splitlines()
                if line.lower().startswith(attr.lower() + ": ")}

    def filter_modify(self, block):
        """Rend une entrée 'modify' idempotente : retire des opérations add:/delete:
        les valeurs déjà présentes / déjà absentes. Retourne (nouveau bloc ou None
        si plus rien à faire, liste des valeurs retirées)."""
        lines = block.split("\n")
        header, body = [], []
        for i, l in enumerate(lines):
            if re.match(r"(?i)^(add|replace|delete):", l):
                header, body = lines[:i], lines[i:]
                break
        else:
            return block, []            # pas d'opération reconnue, on ne touche pas
        dn = next((l.split(":", 1)[1].strip() for l in header if l.lower().startswith("dn:")), None)
        if not dn:
            return block, []

        ops, cur = [], []
        for l in body:
            if l.strip() == "-":
                if cur:
                    ops.append(cur)
                cur = []
            else:
                cur.append(l)
        if cur:
            ops.append(cur)

        kept_ops, removed = [], []
        for op in ops:
            m = re.match(r"(?i)^(add|replace|delete):[ \t]*(\S+)", op[0])
            if not m or m.group(1).lower() == "replace":
                kept_ops.append(op)
                continue
            kind, attr = m.group(1).lower(), m.group(2)
            current = self.current_values(dn, attr)
            if current is None:             # objet absent : laisser Samba produire l'erreur
                kept_ops.append(op)
                continue
            vals = [l for l in op[1:] if l.strip()]
            if kind == "delete" and not vals:   # delete de l'attribut entier
                if current:
                    kept_ops.append(op)
                else:
                    removed.append(f"delete {attr} (déjà absent)")
                continue
            new_vals = []
            for l in vals:
                v = l.split(":", 1)[1].strip()
                resolved = self.resolve_oid(v)
                present = v.lower() in current or resolved.lower() in current
                if (kind == "add" and present) or (kind == "delete" and not present):
                    removed.append(f"{kind} {attr}: {v}" + (f" (= {resolved})" if resolved != v else ""))
                else:
                    new_vals.append(l)
            if new_vals:
                kept_ops.append([op[0]] + new_vals)
        if not kept_ops:
            return None, removed
        out = header + [l for op in kept_ops for l in op + ["-"]]
        return "\n".join(out), removed

    DEP_ATTRS = ("subClassOf", "auxiliaryClass", "systemAuxiliaryClass", "possSuperiors",
                 "systemPossSuperiors", "mayContain", "mustContain", "systemMayContain",
                 "systemMustContain", "rDNAttID")
    _name_cache = {}

    def name_exists(self, name):
        """True si un attribut ou une classe de ce lDAPDisplayName (ou OID) existe."""
        key = name.lower()
        if key not in self._name_cache:
            self._name_cache[key] = self.find_by_name_or_oid(name, name if re.fullmatch(r"\d+(\.\d+)+", name) else None) is not None
        return self._name_cache[key]

    def missing_dependencies(self, block, defined_earlier):
        """Liste (attribut, valeur) des références d'une entrée 'add' qui n'existent ni dans
        le schéma ni parmi les noms déjà définis plus haut dans le fichier."""
        missing = []
        for attr in self.DEP_ATTRS:
            for m in re.finditer(rf"(?im)^{attr}:[ \t]*(.+?)[ \t]*$", block):
                v = m.group(1)
                if v.lower() in defined_earlier or self.name_exists(v):
                    continue
                missing.append((attr, v))
        return missing

    def why_skip(self, e):
        """Raison d'ignorer une entrée 'add', ou None si elle est nouvelle."""
        if self.dn_exists(e["dn"]):
            return "DN déjà présent"
        other = self.find_by_name_or_oid(e["name"], e["oid"])
        if other:
            return f"lDAPDisplayName/OID déjà porté par {other} (conflit !)"
        return None


CHANGETYPE_MAP = {
    "ntdsschemaadd": "add",
    "ntdsschemamodify": "modify",
    "ntdsschemadelete": "delete",
    "ntdsschemamodrdn": "modrdn",
}


def normalize_changetype(block):
    """Remplace les changetypes propres aux LDF Microsoft (ntdsschemaadd, ...) par
    les changetypes LDIF standard, et retourne (bloc normalisé, changetype)."""
    def repl(m):
        ct = CHANGETYPE_MAP.get(m.group(1).lower(), m.group(1).lower())
        return f"changetype: {ct}"
    block = re.sub(r"(?im)^changetype:[ \t]*(\S+)[ \t]*$", repl, block)
    m = re.search(r"(?im)^changetype:[ \t]*(\S+)", block)
    return block, (m.group(1).lower() if m else "add")


def parse_entry(block):
    """Extrait dn, changetype, lDAPDisplayName et OID d'un bloc LDIF."""
    def get(attr):
        m = re.search(rf"(?im)^{attr}:[ \t]*(.*?)[ \t]*$", block)
        return m.group(1) if m else None
    block, changetype = normalize_changetype(block)
    return {
        "block": block,
        "dn": get("dn"),
        "changetype": changetype,
        "name": get("lDAPDisplayName"),
        "oid": get("attributeID") or get("governsID"),
    }


SELF_REF_ATTRS = ("possSuperiors", "systemPossSuperiors", "auxiliaryClass", "systemAuxiliaryClass",
                  "mayContain", "mustContain", "systemMayContain", "systemMustContain")


def split_self_references(e):
    """Samba refuse qu'une classe se référence elle-même (par OID ou par nom) au moment de
    sa création, alors qu'AD l'accepte. Retire ces valeurs du bloc 'add' et retourne
    (bloc sans auto-références, bloc 'modify' à appliquer ensuite ou None)."""
    selfs = {v.lower() for v in (e["name"], e["oid"]) if v}
    if not selfs or not e["name"]:
        return e["block"], None
    kept_lines, deferred = [], {}
    for line in e["block"].split("\n"):
        m = re.match(r"^([A-Za-z]+):[ \t]*(.+?)[ \t]*$", line)
        if m and m.group(1).lower() in {a.lower() for a in SELF_REF_ATTRS} and m.group(2).lower() in selfs:
            deferred.setdefault(m.group(1), set()).add(e["name"])
            continue
        kept_lines.append(line)
    if not deferred:
        return e["block"], None
    ops = "".join(f"add: {attr}\n{attr}: {name}\n-\n" for attr, names in deferred.items() for name in sorted(names))
    return "\n".join(kept_lines), f"dn: {e['dn']}\nchangetype: modify\n{ops}".rstrip("\n")


def prepare(src, dst, ncs, update_now, checker, verbose, self_refs="deferred"):
    """Prépare un fichier LDF. `checker` = SchemaChecker ou None pour ne pas filtrer.
    `self_refs` : "strip"    = passe 1, les auto-références de classe sont retirées et rien d'autre ;
                  "deferred" = passe 2, elles sont réappliquées par un modify différé.
    Retourne (nb_entrées_conservées, nb_entrées_ignorées)."""
    text = _unfold_ldif(read_ldf(src).replace("\r\n", "\n"))
    text = text.replace("<SchemaContainerDN>", ncs["schemaNamingContext"])
    text = text.replace("<ConfigurationContainerDN>", ncs["configurationNamingContext"])
    text = text.replace("<RootDomainDN>", ncs["rootDomainNamingContext"])
    text = text.replace("<DomainDN>", ncs["defaultNamingContext"])

    leftover = sorted(set(re.findall(r"<[A-Za-z]+DN>", text)))
    if leftover:
        raise SystemExit(f"Macros non remplacées dans {src} : {leftover}")

    kept, skipped, entries = [], 0, 0
    defined_earlier, missing_deps = set(), []
    for block in re.split(r"\n{2,}", text.strip()):
        # retire les lignes de commentaire, garde le reste du bloc
        block = "\n".join(l for l in block.split("\n") if not l.startswith("#")).strip()
        if not block:
            continue
        e = parse_entry(block)
        block = e["block"]                    # changetype normalisé (ntdsschemaadd -> add, ...)
        if not e["dn"]:                       # schemaUpdateNow (dn vide) ou bloc sans dn
            kept.append(block)
            continue
        entries += 1

        follow_up = None
        if e["changetype"] == "add":
            block, follow_up = split_self_references(e)
            if self_refs == "strip":
                follow_up = None
            if follow_up and checker is not None:
                # rend le modify idempotent (déjà présent si un run précédent est allé jusqu'au bout)
                follow_up, _ = checker.filter_modify(follow_up)
            if follow_up and verbose:
                print(f"  [add] {e['dn']} : auto-référence(s) déplacée(s) dans un modify différé")
        if checker is not None and e["changetype"] == "add":
            reason = checker.why_skip(e)
            if verbose:
                print(f"  [{e['changetype']}] dn={e['dn']!r} name={e['name']!r} oid={e['oid']!r} "
                      f"-> {'IGNORÉ: ' + reason if reason else 'nouveau'}")
            if reason:
                if not verbose:
                    print(f"  ignoré : {e['dn']} — {reason}")
                skipped += 1
                if e["name"]:
                    defined_earlier.add(e["name"].lower())
                if follow_up:            # l'objet existe mais l'auto-référence peut manquer
                    kept.append(UPDATE_NOW_BLOCK.strip())
                    kept.append(follow_up)
                continue
            for attr, v in checker.missing_dependencies(block, defined_earlier):
                missing_deps.append((e["dn"], attr, v))
            if e["name"]:
                defined_earlier.add(e["name"].lower())
        elif checker is not None and e["changetype"] == "modify":
            block, removed = checker.filter_modify(block)
            if removed and (verbose or block is None):
                print(f"  [modify] {e['dn']} : {len(removed)} valeur(s) déjà à jour"
                      + (" -> entrée ignorée" if block is None else ""))
                if verbose:
                    for r_ in removed:
                        print(f"      - {r_}")
            if block is None:
                skipped += 1
                continue
            if verbose and not removed:
                print(f"  [modify] dn={e['dn']!r} -> conservé")
        elif verbose:
            print(f"  [{e['changetype']}] dn={e['dn']!r} -> conservé")

        if update_now and e["changetype"] == "add":
            kept.append(UPDATE_NOW_BLOCK.strip())
        kept.append(block)
        if follow_up:
            kept.append(UPDATE_NOW_BLOCK.strip())
            kept.append(follow_up)

    # samba-tool n'applique une entrée que lorsqu'il rencontre une ligne vide APRÈS elle :
    # sans ligne vide finale, la dernière entrée du fichier est silencieusement ignorée.
    with open(dst, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n\n".join(kept) + "\n\n")

    if missing_deps:
        print(f"  AVERTISSEMENT : {len(missing_deps)} dépendance(s) introuvable(s) dans le schéma "
              f"(l'application de ce fichier échouera probablement) :", file=sys.stderr)
        for dn, attr, v in missing_deps:
            where = find_definition(os.path.dirname(src), v)
            print(f"    {dn.split(',')[0]} -> {attr}: {v}"
                  + (f"   (défini dans {where})" if where else "   (défini dans aucun fichier source !)"),
                  file=sys.stderr)
    return entries - skipped, skipped


_definition_index = None


def find_definition(src_dir, name):
    """Retourne le nom du fichier LDF de src_dir qui définit ce lDAPDisplayName, ou None."""
    global _definition_index
    if _definition_index is None:
        _definition_index = {}
        for fn in sorted(os.listdir(src_dir)):
            if not fn.lower().endswith(".ldf"):
                continue
            try:
                txt = read_ldf(os.path.join(src_dir, fn))
            except OSError:
                continue
            for m in re.finditer(r"(?im)^lDAPDisplayName:[ \t]*(.+?)[ \t\r]*$", txt):
                _definition_index.setdefault(m.group(1).lower(), fn)
    return _definition_index.get(name.lower())


STAGES_DEFAULT = [
    r"PostWindows2003_schema(\d+)\.ldf",   # série principale, ordre numérique
    r"schemaversion\.ldf",                 # bump de ms-Exch-Schema-Version-Pt, en dernier
]
# Volontairement exclus : schemaN.ldf sans préfixe (variante forêt Windows 2000 de la
# même série, jamais appliquée en plus de PostWindows2003_*) et schemaadam.ldf (AD LDS / Edge).


def list_files(src_dir, stages):
    """Retourne [(stage_idx, num_ou_None, nom)] dans l'ordre d'application.
    Dans une étape dont la regex a un groupe capturant, les fichiers sont triés
    numériquement sur ce groupe ; sinon alphabétiquement."""
    names = os.listdir(src_dir)
    out = []
    for si, pattern in enumerate(stages):
        rx = re.compile("^" + pattern + "$", re.IGNORECASE)
        found = []
        for name in names:
            m = rx.match(name)
            if m:
                num = int(m.group(1)) if m.groups() and m.group(1).isdigit() else None
                found.append((num if num is not None else -1, name.lower(), num, name))
        if not found:
            print(f"AVERTISSEMENT : aucun fichier pour l'étape '{pattern}'", file=sys.stderr)
        for _, _, num, name in sorted(found):
            out.append((si, num, name))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src-dir", default=EXCHANGE_LDF_DIR, help="dossier des LDF Exchange (défaut : %(default)s)")
    ap.add_argument("--work-dir", default=WORK_DIR)
    ap.add_argument("--stages", nargs="+", default=STAGES_DEFAULT, metavar="REGEX",
                    help="étapes, dans l'ordre ; chaque regex doit matcher le nom complet du fichier, "
                         "un groupe capturant numérique sert au tri (défaut : %(default)s)")
    ap.add_argument("--start", type=int, help="premier numéro à appliquer (fichiers numérotés seulement)")
    ap.add_argument("--end", type=int, help="dernier numéro à appliquer (fichiers numérotés seulement)")
    ap.add_argument("--only", help="numéros séparés par des virgules (fichiers numérotés seulement)")
    ap.add_argument("--numbered-only", action="store_true",
                    help="ignore les fichiers non numérotés (ex. schemaversion.ldf)")
    ap.add_argument("--passes", type=int, default=2, choices=(1, 2),
                    help="1 = une seule passe, auto-références réappliquées par modify différé dans la foulée ; "
                         "2 = deux passes (défaut)")
    ap.add_argument("--no-update-now", action="store_true",
                    help="ne pas insérer de schemaUpdateNow avant chaque 'add'")
    ap.add_argument("--no-skip-existing", action="store_true",
                    help="ne pas ignorer les entrées 'add' dont l'objet (DN, lDAPDisplayName ou OID) existe déjà")
    ap.add_argument("--no-backup", action="store_true", help="ne pas faire de samba-tool domain backup offline")
    ap.add_argument("--no-replication-control", action="store_true",
                    help="ne pas désactiver/réactiver la réplication sur le DC")
    ap.add_argument("--yes", "-y", action="store_true", help="ne pas demander de confirmation")
    ap.add_argument("--debug", type=int, default=None, help="niveau -d passé à samba-tool (ex: 3)")
    ap.add_argument("--dry-run", action="store_true", help="prépare les fichiers, n'applique rien")
    ap.add_argument("--verbose", action="store_true",
                    help="affiche, pour chaque entrée, ce qui a été parsé et le résultat des tests")
    args = ap.parse_args()
    update_now = not args.no_update_now

    if os.geteuid() != 0:
        raise SystemExit("À lancer en root.")
    if not os.path.isdir(args.src_dir):
        raise SystemExit(f"Dossier LDF introuvable : {args.src_dir} (copiez Setup\\Data dans Packages/HB-SambaAD/exchange-data puis reconstruisez l'ISO)")

    files = list_files(args.src_dir, args.stages)
    if not files:
        raise SystemExit(f"Aucun fichier correspondant aux étapes dans {args.src_dir}")

    def keep(f):
        _, num, _ = f
        if num is None:
            return not args.numbered_only
        if args.only and num not in {int(x) for x in args.only.split(",")}:
            return False
        if args.start is not None and num < args.start:
            return False
        if args.end is not None and num > args.end:
            return False
        return True
    files = [f for f in files if keep(f)]
    if not files:
        raise SystemExit("Aucun fichier à appliquer après filtrage.")

    ncs = rootdse()
    os.makedirs(args.work_dir, exist_ok=True)

    def new_checker():
        return None if args.no_skip_existing else SchemaChecker(ncs["schemaNamingContext"])

    def prepare_file(name, checker, self_refs):
        kept, skipped = prepare(os.path.join(args.src_dir, name), os.path.join(args.work_dir, name),
                                ncs, update_now, checker, args.verbose, self_refs)
        print(f"{name} : {kept} entrée(s) à appliquer, {skipped} ignorée(s) car déjà présente(s)")
        return kept

    def apply_all(pass_no, self_refs):
        print(f"\n################ PASSE {pass_no} ({'auto-références retirées' if self_refs == 'strip' else 'auto-références rétablies'}) ################")
        checker = new_checker()
        for _, num, name in files:
            print(f"\n===== {name}" + (f" (n°{num})" if num is not None else "") + " =====")
            # Préparation juste avant l'application : les tests tiennent compte des objets
            # créés par les fichiers précédents.
            if prepare_file(name, checker, self_refs) == 0:
                print("  rien à appliquer, fichier sauté.")
                continue
            cmd = ["samba-tool", "domain", "schemaupgrade",
                   f"--ldf-file={name}", f"--base-dir={args.work_dir}"]
            if args.debug is not None:
                cmd += ["-d", str(args.debug)]
            r = run(cmd, check=False)
            if r.returncode != 0:
                print(f"\nÉCHEC sur {name} (passe {pass_no}). Les entrées précédentes de ce fichier sont "
                      f"déjà commitées (une opération LDAP = une transaction).\n"
                      f"Inspecte {os.path.join(args.work_dir, name)}, corrige la source, puis relance "
                      + (f"avec --start {num}" if num is not None else "le script")
                      + " : les entrées déjà appliquées seront ignorées automatiquement.",
                      file=sys.stderr)
                return False
        return True

    if args.dry_run:
        print("Dry-run : préparation des fichiers (passe 2 simulée), rien ne sera appliqué.\n")
        checker = new_checker()
        for _, _, name in files:
            prepare_file(name, checker, "deferred")
        print(f"\nFichiers préparés dans {args.work_dir}.")
        return

    dc = check_schema_master(args.yes)
    dc_count = count_domain_controllers(ncs)
    if dc_count <= 1 and not args.no_replication_control:
        print(f"Un seul contrôleur de domaine détecté ({dc}) : le contrôle de la réplication est inutile, il est désactivé.")
        args.no_replication_control = True
    banner(dc, files, args, dc_count)
    if not args.yes:
        if input("Confirmer le lancement ? Tapez OUI en majuscules : ").strip() != "OUI":
            raise SystemExit("Abandon.")

    if not args.no_backup:
        backup()

    added_flags = ()
    if not args.no_replication_control:
        before = replication_options(dc)
        added_flags = tuple(f for f in REPL_FLAGS if f not in before)
        print(f"\nDSA options actuelles de {dc} : {' '.join(sorted(before)) or '(aucune)'}")
        if added_flags:
            print("Désactivation de la réplication entrante et sortante ...")
            set_replication(dc, disable=True, flags=added_flags)
        else:
            print("La réplication est déjà désactivée ; elle ne sera pas réactivée par le script.")

    ok = False
    try:
        if args.passes == 2:
            ok = apply_all(1, "strip") and apply_all(2, "deferred")
        else:
            ok = apply_all(1, "deferred")
    except KeyboardInterrupt:
        print("\nInterrompu par l'utilisateur.", file=sys.stderr)
    except SystemExit as e:
        if e.code not in (0, None):
            print(f"\nErreur : {e.code}", file=sys.stderr)
        else:
            raise

    if not ok:
        if not args.no_replication_control and added_flags:
            print("\nLa réplication reste DÉSACTIVÉE sur ce DC.", file=sys.stderr)
            print(manual_reenable_instructions(dc), file=sys.stderr)
        raise SystemExit(1)

    if not args.no_replication_control and added_flags:
        print("\nRéactivation de la réplication ...")
        set_replication(dc, disable=False, flags=added_flags)
        run(["samba-tool", "drs", "options", dc], check=False)

    print("\nTous les fichiers ont été appliqués.")
    print("Version du schéma Exchange (rangeUpper de ms-Exch-Schema-Version-Pt) :")
    run(["ldbsearch", "-H", SAM_LDB, "-b", ncs["schemaNamingContext"],
         "(cn=ms-Exch-Schema-Version-Pt)", "rangeUpper"], check=False)
    print("Pense à vérifier la réplication vers les autres DC : samba-tool drs showrepl")


if __name__ == "__main__":
    main()

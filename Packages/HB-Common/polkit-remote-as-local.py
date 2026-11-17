#!/usr/bin/env python3
"""
polkit-remote-as-local.py

Génère une règle polkit qui applique aux sessions distantes (sessions logind
sans seat, ex. « Connexion à distance » GNOME Remote Desktop via GDM) les
autorisations implicites <allow_active> de chaque action, c'est-à-dire le
comportement d'une session locale active.

À relancer après chaque mise à jour de paquets (voir le hook APT en fin de fichier).
"""
import glob
import json
import os
import sys
import tempfile
import xml.etree.ElementTree as ET

ACTIONS_DIR = "/usr/share/polkit-1/actions"
OUTPUT = "/etc/polkit-1/rules.d/60-remote-as-local.rules"

# Groupe autorisé à bénéficier de la règle. None = tous les utilisateurs.
GROUP = "remote-desktop"

# Actions (ou préfixes) à NE PAS aligner sur le local.
EXCLUDE_PREFIXES = (
    # "org.freedesktop.login1.power-off",
    # "org.freedesktop.login1.reboot",
    # "org.freedesktop.login1.suspend",
    # "org.freedesktop.login1.hibernate",
    # "org.freedesktop.NetworkManager.settings.modify.system",
)

VALID = {"no", "yes", "auth_self", "auth_self_keep", "auth_admin", "auth_admin_keep"}


def collect():
    mapping = {}
    for path in sorted(glob.glob(os.path.join(ACTIONS_DIR, "*.policy"))):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError as e:
            print(f"[warn] {path} ignoré : {e}", file=sys.stderr)
            continue
        for action in root.iter("action"):
            aid = action.get("id")
            if not aid or aid.startswith(EXCLUDE_PREFIXES):
                continue
            d = action.find("defaults")
            if d is None:
                continue
            active = (d.findtext("allow_active") or "no").strip()
            any_ = (d.findtext("allow_any") or "no").strip()
            # Inutile d'inscrire une action dont le comportement distant est déjà identique
            if active in VALID and active != any_:
                mapping[aid] = active
    return mapping


def render(mapping):
    group_check = (
        f'if (!subject.isInGroup("{GROUP}")) return polkit.Result.NOT_HANDLED;'
        if GROUP else "// pas de filtrage par groupe"
    )
    body = json.dumps(mapping, indent=4, sort_keys=True).replace("\n", "\n    ")
    return f"""// Généré par {os.path.basename(__file__)} — ne pas éditer à la main.
// Sessions distantes (sans seat) : appliquer <allow_active> comme en session locale.
(function () {{
    var defaults = {body};

    polkit.addRule(function (action, subject) {{
        // Sessions locales ou inactives : comportement polkit inchangé
        if (subject.local || !subject.active) return polkit.Result.NOT_HANDLED;
        {group_check}
        if (defaults.hasOwnProperty(action.id)) return defaults[action.id];
        return polkit.Result.NOT_HANDLED;
    }});
}})();
"""


def main():
    content = render(collect())
    try:
        with open(OUTPUT, encoding="utf-8") as f:
            if f.read() == content:
                return
    except FileNotFoundError:
        pass
    d = os.path.dirname(OUTPUT)
    fd, tmp = tempfile.mkstemp(dir=d, prefix=".remote-as-local.")
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        f.write(content)
    os.chmod(tmp, 0o644)
    os.replace(tmp, OUTPUT)  # écriture atomique ; polkitd recharge tout seul
    print(f"{OUTPUT} mis à jour")


if __name__ == "__main__":
    main()

# Hook APT pour régénérer après chaque installation/mise à jour :
#   /etc/apt/apt.conf.d/99polkit-remote-as-local
#   DPkg::Post-Invoke { "/usr/local/sbin/polkit-remote-as-local.py || true"; };

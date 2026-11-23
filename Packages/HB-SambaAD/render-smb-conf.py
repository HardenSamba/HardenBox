#!/usr/bin/python3
"""Produit le smb.conf HardenBox a partir du modele du paquet hb-sambaad.

provision-samba-ad.sh l'utilise pour ecrire /etc/samba/smb.conf, SambaADPromo
pour l'afficher avant l'installation : les deux voient donc le meme fichier.

Le modele remplace @VARIABLE@ par la valeur donnee avec --set VARIABLE=valeur et
ne garde les lignes comprises entre "@if option@" et "@endif@" que si l'option
figure dans --enable.
"""

import argparse
import re
import sys

TEMPLATE = "/usr/share/hardenbox/samba/smb.conf.template"
PLACEHOLDER = re.compile(r"@([A-Z][A-Z0-9_]*)@")
CONDITION = re.compile(r"^@if ([a-z0-9-]+)@\s*$")
END = re.compile(r"^@endif@\s*$")


def render(template, values, enabled):
    lines = []
    condition = None
    for number, line in enumerate(template.splitlines(), 1):
        start = CONDITION.match(line)
        if start:
            if condition is not None:
                raise ValueError(f"ligne {number} : bloc @if@ imbrique")
            condition = start.group(1)
            continue
        if END.match(line):
            if condition is None:
                raise ValueError(f"ligne {number} : @endif@ sans @if@")
            condition = None
            continue
        if condition is not None and condition not in enabled:
            continue

        def substitute(match):
            name = match.group(1)
            if name not in values:
                raise ValueError(f"ligne {number} : variable @{name}@ sans valeur")
            return values[name]

        # Les commentaires sont recopies tels quels : ils peuvent citer la syntaxe.
        lines.append(line if line.lstrip().startswith(("#", ";")) else PLACEHOLDER.sub(substitute, line))
    if condition is not None:
        raise ValueError(f"bloc @if {condition}@ non termine")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--template", default=TEMPLATE, help=f"modele a utiliser (defaut : {TEMPLATE})")
    parser.add_argument("--set", action="append", default=[], metavar="VARIABLE=valeur",
                        help="valeur d'une variable du modele, repetable")
    parser.add_argument("--enable", default="", metavar="option,option",
                        help="options dont les blocs @if@ sont gardes")
    parser.add_argument("--output", help="fichier a ecrire (defaut : sortie standard)")
    arguments = parser.parse_args()

    values = {}
    for assignment in arguments.set:
        name, separator, value = assignment.partition("=")
        if not separator or not re.fullmatch(r"[A-Z][A-Z0-9_]*", name):
            parser.error(f"--set attend VARIABLE=valeur : {assignment}")
        if "\n" in value or "\r" in value:
            parser.error(f"la valeur de {name} ne doit pas contenir de retour a la ligne")
        values[name] = value
    enabled = {option.strip() for option in arguments.enable.split(",") if option.strip()}

    try:
        with open(arguments.template, encoding="utf-8") as template_file:
            content = render(template_file.read(), values, enabled)
    except (OSError, ValueError) as error:
        print(f"render-smb-conf : {arguments.template} : {error}", file=sys.stderr)
        return 1
    if arguments.output:
        with open(arguments.output, "w", encoding="utf-8") as output:
            output.write(content)
    else:
        sys.stdout.write(content)
    return 0


if __name__ == "__main__":
    sys.exit(main())

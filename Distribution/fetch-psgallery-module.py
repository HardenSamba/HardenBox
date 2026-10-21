#!/usr/bin/env python3
"""Telecharge un module de la PowerShell Gallery pour l'embarquer hors ligne.

Equivalent de `Install-Module -AllowPrerelease` : sans version imposee, la derniere
version publiee est retenue, preversions comprises. Le paquet .nupkg est verifie
avec l'empreinte publiee par la Gallery, conserve dans le cache, puis extrait dans
<sortie>/<Module>/<ModuleVersion>/ comme le ferait PowerShellGet.

Sans acces a la Gallery, la derniere version deja verifiee du cache est reutilisee.
"""

import argparse
import base64
import hashlib
import shutil
import sys
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path, PurePosixPath

GALLERY = "https://www.powershellgallery.com/api/v2"
NAMESPACES = {
    "a": "http://www.w3.org/2005/Atom",
    "m": "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata",
    "d": "http://schemas.microsoft.com/ado/2007/08/dataservices",
}
# Metadonnees NuGet absentes d'un module installe par PowerShellGet.
NUGET_ENTRIES = ("_rels/", "package/", "[Content_Types].xml")


def fetch(url):
    request = urllib.request.Request(url, headers={"User-Agent": "HardenBox-build"})
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def gallery_release(name, version):
    """Retourne (version, empreinte base64, algorithme) publies par la Gallery."""
    query = {"id": f"'{name}'"}
    if version:
        query["$filter"] = f"Version eq '{version}'"
    else:
        query["$filter"] = "IsAbsoluteLatestVersion eq true"
    url = f"{GALLERY}/FindPackagesById()?" + urllib.parse.urlencode(query, safe="'$")
    root = ET.fromstring(fetch(url))
    entries = root.findall("a:entry", NAMESPACES)
    if not entries:
        raise SystemExit(f"ERREUR: {name} {version or '(derniere version)'} introuvable sur la Gallery.")
    properties = entries[0].find("m:properties", NAMESPACES)

    def field(key):
        element = properties.find(f"d:{key}", NAMESPACES)
        return element.text if element is not None else None

    return field("Version"), field("PackageHash"), (field("PackageHashAlgorithm") or "SHA512")


def digest(path, algorithm):
    return base64.b64encode(hashlib.new(algorithm.lower(), path.read_bytes()).digest()).decode()


def version_key(version):
    """Ordre des versions : 0.1.5 > 0.1.5-alpha > 0.1.4."""
    release, _, prerelease = version.partition("-")
    numbers = tuple(int(part) if part.isdigit() else 0 for part in release.split("."))
    return numbers, not prerelease, prerelease


def cached_release(cache_dir, name, version):
    """Plus haute version verifiee du cache (ou celle demandee) : (nupkg, version)."""
    candidates = sorted(
        cache_dir.glob(f"{name}.*.nupkg"),
        key=lambda item: version_key(item.name[len(name) + 1:-len(".nupkg")]),
    )
    for nupkg in reversed(candidates):
        found = nupkg.name[len(name) + 1:-len(".nupkg")]
        stamp = nupkg.with_suffix(".nupkg.sha512")
        if (not version or found == version) and stamp.is_file() and \
                stamp.read_text().strip() == digest(nupkg, "SHA512"):
            return nupkg, found
    return None, None


def extract(nupkg, name, version, output_dir):
    module_version = version.split("-", 1)[0]
    target = output_dir / name / module_version
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True)
    with zipfile.ZipFile(nupkg) as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            if entry.is_dir() or entry.filename.startswith(NUGET_ENTRIES) or \
                    entry.filename.lower() == f"{name.lower()}.nuspec":
                continue
            if path.is_absolute() or ".." in path.parts:
                raise SystemExit(f"ERREUR: chemin invalide dans {nupkg.name}: {entry.filename}")
            # NuGet encode les caracteres speciaux des noms de fichiers (%20...).
            destination = target / urllib.parse.unquote(str(path))
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(archive.read(entry))
    manifest = target / f"{name}.psd1"
    if not manifest.is_file():
        raise SystemExit(f"ERREUR: manifeste {name}.psd1 absent de {nupkg.name}.")
    return target


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("name", help="nom du module, par exemple HGPM")
    parser.add_argument("--version", default="", help="version exacte; vide = derniere, preversions comprises")
    parser.add_argument("--cache-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    args = parser.parse_args()
    args.cache_dir.mkdir(parents=True, exist_ok=True)

    try:
        version, expected, algorithm = gallery_release(args.name, args.version)
    except (OSError, ET.ParseError) as error:
        nupkg, version = cached_release(args.cache_dir, args.name, args.version)
        if nupkg is None:
            raise SystemExit(f"ERREUR: PowerShell Gallery injoignable et aucun {args.name} verifie en cache: {error}")
        print(f"AVERTISSEMENT: Gallery injoignable, {args.name} {version} repris du cache.", file=sys.stderr)
    else:
        nupkg = args.cache_dir / f"{args.name}.{version}.nupkg"
        if not nupkg.is_file() or digest(nupkg, algorithm) != expected:
            url = f"{GALLERY}/package/{urllib.parse.quote(args.name)}/{urllib.parse.quote(version)}"
            nupkg.write_bytes(fetch(url))
        if digest(nupkg, algorithm) != expected:
            nupkg.unlink()
            raise SystemExit(f"ERREUR: empreinte {algorithm} invalide pour {args.name} {version}.")
        nupkg.with_suffix(".nupkg.sha512").write_text(digest(nupkg, "SHA512") + "\n")

    target = extract(nupkg, args.name, version, args.output_dir)
    print(f"{args.name} {version} pret dans {target}")


if __name__ == "__main__":
    main()

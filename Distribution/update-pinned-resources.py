#!/usr/bin/env python3
"""Met a jour les ressources GitHub epinglees dans build-iso.sh.

Pour chaque appel fetch_verified de build-iso.sh, recherche la derniere version
stable (release GitHub, ou dernier commit de la branche par defaut pour une
ressource epinglee sur un commit). Avec --apply, telecharge la nouvelle version
dans vendor-cache, verifie sa somme SHA-256 avec l'empreinte publiee par GitHub
lorsqu'elle existe, puis remplace l'URL, le nom du fichier, la somme et les
references de version dans les sources Harden Box.

Codes de sortie : 0 rien a faire ou mises a jour appliquees, 10 mises a jour
disponibles (sans --apply), 2 verification impossible.
"""

import argparse
import hashlib
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path

DISTRIBUTION = Path(__file__).resolve().parent
REPOSITORY = DISTRIBUTION.parent
BUILD_SCRIPT = DISTRIBUTION / "build-iso.sh"
VENDOR_CACHE = DISTRIBUTION / "vendor-cache"
# Fichiers qui citent les versions des ressources epinglees.
SOURCES = (
    BUILD_SCRIPT,
    REPOSITORY / "Packages/HB-Common/deploy-debian13.sh",
    REPOSITORY / "Packages/HB-SambaAD/provision-samba-ad.sh",
)
UNSTABLE = re.compile(r"(alpha|beta|preview|rc|tp)", re.IGNORECASE)
PIN = re.compile(
    r"fetch_verified \\\n\s+(?P<url>https://github\.com/\S+) \\\n"
    r"\s+\"(?P<dest>[^\"]+)\" \\\n\s+(?P<sha>[0-9a-f]{64})"
)


def github(path):
    request = urllib.request.Request(
        f"https://api.github.com/{path}",
        headers={"Accept": "application/vnd.github+json", "User-Agent": "HardenBox-build"},
    )
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        request.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(request, timeout=30) as response:
        return json.load(response)


def version_key(name):
    return [int(part) for part in re.findall(r"\d+", name)]


def latest_release(repository):
    """(tag, release) de la plus haute version stable, ou (tag, None) pour une etiquette."""
    releases = [r for r in github(f"repos/{repository}/releases?per_page=100")
                if not r.get("draft") and not r.get("prerelease")
                and re.search(r"\d", r["tag_name"]) and not UNSTABLE.search(r["tag_name"])]
    if releases:
        best = max(releases, key=lambda r: version_key(r["tag_name"]))
        return best["tag_name"], best
    tags = [t["name"] for t in github(f"repos/{repository}/tags?per_page=100")
            if re.search(r"\d", t["name"]) and not UNSTABLE.search(t["name"])]
    if not tags:
        raise ValueError(f"aucune version stable pour {repository}")
    return max(tags, key=version_key), None


def latest_commit(repository):
    branch = github(f"repos/{repository}")["default_branch"]
    return github(f"repos/{repository}/commits/{branch}")["sha"]


def download(url, destination):
    request = urllib.request.Request(url, headers={"User-Agent": "HardenBox-build"})
    digest = hashlib.sha256()
    temporary = destination.with_suffix(destination.suffix + ".part")
    with urllib.request.urlopen(request, timeout=300) as response, open(temporary, "wb") as output:
        for chunk in iter(lambda: response.read(1 << 20), b""):
            digest.update(chunk)
            output.write(chunk)
    temporary.replace(destination)
    return digest.hexdigest()


def pinned_resources():
    text = BUILD_SCRIPT.read_text()
    for match in PIN.finditer(text):
        url = match.group("url")
        path = url[len("https://github.com/"):]
        repository = "/".join(path.split("/")[:2])
        release = re.search(r"/releases/download/([^/]+)/([^/]+)$", path)
        archive = re.search(r"/archive/(.+)\.tar\.gz$", path)
        if release:
            kind, pinned, asset = "release", release.group(1), release.group(2)
        elif archive:
            pinned, asset = archive.group(1), None
            kind = "commit" if re.fullmatch(r"[0-9a-f]{7,40}", pinned) else "tag"
        else:
            continue
        dest = match.group("dest")
        # Destination donnee par une variable (POWERSHELL_DEB=...) : sa valeur porte le nom du fichier.
        variable = re.fullmatch(r"\$\{?(\w+)\}?", dest)
        if variable:
            definition = re.search(rf'^{variable.group(1)}="([^"]+)"$', text, re.M)
            if not definition:
                raise ValueError(f"definition de {dest} introuvable dans build-iso.sh")
            dest = definition.group(1)
        yield {"repository": repository, "kind": kind, "pinned": pinned, "asset": asset,
               "url": url, "dest": dest, "sha": match.group("sha")}


def plan(resource):
    """Calcule la nouvelle version, ou None si la ressource est a jour."""
    repository, pinned = resource["repository"], resource["pinned"]
    if resource["kind"] == "commit":
        latest = latest_commit(repository)
        if latest.startswith(pinned):
            return None
        new = latest[:len(pinned)]
        return {"old": pinned, "new": new, "url": resource["url"].replace(f"/{pinned}.tar.gz", f"/{new}.tar.gz"),
                "asset": None, "digest": None}
    tag, release = latest_release(repository)
    if tag.lstrip("v") == pinned.lstrip("v"):
        return None
    old_version, new_version = pinned.lstrip("v"), tag.lstrip("v")
    if resource["kind"] == "release":
        asset_name = resource["asset"].replace(old_version, new_version)
        assets = {a["name"]: a for a in (release or {}).get("assets", [])}
        if asset_name not in assets:
            raise ValueError(f"{repository} {tag} : fichier {asset_name} absent de la release")
        digest = (assets[asset_name].get("digest") or "").removeprefix("sha256:") or None
        url = assets[asset_name]["browser_download_url"]
    else:
        asset_name, digest = None, None
        url = resource["url"].replace(f"/{pinned}.tar.gz", f"/{tag}.tar.gz")
    return {"old": pinned, "new": tag, "url": url, "asset": asset_name, "digest": digest,
            "old_version": old_version, "new_version": new_version}


def keyword(resource):
    """Mot qui identifie les lignes d'une ressource dans les sources."""
    name = resource["repository"].split("/")[1].lower()
    return {"we10x-icon-theme": "we10x"}.get(name, name)


def apply(resource, update):
    old_dest = resource["dest"]
    old_file = old_dest.rsplit("/", 1)[-1]
    if "$" in old_file or "/" not in old_dest and not old_file.endswith((".deb", ".tar.gz")):
        raise ValueError(f"nom de fichier non reconnu : {old_dest}")
    if update["asset"]:
        new_file = update["asset"]
    else:
        new_file = old_file.replace(update["old"], update["new"])
    new_dest = old_dest.replace(old_file, new_file)
    VENDOR_CACHE.mkdir(parents=True, exist_ok=True)
    sha = download(update["url"], VENDOR_CACHE / new_file)
    if update["digest"] and sha != update["digest"]:
        (VENDOR_CACHE / new_file).unlink()
        raise ValueError(f"{new_file} : SHA-256 different de l'empreinte publiee par GitHub")
    old_tokens = {update["old"], update.get("old_version") or update["old"]}
    word = keyword(resource)
    changed = []
    for source in SOURCES:
        if not source.is_file():
            continue
        lines = source.read_text().split("\n")
        updated = []
        for line in lines:
            new_line = line.replace(resource["url"], update["url"]).replace(old_file, new_file)
            new_line = new_line.replace(resource["sha"], sha)
            # Paquet reconditionne par Harden Box (ex. OpenRSAT 0.4.519+hardenbox1).
            if update.get("old_version"):
                new_line = new_line.replace(f"{update['old_version']}+hardenbox", f"{update['new_version']}+hardenbox")
            if word in line.lower():
                for token in sorted(old_tokens, key=len, reverse=True):
                    new_token = update["new"] if token == update["old"] else update["new_version"]
                    # Un numero de version n'est jamais colle a un autre chiffre ou point.
                    new_line = re.sub(rf"(?<![\d.]){re.escape(token)}(?![\d])", new_token, new_line)
            updated.append(new_line)
        if updated != lines:
            source.write_text("\n".join(updated))
            changed.append(str(source.relative_to(REPOSITORY)))
    return sha, new_dest, changed


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--apply", action="store_true", help="telecharge et applique les mises a jour")
    args = parser.parse_args()
    pending = applied = 0
    failed = False
    for resource in pinned_resources():
        label = f"{resource['repository']:34} {resource['pinned']:12}"
        try:
            update = plan(resource)
        except (urllib.error.URLError, OSError, ValueError, KeyError) as error:
            print(f"{label} ERREUR: verification impossible ({error})", file=sys.stderr)
            failed = True
            continue
        if update is None:
            print(f"{label} a jour")
            continue
        pending += 1
        if not args.apply:
            print(f"{label} MISE A JOUR DISPONIBLE : {update['new']}")
            continue
        try:
            sha, new_dest, changed = apply(resource, update)
        except (urllib.error.URLError, OSError, ValueError) as error:
            print(f"{label} ERREUR: mise a jour {update['new']} impossible ({error})", file=sys.stderr)
            failed = True
            continue
        applied += 1
        checked = "verifiee avec GitHub" if update["digest"] else "calculee"
        print(f"{label} MISE A JOUR APPLIQUEE : {update['new']} (SHA-256 {checked} : {sha[:16]}...)")
        print(f"{'':48}fichiers modifies : {', '.join(changed)}")
    if args.apply and applied:
        print(f"\n{applied} ressource(s) mise(s) a jour : verifiez puis commitez les fichiers modifies.")
    if failed:
        return 2
    return 10 if pending and not args.apply else 0


if __name__ == "__main__":
    sys.exit(main())

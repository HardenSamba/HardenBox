#!/usr/bin/env bash

# Compare les ressources GitHub epinglees dans build-iso.sh a leurs dernieres
# versions publiees. Le script ne modifie rien.
#
# Codes de sortie : 0 tout est a jour, 10 mise a jour disponible,
# 2 verification impossible (reseau, limite de l'API GitHub).

set -Eeuo pipefail
IFS=$'\n\t'

SCRIPT_DIR=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
BUILD_SCRIPT="$SCRIPT_DIR/build-iso.sh"
GITHUB_API=https://api.github.com

command -v curl >/dev/null 2>&1 || { printf 'ERREUR: curl est requis.\n' >&2; exit 2; }
command -v python3 >/dev/null 2>&1 || { printf 'ERREUR: python3 est requis.\n' >&2; exit 2; }

curl_options=(--fail --silent --location --max-time 20 --retry 3
    --header 'Accept: application/vnd.github+json')
# Un jeton evite la limite de 60 requetes par heure de l'API anonyme.
if [[ -n ${GITHUB_TOKEN:-} ]]; then
    curl_options+=(--header "Authorization: Bearer $GITHUB_TOKEN")
fi

github_get() {
    curl "${curl_options[@]}" "$GITHUB_API/$1"
}

json_field() {
    python3 -I -c '
import json, sys
try:
    print(json.load(sys.stdin)[sys.argv[1]])
except (KeyError, TypeError, ValueError):
    raise SystemExit(1)' "$1"
}

# Plus haute version stable d'une liste JSON de releases ou d'etiquettes.
highest_stable() {
    python3 -I -c '
import json, re, sys
try:
    items = json.load(sys.stdin)
except ValueError:
    raise SystemExit(1)
names = [
    item.get("tag_name") or item.get("name") or ""
    for item in items
    if not item.get("draft") and not item.get("prerelease")
]
unstable = re.compile(r"(alpha|beta|preview|rc|tp)", re.IGNORECASE)
names = [name for name in names if re.search(r"\d", name) and not unstable.search(name)]
if not names:
    raise SystemExit(1)
print(max(names, key=lambda name: [int(part) for part in re.findall(r"\d+", name)]))'
}

latest_release() {
    local repository=$1 releases
    releases=$(github_get "repos/$repository/releases?per_page=100") || return 1
    highest_stable <<<"$releases" && return 0
    # Depot sans release GitHub : plus haute etiquette stable.
    github_get "repos/$repository/tags?per_page=100" | highest_stable
}

latest_commit() {
    local repository=$1 branch
    branch=$(github_get "repos/$repository" | json_field default_branch)
    github_get "repos/$repository/commits/$branch" | json_field sha
}

mapfile -t pinned_urls < <(grep -oE 'https://github\.com/[^[:space:]"]+' "$BUILD_SCRIPT" | sort -u)
((${#pinned_urls[@]})) || { printf 'ERREUR: aucune ressource epinglee dans %s\n' "$BUILD_SCRIPT" >&2; exit 2; }

status=0
printf '%-36s %-14s %-14s %s\n' "Depot" "Epinglee" "Disponible" "Etat"
for url in "${pinned_urls[@]}"; do
    path=${url#https://github.com/}
    repository=$(cut -d/ -f1-2 <<<"$path")
    kind=release
    if [[ $path =~ /releases/download/([^/]+)/ ]]; then
        pinned=${BASH_REMATCH[1]}
    elif [[ $path =~ /archive/(.+)\.tar\.gz$ ]]; then
        pinned=${BASH_REMATCH[1]}
        [[ ! $pinned =~ ^[0-9a-f]{7,40}$ ]] || kind=commit
    else
        continue
    fi

    if [[ $kind == commit ]]; then
        latest=$(latest_commit "$repository") || latest=""
    else
        latest=$(latest_release "$repository") || latest=""
    fi

    if [[ -z $latest ]]; then
        state="ERREUR: verification impossible"
        ((status == 10)) || status=2
    elif [[ $kind == commit && $latest == "$pinned"* ]] || \
        [[ $kind == release && ${latest#v} == "${pinned#v}" ]]; then
        state="a jour"
    else
        state="MISE A JOUR DISPONIBLE"
        status=10
    fi
    [[ $kind == release ]] || latest=${latest:0:7}
    printf '%-36s %-14s %-14s %s\n' "$repository" "$pinned" "${latest:--}" "$state"
done

if ((status == 10)); then
    printf '\nMettez a jour l URL et la somme SHA256 dans build-iso.sh (voir README.md).\n'
fi
exit "$status"

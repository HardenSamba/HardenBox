#!/usr/bin/python3

import ipaddress
import json
import os
import pwd
import socket
import subprocess
import sys


def detected_defaults():
    users = [
        entry.pw_name
        for entry in pwd.getpwall()
        if 1000 <= entry.pw_uid < 60000 and entry.pw_name != "nobody"
    ]
    defaults = {
        "hostname": socket.getfqdn().strip().lower(),
        "admin-user": os.environ.get("SUDO_USER") or (users[0] if users else ""),
        "ssh-port": "22",
        "ssh-allowed-cidr": "0.0.0.0/0",
        "network-interface": "",
        "ip-address": "",
        "gateway": "",
        "dns": "",
    }
    try:
        links = json.loads(subprocess.run(
            ["ip", "-j", "link", "show"],
            text=True,
            capture_output=True,
            timeout=3,
            check=True,
        ).stdout)
        defaults["network-interface"] = next(
            link["ifname"] for link in links if link["ifname"] != "lo"
        )
        routes = json.loads(subprocess.run(
            ["ip", "-j", "-4", "route", "show", "default"],
            text=True,
            capture_output=True,
            timeout=3,
            check=True,
        ).stdout)
        if routes:
            defaults["network-interface"] = routes[0]["dev"]
            defaults["gateway"] = routes[0].get("gateway", "")
    except (IndexError, KeyError, OSError, StopIteration, subprocess.SubprocessError, ValueError):
        pass
    return defaults


def ask(label, default="", required=True):
    suffix = f" [{default}]" if default else ""
    while True:
        value = input(f"{label}{suffix} : ").strip() or default
        if value or not required:
            return value
        print("Cette valeur est obligatoire.")


def validate(values):
    if not values["hostname"] or len(values["hostname"]) > 253:
        raise ValueError("Nom d'hote invalide.")
    try:
        port = int(values["ssh-port"])
        if not 1 <= port <= 65535:
            raise ValueError
    except ValueError as error:
        raise ValueError("Le port SSH doit etre compris entre 1 et 65535.") from error
    ipaddress.ip_network(values["ssh-allowed-cidr"], strict=False)
    interface = ipaddress.ip_interface(values["ip-address"])
    gateway = ipaddress.ip_address(values["gateway"])
    if interface.version != 4 or gateway.version != 4 or gateway not in interface.network:
        raise ValueError("La passerelle doit appartenir au reseau IPv4 configure.")
    for server in values["dns"].split(","):
        if ipaddress.ip_address(server.strip()).version != 4:
            raise ValueError("Seuls les serveurs DNS IPv4 sont acceptes.")


def main():
    if os.geteuid() != 0:
        os.execvp("sudo", ["sudo", sys.executable, os.path.abspath(__file__)])
    if not sys.stdin.isatty():
        raise SystemExit("L'assistant console exige un terminal interactif.")

    defaults = detected_defaults()
    print("\nConfiguration initiale HardenBox Core")
    print("Les valeurs seront appliquees avant la configuration Samba AD.\n")
    values = {
        "hostname": ask("Nom d'hote complet", defaults["hostname"]),
        "admin-user": ask("Compte administrateur", defaults["admin-user"]),
        "ssh-port": ask("Port SSH", defaults["ssh-port"]),
        "ssh-allowed-cidr": ask("Reseau autorise pour SSH", defaults["ssh-allowed-cidr"]),
        "network-interface": ask("Interface reseau", defaults["network-interface"]),
        "ip-address": ask("Adresse IPv4 avec prefixe (ex: 192.168.10.20/24)"),
        "gateway": ask("Passerelle IPv4", defaults["gateway"]),
        "dns": ask("Serveurs DNS separes par des virgules", defaults["dns"]),
    }
    try:
        validate(values)
    except ValueError as error:
        raise SystemExit(f"ERREUR: {error}") from error

    print("\nResume")
    for label, name in (
        ("Nom d'hote", "hostname"),
        ("Administrateur", "admin-user"),
        ("SSH", "ssh-port"),
        ("Reseau SSH", "ssh-allowed-cidr"),
        ("Interface", "network-interface"),
        ("Adresse", "ip-address"),
        ("Passerelle", "gateway"),
        ("DNS", "dns"),
    ):
        print(f"  {label:<16} {values[name]}")
    if input("\nAppliquer cette configuration ? [o/N] : ").strip().lower() not in ("o", "oui"):
        raise SystemExit("Configuration annulee.")

    command = [
        "/usr/lib/hardenbox/hb-common-config-apply",
        "--profile", "server",
        "--desktop-user", "",
    ]
    for name, value in values.items():
        command.extend((f"--{name}", value))
    result = subprocess.run(command, check=False)
    if result.returncode != 0:
        raise SystemExit(result.returncode)
    print("\nConfiguration Common terminee.")
    print("L'assistant Samba AD est maintenant disponible sur le reseau.")


if __name__ == "__main__":
    main()
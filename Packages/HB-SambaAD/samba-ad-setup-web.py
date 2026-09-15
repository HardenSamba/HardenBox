#!/usr/bin/python3

import hmac
import ipaddress
import json
import os
import secrets
import socket
import subprocess
import threading
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse


HOST = "127.0.0.1"
PORT = 9090
STATE_DIR = Path("/var/lib/hardenbox")
TOKEN_FILE = STATE_DIR / "sambaad-web.token"
COMPLETED_MARKER = STATE_DIR / "hb-sambaad.completed"
HTML_FILE = Path("/usr/share/hardenbox/samba-ad-setup.html")
HARDENING_OPTIONS = {"mdb", "smb", "winbind", "audit", "backup", "password", "hibp", "powershell"}


class SetupState:
    def __init__(self):
        self.lock = threading.Lock()
        self.running = False
        self.success = COMPLETED_MARKER.exists()
        self.message = "Configuration deja terminee." if self.success else "Pret a configurer Samba AD."
        self.output = ""

    def snapshot(self):
        with self.lock:
            return {
                "running": self.running,
                "success": self.success,
                "message": self.message,
                "output": self.output,
            }


STATE = SetupState()
TOKEN = ""


def create_token():
    STATE_DIR.mkdir(mode=0o700, parents=True, exist_ok=True)
    token = secrets.token_urlsafe(32)
    descriptor = os.open(TOKEN_FILE, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
    try:
        os.write(descriptor, (token + "\n").encode())
    finally:
        os.close(descriptor)
    os.chown(TOKEN_FILE, 0, 0)
    os.chmod(TOKEN_FILE, 0o600)
    return token


def system_defaults():
    defaults = {
        "resume": Path("/var/lib/samba/private/sam.ldb").exists(),
        "deployment": "forest",
        "realm": "AD.EXEMPLE.FR",
        "domain": "EXEMPLE",
        "domain-admin": "Administrator",
        "domain-dns": "",
        "hostname": socket.gethostname().split(".", 1)[0].lower(),
        "interface": "ens18",
        "ip-address": "192.168.10.10/24",
        "gateway": "192.168.10.1",
        "dns-forwarder": "1.1.1.1",
        "allowed-cidr": "192.168.10.0/24",
        "function-level": "2016",
        "hardening": ["mdb", "smb", "winbind", "audit", "backup"],
    }
    try:
        routes = json.loads(subprocess.run(
            ["ip", "-j", "-4", "route", "show", "default"],
            text=True,
            capture_output=True,
            timeout=3,
            check=True,
        ).stdout)
        route = routes[0]
        defaults["interface"] = route["dev"]
        defaults["gateway"] = route.get("gateway", defaults["gateway"])
        links = json.loads(subprocess.run(
            ["ip", "-j", "-4", "address", "show", "dev", route["dev"], "scope", "global"],
            text=True,
            capture_output=True,
            timeout=3,
            check=True,
        ).stdout)
        address = next(item for item in links[0].get("addr_info", []) if item.get("family") == "inet")
        cidr = f'{address["local"]}/{address["prefixlen"]}'
        defaults["ip-address"] = cidr
        defaults["allowed-cidr"] = str(ipaddress.ip_interface(cidr).network)
    except (IndexError, KeyError, OSError, StopIteration, subprocess.SubprocessError, ValueError):
        pass
    return defaults


def required_string(payload, name, maximum=255):
    value = payload.get(name)
    if not isinstance(value, str) or not value.strip() or len(value) > maximum:
        raise ValueError(f"Champ invalide ou absent: {name}")
    return value.strip()


def build_command(payload):
    deployment = required_string(payload, "deployment", 10)
    if deployment not in ("forest", "dc"):
        raise ValueError("Operation de deploiement invalide.")
    function_level = required_string(payload, "function-level", 10)
    if function_level not in ("2016", "2008_R2"):
        raise ValueError("Niveau fonctionnel invalide.")
    password = required_string(payload, "password", 1024)
    if not hmac.compare_digest(password, required_string(payload, "password-again", 1024)):
        raise ValueError("Les mots de passe ne correspondent pas.")
    hardening = payload.get("hardening", [])
    if not isinstance(hardening, list) or any(option not in HARDENING_OPTIONS for option in hardening):
        raise ValueError("Options de durcissement invalides.")
    command = [
        "/usr/sbin/provision-samba-ad.sh",
        "--non-interactive",
        "--password-stdin",
    ]
    if Path("/var/lib/samba/private/sam.ldb").exists():
        command.append("--resume")
    else:
        command.extend(("--cleanup-partial", "--deployment", deployment))
    for name in (
        "realm", "domain", "hostname", "interface", "ip-address", "gateway",
        "dns-forwarder", "allowed-cidr",
    ):
        command.extend((f"--{name}", required_string(payload, name)))
    command.extend(("--function-level", function_level))
    command.extend(("--hardening", ",".join(hardening) or "none"))
    if deployment == "dc":
        command.extend(("--domain-admin", required_string(payload, "domain-admin")))
        command.extend(("--domain-dns", required_string(payload, "domain-dns")))
    return command, password


def run_setup(command, password):
    with STATE.lock:
        STATE.success = False
        STATE.message = "Provisionnement Samba AD en cours..."
        STATE.output = ""
    try:
        result = subprocess.run(
            command,
            input=password + "\n",
            text=True,
            capture_output=True,
            check=False,
        )
        output = (result.stderr or result.stdout)[-8000:]
        with STATE.lock:
            STATE.success = result.returncode == 0
            STATE.message = (
                "Provisionnement termine. Redemarrez le serveur."
                if STATE.success else "Le provisionnement a echoue."
            )
            STATE.output = output
    except OSError as error:
        with STATE.lock:
            STATE.success = False
            STATE.message = "Impossible de lancer le provisionnement."
            STATE.output = str(error)
    finally:
        password = ""
        with STATE.lock:
            STATE.running = False


class SetupHandler(BaseHTTPRequestHandler):
    server_version = "HardenBoxSetup/1.0"

    def log_message(self, message, *args):
        print(f"{self.client_address[0]} - {message % args}")

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header(
            "Content-Security-Policy",
            "default-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; form-action 'self'",
        )
        super().end_headers()

    def send_json(self, status, payload):
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def authenticated(self):
        supplied = self.headers.get("X-HardenBox-Token", "")
        if not hmac.compare_digest(supplied, TOKEN):
            self.send_json(HTTPStatus.UNAUTHORIZED, {"error": "Jeton d'acces invalide."})
            return False
        return True

    def read_json(self):
        try:
            length = int(self.headers.get("Content-Length", "0"))
        except ValueError as error:
            raise ValueError("Taille de requete invalide.") from error
        if length < 1 or length > 65536:
            raise ValueError("Taille de requete invalide.")
        try:
            payload = json.loads(self.rfile.read(length))
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise ValueError("Corps JSON invalide.") from error
        if not isinstance(payload, dict):
            raise ValueError("Objet JSON attendu.")
        return payload

    def do_GET(self):
        path = urlparse(self.path).path
        if path == "/":
            try:
                body = HTML_FILE.read_bytes()
            except OSError:
                self.send_error(HTTPStatus.INTERNAL_SERVER_ERROR)
                return
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return
        if path in ("/api/defaults", "/api/status"):
            if not self.authenticated():
                return
            payload = system_defaults() if path == "/api/defaults" else STATE.snapshot()
            self.send_json(HTTPStatus.OK, payload)
            return
        self.send_error(HTTPStatus.NOT_FOUND)

    def do_POST(self):
        path = urlparse(self.path).path
        if not self.authenticated():
            return
        try:
            payload = self.read_json()
            if path == "/api/dns-test":
                realm = required_string(payload, "realm").lower()
                dns_server = required_string(payload, "domain-dns")
                for record_type, name in (
                    ("SOA", realm),
                    ("SRV", f"_ldap._tcp.dc._msdcs.{realm}"),
                    ("SRV", f"_kerberos._tcp.{realm}"),
                ):
                    subprocess.run(
                        ["host", "-W", "3", "-t", record_type, name, dns_server],
                        text=True,
                        capture_output=True,
                        timeout=5,
                        check=True,
                    )
                self.send_json(HTTPStatus.OK, {"message": "Resolution DNS valide."})
                return
            if path != "/api/apply":
                self.send_error(HTTPStatus.NOT_FOUND)
                return
            with STATE.lock:
                if COMPLETED_MARKER.exists():
                    self.send_json(HTTPStatus.CONFLICT, {"error": "La configuration Samba AD est deja terminee."})
                    return
                if STATE.running:
                    self.send_json(HTTPStatus.CONFLICT, {"error": "Un provisionnement est deja en cours."})
                    return
                STATE.running = True
            command, password = build_command(payload)
        except (ValueError, subprocess.SubprocessError) as error:
            with STATE.lock:
                STATE.running = False
            self.send_json(HTTPStatus.BAD_REQUEST, {"error": str(error)})
            return
        threading.Thread(target=run_setup, args=(command, password), daemon=True).start()
        self.send_json(HTTPStatus.ACCEPTED, {"message": "Provisionnement lance."})


def main():
    global TOKEN
    if os.geteuid() != 0:
        raise SystemExit("Le service doit etre execute en root.")
    TOKEN = create_token()
    server = ThreadingHTTPServer((HOST, PORT), SetupHandler)
    print(f"Assistant Samba AD disponible sur http://{HOST}:{PORT}")
    print(f"Jeton lisible avec: sudo cat {TOKEN_FILE}")
    server.serve_forever()


if __name__ == "__main__":
    main()
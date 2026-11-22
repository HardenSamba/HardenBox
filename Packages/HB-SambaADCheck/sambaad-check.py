#!/usr/bin/env python3
"""Verifie un controleur de domaine Samba AD Harden Box apres sa promotion.

Teste les services, la configuration, le DNS du domaine, la redirection DNS,
les ports a l'ecoute et le pare-feu, l'heure signee, Kerberos, SYSVOL et
NETLOGON, LDAP et LDAPS, la base, la replication, les roles FSMO et les GPO
par defaut. Le mot de passe Administrator, facultatif, est lu sur l'entree
standard (--password-stdin) pour les tests Kerberos et SMB.

Codes de sortie : 0 tout est correct, 1 avertissements, 2 echecs.
"""

import argparse
import ipaddress
import json
import os
import re
import socket
import subprocess
import sys
import tempfile
from pathlib import Path

OK, WARN, FAIL, SKIP = "ok", "warn", "fail", "skip"

# Ports d'un controleur de domaine Harden Box (ADWS compris).
TCP_PORTS = {53: "DNS", 88: "Kerberos", 135: "RPC", 389: "LDAP", 445: "SMB", 464: "kpasswd",
             636: "LDAPS", 3268: "GC", 3269: "GC SSL", 9389: "ADWS"}
UDP_PORTS = {53: "DNS", 88: "Kerberos", 123: "NTP", 389: "CLDAP", 464: "kpasswd"}

# Traductions anglaises, indexees par le texte francais (meme principe que SambaADPromo).
EN = {
    "Services": "Services",
    "Configuration": "Configuration",
    "DNS du domaine": "Domain DNS",
    "Redirection DNS": "DNS forwarding",
    "Ports": "Ports",
    "Pare-feu": "Firewall",
    "Heure": "Time",
    "Kerberos": "Kerberos",
    "Partages": "Shares",
    "Annuaire": "Directory",
    "Replication": "Replication",
    "GPO": "GPO",
    "Service {}": "Service {}",
    "actif": "active",
    "inactif ({})": "inactive ({})",
    "Role du serveur": "Server role",
    "Royaume et domaine NetBIOS": "Realm and NetBIOS domain",
    "Niveau fonctionnel": "Functional level",
    "Enregistrement {} {}": "{} record {}",
    "Nom {} -> {}": "Name {} -> {}",
    "absent : {}": "missing: {}",
    "{} ne correspond a aucune adresse locale ({})": "{} does not match any local address ({})",
    "Redirecteurs configures": "Configured forwarders",
    "aucun redirecteur : les noms externes ne seront pas resolus": "no forwarder: external names will not be resolved",
    "{} designe ce controleur : boucle DNS": "{} is this controller: DNS loop",
    "Redirecteur {} joignable": "Forwarder {} reachable",
    "{} ne repond pas pour {}": "{} does not answer for {}",
    "Resolution externe via Samba ({})": "External resolution through Samba ({})",
    "{} non resolu par le DNS local : {}": "{} not resolved by the local DNS: {}",
    "Serveurs DNS du systeme (/etc/resolv.conf)": "System DNS servers (/etc/resolv.conf)",
    "aucun ne designe ce controleur ni un autre DC": "none points to this controller or another DC",
    "Port {} {} ({}) a l'ecoute": "Port {} {} ({}) listening",
    "personne n'ecoute": "nothing listens",
    "Regles nftables": "nftables rules",
    "pas de politique de blocage en entree : tout est ouvert": "no input drop policy: everything is open",
    "Port {} {} ({}) autorise": "Port {} {} ({}) allowed",
    "aucune regle d'acceptation trouvee": "no accept rule found",
    "nftables illisible : {}": "cannot read nftables: {}",
    "Synchronisation Chrony": "Chrony synchronisation",
    "Socket ntp_signd pour l'heure signee": "ntp_signd socket for signed time",
    "droits {} {}:{} (attendu 0750 root:_chrony)": "permissions {} {}:{} (expected 0750 root:_chrony)",
    "Ticket Kerberos pour {}": "Kerberos ticket for {}",
    "mot de passe non fourni": "password not provided",
    "Partage {} accessible en Kerberos": "Share {} reachable with Kerberos",
    "pas de ticket Kerberos": "no Kerberos ticket",
    "Reponse CLDAP du controleur": "Controller CLDAP answer",
    "Certificat LDAPS (port 636)": "LDAPS certificate (port 636)",
    "Coherence de la base (dbcheck)": "Database consistency (dbcheck)",
    "Droits SYSVOL (ntacl sysvolcheck)": "SYSVOL permissions (ntacl sysvolcheck)",
    "Roles FSMO": "FSMO roles",
    "Etat de la replication": "Replication state",
    "un seul controleur : pas de replication": "single controller: no replication",
    "GPO par defaut (samba-dcgpofix --check)": "Default GPOs (samba-dcgpofix --check)",
    "ecarts detectes : relancez SambaADPromo, Corriger le durcissement et les GPO": "deviations found: relaunch SambaADPromo, Correct the hardening and the GPOs",
    "commande absente : {}": "missing command: {}",
    "delai depasse": "timed out",
    "Ce test exige root : sudo sambaad-check": "This test needs root: sudo sambaad-check",
    "Ce serveur n'est pas un controleur de domaine Samba AD.": "This server is not a Samba AD domain controller.",
    "Controleur {} - domaine {}": "Controller {} - domain {}",
    "{} correct(s), {} avertissement(s), {} echec(s), {} ignore(s)": "{} passed, {} warning(s), {} failure(s), {} skipped",
    "OK": "OK",
    "AVERT.": "WARN",
    "ECHEC": "FAIL",
    "IGNORE": "SKIP",
}


def detect_language():
    for variable in ("LANGUAGE", "LC_ALL", "LC_MESSAGES", "LANG"):
        value = os.environ.get(variable, "")
        if value and value not in ("C", "C.UTF-8", "POSIX"):
            return "en" if value.lower().startswith("en") else "fr"
    try:
        for line in Path("/etc/default/locale").read_text().splitlines():
            if line.startswith("LANG="):
                return "en" if line.split("=", 1)[1].strip('"').lower().startswith("en") else "fr"
    except OSError:
        pass
    return "fr"


LANGUAGE = detect_language()


def tr(text):
    return EN.get(text, text) if LANGUAGE == "en" else text


def run(command, timeout=30, input_text=None, env=None):
    """Execute une commande ; renvoie (code, sortie combinee)."""
    try:
        result = subprocess.run(command, text=True, capture_output=True, timeout=timeout,
                                input=input_text, env=env, check=False)
    except FileNotFoundError:
        return 127, tr("commande absente : {}").format(command[0])
    except subprocess.TimeoutExpired:
        return 124, tr("delai depasse")
    return result.returncode, (result.stdout + result.stderr).strip()


def last_line(text):
    lines = [line for line in text.splitlines() if line.strip()]
    return lines[-1].strip() if lines else ""


class Checker:
    def __init__(self, password, external_name):
        self.password = password
        self.external_name = external_name
        self.results = []
        self.ccache_dir = None
        self.is_root = os.geteuid() == 0
        self.realm = self._parameter("realm").upper()
        self.workgroup = self._parameter("workgroup").upper()
        self.fqdn = socket.getfqdn().lower()
        self.local_addresses = self._local_addresses()

    # --- outils -----------------------------------------------------------
    def add(self, category, title, status, detail=""):
        self.results.append({"category": tr(category), "title": title, "status": status, "detail": detail})

    @staticmethod
    def _parameter(name):
        code, output = run(["testparm", "-s", "--parameter-name=" + name], timeout=15)
        return output.splitlines()[-1].strip() if code == 0 and output else ""

    @staticmethod
    def _local_addresses():
        code, output = run(["ip", "-o", "-4", "address", "show", "scope", "global"], timeout=5)
        return sorted(set(re.findall(r"inet (\d+\.\d+\.\d+\.\d+)/", output))) if code == 0 else []

    def _host(self, record_type, name, server="127.0.0.1"):
        return run(["host", "-W", "3", "-t", record_type, name, server], timeout=10)

    def _root_only(self, category, title):
        if self.is_root:
            return False
        self.add(category, title, SKIP, tr("Ce test exige root : sudo sambaad-check"))
        return True

    # --- tests --------------------------------------------------------------
    def check_services(self):
        for service in ("samba-ad-dc", "samba-adws", "chrony"):
            code, output = run(["systemctl", "is-active", service], timeout=10)
            status = OK if code == 0 else (WARN if service == "samba-adws" else FAIL)
            self.add("Services", tr("Service {}").format(service), status,
                     tr("actif") if code == 0 else tr("inactif ({})").format(output or code))

    def check_configuration(self):
        role = self._parameter("server role")
        self.add("Configuration", tr("Role du serveur"),
                 OK if role.lower() == "active directory domain controller" else FAIL, role)
        self.add("Configuration", tr("Royaume et domaine NetBIOS"),
                 OK if self.realm and self.workgroup else FAIL, f"{self.realm} / {self.workgroup}")
        if not self._root_only("Configuration", tr("Niveau fonctionnel")):
            code, output = run(["samba-tool", "domain", "level", "show"], timeout=30)
            level = next((line.split(":", 1)[1].strip() for line in output.splitlines()
                          if line.startswith("Domain function level")), last_line(output))
            self.add("Configuration", tr("Niveau fonctionnel"), OK if code == 0 else FAIL, level)

    def check_domain_dns(self):
        realm = self.realm.lower()
        records = (
            ("SOA", realm),
            ("SRV", f"_ldap._tcp.{realm}"),
            ("SRV", f"_ldap._tcp.dc._msdcs.{realm}"),
            ("SRV", f"_kerberos._tcp.{realm}"),
            ("SRV", f"_kerberos._udp.{realm}"),
            ("SRV", f"_kpasswd._tcp.{realm}"),
            ("SRV", f"_gc._tcp.{realm}"),
        )
        for record_type, name in records:
            code, output = self._host(record_type, name)
            self.add("DNS du domaine", tr("Enregistrement {} {}").format(record_type, name),
                     OK if code == 0 else FAIL, last_line(output) if code == 0 else tr("absent : {}").format(last_line(output)))
        code, output = self._host("A", self.fqdn)
        addresses = re.findall(r"has address (\S+)", output)
        if code != 0 or not addresses:
            self.add("DNS du domaine", tr("Nom {} -> {}").format(self.fqdn, "?"), FAIL,
                     tr("absent : {}").format(last_line(output)))
        else:
            matching = [address for address in addresses if address in self.local_addresses]
            self.add("DNS du domaine", tr("Nom {} -> {}").format(self.fqdn, ", ".join(addresses)),
                     OK if matching else FAIL,
                     "" if matching else tr("{} ne correspond a aucune adresse locale ({})").format(
                         ", ".join(addresses), ", ".join(self.local_addresses)))

    def check_dns_forwarding(self):
        forwarders = self._parameter("dns forwarder").replace(",", " ").split()
        if not forwarders:
            self.add("Redirection DNS", tr("Redirecteurs configures"), FAIL,
                     tr("aucun redirecteur : les noms externes ne seront pas resolus"))
        else:
            loops = [f for f in forwarders if f in self.local_addresses or f.startswith("127.") or f == "::1"]
            self.add("Redirection DNS", tr("Redirecteurs configures"), FAIL if loops else OK,
                     "; ".join(tr("{} designe ce controleur : boucle DNS").format(f) for f in loops) or " ".join(forwarders))
            for forwarder in forwarders:
                if forwarder in loops:
                    continue
                code, output = self._host("A", self.external_name, forwarder)
                self.add("Redirection DNS", tr("Redirecteur {} joignable").format(forwarder), OK if code == 0 else FAIL,
                         last_line(output) if code == 0 else tr("{} ne repond pas pour {}").format(forwarder, self.external_name))
        code, output = self._host("A", self.external_name)
        self.add("Redirection DNS", tr("Resolution externe via Samba ({})").format(self.external_name),
                 OK if code == 0 else FAIL,
                 last_line(output) if code == 0 else tr("{} non resolu par le DNS local : {}").format(
                     self.external_name, last_line(output)))
        servers = []
        try:
            servers = [line.split()[1] for line in Path("/etc/resolv.conf").read_text().splitlines()
                       if line.startswith("nameserver") and len(line.split()) > 1]
        except OSError:
            pass
        code, output = self._host("SRV", f"_ldap._tcp.dc._msdcs.{self.realm.lower()}")
        dc_names = re.findall(r"has SRV record \S+ \S+ \S+ (\S+?)\.?$", output, re.M)
        dc_addresses = set(self.local_addresses) | {"127.0.0.1"}
        for name in dc_names:
            dc_addresses.update(re.findall(r"has address (\S+)", self._host("A", name)[1]))
        self.add("Redirection DNS", tr("Serveurs DNS du systeme (/etc/resolv.conf)"),
                 OK if any(server in dc_addresses for server in servers) else WARN,
                 " ".join(servers) if any(server in dc_addresses for server in servers)
                 else tr("aucun ne designe ce controleur ni un autre DC") + f" ({' '.join(servers)})")

    def check_ports(self):
        code, output = run(["ss", "-H", "-ltnu"], timeout=10)
        listening = set()
        for line in output.splitlines():
            fields = line.split()
            if len(fields) >= 5:
                port = fields[4].rsplit(":", 1)[-1]
                if port.isdigit():
                    listening.add((fields[0], int(port)))
        for protocol, ports in (("tcp", TCP_PORTS), ("udp", UDP_PORTS)):
            for port, name in ports.items():
                ok = (protocol, port) in listening
                self.add("Ports", tr("Port {} {} ({}) a l'ecoute").format(protocol.upper(), port, name),
                         OK if ok else FAIL, "" if ok else tr("personne n'ecoute"))

    def check_firewall(self):
        if self._root_only("Pare-feu", tr("Regles nftables")):
            return
        code, output = run(["nft", "list", "ruleset"], timeout=15)
        if code != 0:
            self.add("Pare-feu", tr("Regles nftables"), WARN, tr("nftables illisible : {}").format(last_line(output)))
            return
        if not re.search(r"hook input .*policy drop", output):
            self.add("Pare-feu", tr("Regles nftables"), OK, tr("pas de politique de blocage en entree : tout est ouvert"))
            return
        accepted = {"tcp": [], "udp": []}
        for line in output.splitlines():
            match = re.search(r"\b(tcp|udp) dport (\{[^}]*\}|\S+).*\baccept\b", line)
            if not match or "saddr" in line:
                continue
            for item in re.findall(r"\d+(?:-\d+)?", match.group(2)):
                low, _, high = item.partition("-")
                accepted[match.group(1)].append((int(low), int(high or low)))
        for protocol, ports in (("tcp", TCP_PORTS), ("udp", UDP_PORTS)):
            for port, name in ports.items():
                ok = any(low <= port <= high for low, high in accepted[protocol])
                self.add("Pare-feu", tr("Port {} {} ({}) autorise").format(protocol.upper(), port, name),
                         OK if ok else FAIL, "" if ok else tr("aucune regle d'acceptation trouvee"))

    def check_time(self):
        code, output = run(["chronyc", "tracking"], timeout=10)
        leap = next((line.split(":", 1)[1].strip() for line in output.splitlines() if line.startswith("Leap status")), "")
        self.add("Heure", tr("Synchronisation Chrony"), OK if code == 0 and leap == "Normal" else WARN,
                 leap or last_line(output))
        path = Path("/var/lib/samba/ntp_signd")
        try:
            info = path.stat()
            mode = oct(info.st_mode & 0o777)[2:]
            owner = Path(path).owner()
            group = Path(path).group()
            ok = mode == "750" and owner == "root" and group == "_chrony"
            self.add("Heure", tr("Socket ntp_signd pour l'heure signee"), OK if ok else WARN,
                     tr("droits {} {}:{} (attendu 0750 root:_chrony)").format(mode, owner, group) if not ok else str(path))
        except OSError as error:
            self.add("Heure", tr("Socket ntp_signd pour l'heure signee"), WARN, str(error))

    def check_kerberos_and_shares(self):
        principal = f"Administrator@{self.realm}"
        if not self.password:
            self.add("Kerberos", tr("Ticket Kerberos pour {}").format(principal), SKIP, tr("mot de passe non fourni"))
            for share in ("sysvol", "netlogon"):
                self.add("Partages", tr("Partage {} accessible en Kerberos").format(share), SKIP, tr("pas de ticket Kerberos"))
            return
        self.ccache_dir = tempfile.mkdtemp(prefix="sambaad-check-")
        ccache = f"FILE:{self.ccache_dir}/krb5cc"
        environment = dict(os.environ, KRB5CCNAME=ccache)
        code, output = run(["kinit", principal], input_text=self.password + "\n", env=environment, timeout=20)
        self.add("Kerberos", tr("Ticket Kerberos pour {}").format(principal), OK if code == 0 else FAIL,
                 "" if code == 0 else last_line(output))
        for share in ("sysvol", "netlogon"):
            if code != 0:
                self.add("Partages", tr("Partage {} accessible en Kerberos").format(share), SKIP, tr("pas de ticket Kerberos"))
                continue
            share_code, share_output = run(
                ["smbclient", f"//{self.fqdn}/{share}", "--use-kerberos=required",
                 f"--use-krb5-ccache={ccache}", "-c", "ls"], timeout=30)
            self.add("Partages", tr("Partage {} accessible en Kerberos").format(share),
                     OK if share_code == 0 else FAIL, "" if share_code == 0 else last_line(share_output))
        run(["kdestroy"], env=environment, timeout=10)

    def check_directory(self):
        code, output = run(["samba-tool", "domain", "info", "127.0.0.1"], timeout=20)
        self.add("Annuaire", tr("Reponse CLDAP du controleur"), OK if code == 0 else FAIL,
                 " / ".join(line.strip() for line in output.splitlines()[:2]) if code == 0 else last_line(output))
        code, output = run(["openssl", "s_client", "-connect", "127.0.0.1:636", "-brief"],
                           input_text="", timeout=15)
        handshake = "CONNECTION ESTABLISHED" in output or "Protocol version" in output
        subject = next((line.split(":", 1)[1].strip() for line in output.splitlines()
                        if line.startswith("Peer certificate")), "")
        self.add("Annuaire", tr("Certificat LDAPS (port 636)"), OK if handshake else FAIL,
                 subject or last_line(output))
        if not self._root_only("Annuaire", tr("Coherence de la base (dbcheck)")):
            code, output = run(["samba-tool", "dbcheck", "--cross-ncs"], timeout=600)
            self.add("Annuaire", tr("Coherence de la base (dbcheck)"), OK if code == 0 else FAIL, last_line(output))
        if not self._root_only("Annuaire", tr("Droits SYSVOL (ntacl sysvolcheck)")):
            code, output = run(["samba-tool", "ntacl", "sysvolcheck"], timeout=120)
            self.add("Annuaire", tr("Droits SYSVOL (ntacl sysvolcheck)"), OK if code == 0 else FAIL,
                     last_line(output) or "OK")
        if not self._root_only("Annuaire", tr("Roles FSMO")):
            code, output = run(["samba-tool", "fsmo", "show"], timeout=30)
            owners = sorted({m for m in re.findall(r"CN=NTDS Settings,CN=([^,]+)", output)})
            self.add("Annuaire", tr("Roles FSMO"), OK if code == 0 else FAIL,
                     ", ".join(owners) if code == 0 else last_line(output))

    def check_replication(self):
        code, output = self._host("SRV", f"_ldap._tcp.dc._msdcs.{self.realm.lower()}")
        controllers = set(re.findall(r"has SRV record \S+ \S+ \S+ (\S+?)\.?$", output, re.M))
        if len(controllers) <= 1:
            self.add("Replication", tr("Etat de la replication"), OK, tr("un seul controleur : pas de replication"))
            return
        if self._root_only("Replication", tr("Etat de la replication")):
            return
        code, output = run(["samba-tool", "drs", "showrepl", "--summary"], timeout=120)
        self.add("Replication", tr("Etat de la replication"),
                 OK if code == 0 and "ALL GOOD" in output else FAIL, last_line(output))

    def check_gpo(self):
        if self._root_only("GPO", tr("GPO par defaut (samba-dcgpofix --check)")):
            return
        code, output = run(["/usr/sbin/samba-dcgpofix", "--check"], timeout=120)
        self.add("GPO", tr("GPO par defaut (samba-dcgpofix --check)"), OK if code == 0 else FAIL,
                 last_line(output) if code == 0 else tr(
                     "ecarts detectes : relancez SambaADPromo, Corriger le durcissement et les GPO"))

    def run_all(self):
        self.check_services()
        self.check_configuration()
        self.check_domain_dns()
        self.check_dns_forwarding()
        self.check_ports()
        self.check_firewall()
        self.check_time()
        self.check_kerberos_and_shares()
        self.check_directory()
        self.check_replication()
        self.check_gpo()
        if self.ccache_dir:
            for item in Path(self.ccache_dir).iterdir():
                item.unlink()
            Path(self.ccache_dir).rmdir()
        return self.results


def summary(results):
    counts = {status: sum(1 for r in results if r["status"] == status) for status in (OK, WARN, FAIL, SKIP)}
    return counts, tr("{} correct(s), {} avertissement(s), {} echec(s), {} ignore(s)").format(
        counts[OK], counts[WARN], counts[FAIL], counts[SKIP])


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--json", action="store_true", help="resultats au format JSON")
    parser.add_argument("--password-stdin", action="store_true",
                        help="lit le mot de passe Administrator sur l'entree standard")
    parser.add_argument("--external-name", default="debian.org",
                        help="nom externe resolu pour tester la redirection DNS (defaut : debian.org)")
    args = parser.parse_args()
    password = sys.stdin.readline().rstrip("\n") if args.password_stdin else ""

    if not Path("/var/lib/samba/private/sam.ldb").exists() and not Path("/etc/samba/smb.conf").exists():
        message = tr("Ce serveur n'est pas un controleur de domaine Samba AD.")
        print(json.dumps({"error": message}) if args.json else message)
        return 2

    checker = Checker(password, args.external_name)
    results = checker.run_all()
    counts, text = summary(results)
    if args.json:
        print(json.dumps({
            "controller": checker.fqdn, "realm": checker.realm, "language": LANGUAGE,
            "results": results, "summary": text, "counts": counts,
        }, ensure_ascii=False))
    else:
        labels = {OK: tr("OK"), WARN: tr("AVERT."), FAIL: tr("ECHEC"), SKIP: tr("IGNORE")}
        print(tr("Controleur {} - domaine {}").format(checker.fqdn, checker.realm))
        category = None
        for result in results:
            if result["category"] != category:
                category = result["category"]
                print(f"\n[{category}]")
            detail = f" - {result['detail']}" if result["detail"] else ""
            print(f"  {labels[result['status']]:7} {result['title']}{detail}")
        print("\n" + text)
    return 2 if counts[FAIL] else 1 if counts[WARN] else 0


if __name__ == "__main__":
    sys.exit(main())

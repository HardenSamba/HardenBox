#!/usr/bin/python3

import ipaddress
import json
import os
import queue
import re
import socket
import subprocess
import tempfile
import threading
import tkinter as tk
from pathlib import Path
from tkinter import messagebox, ttk

# Traductions anglaises de l'interface, indexees par le texte francais.
EN = {
    "Configuration de deploiement": "Deployment configuration",
    "Options du controleur": "Controller options",
    "Configuration reseau": "Network configuration",
    "Options de durcissement": "Hardening options",
    "Verification des options": "Review options",
    "Les fichiers PostWindows2003_schema*.ldf d'Exchange 2019 sont absents de {}.\n\nCopiez le dossier Setup/Data d'Exchange dans Packages/HB-SambaAD/exchange-data, puis reconstruisez l'ISO.":
        "The Exchange 2019 PostWindows2003_schema*.ldf files are missing from {}.\n\nCopy the Exchange Setup/Data folder to Packages/HB-SambaAD/exchange-data, then rebuild the ISO.",
    "Domaine actif detecte : les options de durcissement peuvent etre modifiees.": "Active domain detected: the hardening options can be changed.",
    "Pret a configurer ce serveur.": "Ready to configure this server.",
    "Test DNS non effectue.": "DNS test not run.",
    "Configuration des services de domaine Active Directory": "Active Directory Domain Services configuration",
    "Assistant de promotion Samba AD DS": "Samba AD DS promotion wizard",
    "ETAPES": "STEPS",
    "Installer": "Install",
    "Suivant >": "Next >",
    "< Precedent": "< Back",
    "Annuler": "Cancel",
    "Selectionnez l'operation de deploiement et indiquez le domaine Active Directory.": "Select the deployment operation and enter the Active Directory domain.",
    "Operation de deploiement": "Deployment operation",
    "Creer une nouvelle foret": "Create a new forest",
    "Ajouter un controleur de domaine a un domaine existant": "Add a domain controller to an existing domain",
    "Domaine DNS (AD.ENTREPRISE.FR)": "DNS domain (AD.COMPANY.COM)",
    "Domaine NetBIOS (15 caracteres max.)": "NetBIOS domain (15 characters max.)",
    "Compte administrateur du domaine": "Domain administrator account",
    "Mot de passe du compte du domaine": "Domain account password",
    "Serveur DNS du domaine (IPv4)": "Domain DNS server (IPv4)",
    "Tester le domaine (DNS et authentification)": "Test the domain (DNS and authentication)",
    "Test du domaine non effectue.": "Domain test not run.",
    "Test DNS a effectuer.": "DNS test to run.",
    "Indiquez le domaine DNS, le compte du domaine, son mot de passe et le serveur DNS.": "Enter the DNS domain, the domain account, its password and the DNS server.",
    "Test de la resolution en cours...": "Testing name resolution...",
    "zone DNS": "DNS zone",
    "service LDAP": "LDAP service",
    "service Kerberos": "Kerberos service",
    "{} introuvable : {}": "{} not found: {}",
    "Domaine valide : ": "Valid domain: ",
    ", authentification Kerberos": ", Kerberos authentication",
    "Recherche du controleur impossible : {}": "Cannot look up the controller: {}",
    "Aucun controleur Kerberos joignable n'est annonce par ce serveur DNS.": "This DNS server does not announce any reachable Kerberos controller.",
    "Authentification Kerberos impossible : {}": "Kerberos authentication failed: {}",
    "Authentification de {} refusee : {}": "Authentication of {} refused: {}",
    "erreur inconnue": "unknown error",
    "Options du controleur de domaine": "Domain controller options",
    "Definissez les capacites du controleur et le mot de passe du compte Administrator.": "Set the controller capabilities and the password of the Administrator account.",
    "Nom du controleur": "Controller name",
    "Nom court, par exemple dc01": "Short name, for example dc01",
    "Niveau fonctionnel": "Functional level",
    "Serveur DNS": "DNS server",
    "Catalogue global (GC)": "Global catalog (GC)",
    "Mot de passe du compte Administrator": "Administrator account password",
    "Confirmer le mot de passe Administrator": "Confirm the Administrator password",
    "Indiquez trois serveurs DNS au plus.": "Enter three DNS servers at most.",
    "Serveur DNS invalide : {}": "Invalid DNS server: {}",
    "Indiquez au moins un redirecteur DNS.": "Enter at least one DNS forwarder.",
    "Redirecteur DNS invalide : {}": "Invalid DNS forwarder: {}",
    "Le redirecteur {} designe ce controleur : le DNS tournerait en boucle. Indiquez un DNS externe (passerelle, DNS du FAI ou public).":
        "The forwarder {} is this controller: DNS would loop. Enter an external DNS server (gateway, ISP or public DNS).",
    "Detecte automatiquement depuis le domaine rejoint": "Automatically detected from the joined domain",
    "Configuration DNS": "DNS configuration",
    "L'adresse du serveur est conservee ; le controleur utilisera son propre DNS.": "The server address is kept; the controller will use its own DNS.",
    "Adresse du serveur (inchangee)": "Server address (unchanged)",
    "Redirecteurs DNS (separes par un espace)": "DNS forwarders (space separated)",
    "Les ports Active Directory sont ouverts automatiquement a toutes les sources.": "Active Directory ports are opened automatically to every source.",
    "Selectionnez les protections et outils a appliquer au controleur de domaine.": "Select the protections and tools to apply to the domain controller.",
    "Operation de maintenance": "Maintenance operation",
    "Corriger le durcissement et les GPO par defaut": "Correct the hardening and the default GPOs",
    "Etendre uniquement le schema pour Exchange": "Only extend the schema for Exchange",
    "Retrograder ce controleur (il devient un serveur autonome)": "Demote this controller (it becomes a standalone server)",
    "Controleur qui recoit les roles FSMO": "Controller receiving the FSMO roles",
    "Backend MDB pour une nouvelle foret": "MDB backend for a new forest",
    "Durcissement SMB, NTLM, NetBIOS et impression": "SMB, NTLM, NetBIOS and printing hardening",
    "Integration Winbind dans NSS et PAM": "Winbind integration in NSS and PAM",
    "Journaux d'audit AD et SYSVOL": "AD and SYSVOL audit logs",
    "Sauvegarde locale AD apres validation": "Local AD backup after validation",
    "Controle local renforce des mots de passe": "Strengthened local password check",
    "OpenRSAT et ses dependances GTK": "OpenRSAT and its GTK dependencies",
    "Harden Group Policy Management (HGPM)": "Harden Group Policy Management (HGPM)",
    "Serveurs DNS de ce controleur": "DNS servers of this controller",
    "Tous les serveurs DNS utilises par ce serveur, 3 au plus (lui-meme et les autres DC)": "Every DNS server used by this server, 3 at most (itself and the other DCs)",
    "Etendre le schema Active Directory pour Exchange apres la promotion": "Extend the Active Directory schema for Exchange after the promotion",
    "Verifiez vos choix avant de lancer la promotion de ce serveur.": "Review your choices before promoting this server.",
    "Le nom NetBIOS doit compter 15 caracteres au plus : lettres majuscules, chiffres et tirets.": "The NetBIOS name must be 15 characters at most: uppercase letters, digits and hyphens.",
    "Testez avec succes le domaine (DNS et authentification) avant de continuer.": "Successfully test the domain (DNS and authentication) before continuing.",
    "Les mots de passe sont absents ou differents.": "The passwords are missing or do not match.",
    "Aucun autre controleur n'est annonce dans le domaine : le dernier controleur ne peut pas etre retrograde.": "No other controller is announced in the domain: the last controller cannot be demoted.",
    "Tous les champs de cette etape sont obligatoires.": "All the fields of this step are required.",
    "Operation": "Operation",
    "Schema Exchange uniquement": "Exchange schema only",
    "Retrograder ce controleur, roles FSMO vers {}": "Demote this controller, FSMO roles to {}",
    "Modifier le durcissement": "Change the hardening",
    "Ajouter un DC": "Add a DC",
    "Nouvelle foret": "New forest",
    "Domaine DNS": "DNS domain",
    "Domaine NetBIOS": "NetBIOS domain",
    "Lu dans le domaine apres la jonction": "Read from the domain after the join",
    "Compte du domaine": "Domain account",
    "DNS du domaine": "Domain DNS",
    "Sans objet": "Not applicable",
    "Controleur de domaine": "Domain controller",
    "Detecte depuis le domaine": "Detected from the domain",
    "Adresse du serveur": "Server address",
    "{} (inchangee)": "{} (unchanged)",
    "Redirecteur DNS": "DNS forwarder",
    "Pare-feu": "Firewall",
    "Ports AD ouverts a toutes les sources": "AD ports open to every source",
    "Durcissement": "Hardening",
    "Aucun": "None",
    "Schema Exchange": "Exchange schema",
    "Extension avec les LDF embarques": "Extension with the bundled LDF files",
    "Non demande": "Not requested",
    "RESUME DE LA CONFIGURATION\n\n": "CONFIGURATION SUMMARY\n\n",
    "Extension du schema Exchange en cours...": "Extending the Exchange schema...",
    "Promotion du serveur en cours...": "Promoting the server...",
    "Operation terminee.": "Operation completed.",
    "Confirmer l'installation": "Confirm the installation",
    "Le schema Active Directory sera etendu pour Exchange. Cette operation est irreversible. Continuer ?":
        "The Active Directory schema will be extended for Exchange. This cannot be undone. Continue?",
    "Les options de durcissement vont etre appliquees et les deux GPO par defaut seront sauvegardees puis remises a leur etat Windows par defaut. Continuer ?":
        "The hardening options will be applied and the two default GPOs will be backed up then reset to their Windows defaults. Continue?",
    "Le serveur va etre promu en controleur de domaine Samba AD. Continuer ?": "The server will be promoted to a Samba AD domain controller. Continue?",
    "Confirmer la retrogradation": "Confirm the demotion",
    "Les roles FSMO vont etre transferes a {}, puis ce serveur ne sera plus controleur de domaine : Samba AD sera arrete et sa configuration retiree (une sauvegarde est conservee). Cette operation est irreversible. Continuer ?":
        "The FSMO roles will be transferred to {}, then this server will no longer be a domain controller: Samba AD will be stopped and its configuration removed (a backup is kept). This cannot be undone. Continue?",
    "Controleur retrograde : ce serveur est maintenant autonome. Redemarrez la machine.": "Controller demoted: this server is now standalone. Restart the machine.",
    "Schema Active Directory etendu pour Exchange avec succes.": "Active Directory schema successfully extended for Exchange.",
    "Durcissement applique et GPO par defaut corrigees avec succes.": "Hardening applied and default GPOs successfully corrected.",
    "Promotion terminee avec succes. Redemarrez la machine.": "Promotion completed successfully. Restart the machine.",
    "La retrogradation a echoue.": "The demotion failed.",
    "La promotion a echoue.": "The promotion failed.",
    "\n\n{}\n\nJournal complet : /var/log/hardenbox/": "\n\n{}\n\nFull log: /var/log/hardenbox/",
    "SambaADPromo - Configuration AD DS": "SambaADPromo - AD DS configuration",
    "Produit de la communaute HardenAD": "A HardenAD community product",
    "Afficher le smb.conf": "Show smb.conf",
    "Fermer": "Close",
    "Contenu de /etc/samba/smb.conf": "Content of /etc/samba/smb.conf",
    "Fichier qui sera installe, produit depuis le modele {}.": "File that will be installed, generated from the template {}.",
    "Les valeurs entre < > seront lues dans le domaine pendant la jonction.": "Values between < > will be read from the domain during the join.",
    "<lu dans le domaine>": "<read from the domain>",
    "<niveau du domaine>": "<domain level>",
    "Apercu du smb.conf impossible : {}": "Cannot preview smb.conf: {}",
}


def detect_language():
    """Anglais si la langue du systeme est l'anglais, francais sinon (#14)."""
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
    """Texte de l'interface dans la langue du systeme ; le francais est la reference."""
    return EN.get(text, text) if LANGUAGE == "en" else text


STEPS = (
    tr('Configuration de deploiement'),
    tr('Options du controleur'),
    tr('Configuration reseau'),
    tr('Options de durcissement'),
    tr('Verification des options'),
)


def system_defaults():
    defaults = {
        "realm": "AD.EXEMPLE.FR",
        "domain": "EXEMPLE",
        "domain-dns": "",
        "hostname": "dc01",
        "interface": "ens18",
        "ip-address": "192.168.10.10/24",
        "gateway": "192.168.10.1",
        "dns-forwarder": "1.1.1.1",
        "allowed-cidr": "192.168.10.0/24",
    }
    fqdn = socket.gethostname().strip().lower()
    if fqdn:
        defaults["hostname"] = fqdn.split(".", 1)[0]

    resolv_lines = []
    try:
        resolv_lines = Path("/etc/resolv.conf").read_text().splitlines()
    except OSError:
        pass
    search_domain = ""
    nameservers = []
    for line in resolv_lines:
        fields = line.split()
        if len(fields) >= 2 and fields[0] == "search":
            search_domain = fields[1].rstrip(".").lower()
        elif len(fields) == 2 and fields[0] == "nameserver":
            try:
                address = ipaddress.ip_address(fields[1])
            except ValueError:
                continue
            # 127.0.0.1 designe Samba lui-meme apres la promotion.
            if address.version == 4 and not address.is_loopback and fields[1] not in nameservers:
                nameservers.append(fields[1])
    if nameservers:
        defaults["dns-forwarder"] = " ".join(nameservers)
        defaults["domain-dns"] = nameservers[0]
    try:
        configured = subprocess.run(
            ["testparm", "-s", "--parameter-name=dns forwarder"],
            text=True,
            capture_output=True,
            timeout=5,
            check=False,
        ).stdout.strip()
    except (OSError, subprocess.TimeoutExpired):
        configured = ""
    if configured:
        defaults["dns-forwarder"] = configured

    realm = fqdn.split(".", 1)[1] if "." in fqdn else search_domain
    if realm:
        defaults["realm"] = realm.upper()
        defaults["domain"] = realm.split(".", 1)[0].upper()[:15]

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
        if route.get("gateway"):
            defaults["gateway"] = route["gateway"]

        links = json.loads(subprocess.run(
            ["ip", "-j", "-4", "address", "show", "dev", route["dev"], "scope", "global"],
            text=True,
            capture_output=True,
            timeout=3,
            check=True,
        ).stdout)
        addresses = links[0].get("addr_info", [])
        address = next(item for item in addresses if item.get("family") == "inet")
        cidr = f'{address["local"]}/{address["prefixlen"]}'
        defaults["ip-address"] = cidr
        defaults["allowed-cidr"] = str(ipaddress.ip_interface(cidr).network)
    except (IndexError, KeyError, OSError, StopIteration, subprocess.SubprocessError, ValueError):
        pass
    return defaults


def current_dns_servers():
    """Serveurs DNS de /etc/resolv.conf (IPv4), dans l'ordre."""
    servers = []
    try:
        for line in Path("/etc/resolv.conf").read_text().splitlines():
            fields = line.split()
            if len(fields) == 2 and fields[0] == "nameserver":
                try:
                    if ipaddress.ip_address(fields[1]).version == 4 and fields[1] not in servers:
                        servers.append(fields[1])
                except ValueError:
                    continue
    except OSError:
        pass
    return " ".join(servers[:3])


def other_domain_controllers(realm):
    """Autres controleurs du domaine, annonces dans le DNS local (#14)."""
    local = socket.getfqdn().lower()
    try:
        result = subprocess.run(
            ["host", "-W", "3", "-t", "SRV", f"_ldap._tcp.dc._msdcs.{realm.lower()}", "127.0.0.1"],
            text=True, capture_output=True, timeout=5, check=False,
        )
    except (OSError, subprocess.TimeoutExpired):
        return []
    names = sorted({
        line.split()[-1].rstrip(".").lower()
        for line in result.stdout.splitlines() if "has SRV record" in line
    })
    return [name for name in names if name != local]


def domain_is_provisioned():
    try:
        service = subprocess.run(
            ["systemctl", "is-active", "--quiet", "samba-ad-dc"],
            timeout=5,
            check=False,
        )
        role = subprocess.run(
            ["testparm", "-s", "--parameter-name=server role"],
            text=True,
            capture_output=True,
            timeout=5,
            check=False,
        )
        return service.returncode == 0 and role.stdout.strip().lower() == "active directory domain controller"
    except (OSError, subprocess.TimeoutExpired):
        return False


# LDF Exchange 2019 embarques par hb-sambaad (Packages/HB-SambaAD/exchange-data).
EXCHANGE_LDF_DIR = Path("/usr/share/hardenbox/exchange-data")
# Modele et moteur de rendu du smb.conf, fournis par hb-sambaad et partages avec
# provision-samba-ad.sh : l'apercu est le fichier qui sera installe.
SMB_CONF_RENDERER = "/usr/lib/hardenbox/render-smb-conf"
SMB_CONF_TEMPLATE = "/usr/share/hardenbox/samba/smb.conf.template"
EXCHANGE_MISSING_MESSAGE = (
    tr("Les fichiers PostWindows2003_schema*.ldf d'Exchange 2019 sont absents de {}.\n\nCopiez le dossier Setup/Data d'Exchange dans Packages/HB-SambaAD/exchange-data, puis reconstruisez l'ISO.").format(EXCHANGE_LDF_DIR)
)


def exchange_ldf_available():
    if not EXCHANGE_LDF_DIR.is_dir():
        return False
    return any(
        entry.is_file() and re.fullmatch(r"PostWindows2003_schema\d+\.ldf", entry.name, re.IGNORECASE)
        for entry in EXCHANGE_LDF_DIR.iterdir()
    )


class PromoApp(tk.Frame):
    def __init__(self, master):
        super().__init__(master, background="#f3f4f6")
        self.master = master
        self.current_step = 0
        self.pages = []
        self.step_labels = []
        self.resume_mode = domain_is_provisioned()
        self.completed = False
        detected = system_defaults()
        self.values = {
            "deployment": tk.StringVar(value="forest"),
            "realm": tk.StringVar(value=detected["realm"]),
            "domain": tk.StringVar(value=detected["domain"]),
            "domain-admin": tk.StringVar(value="Administrator"),
            "domain-dns": tk.StringVar(value=detected["domain-dns"]),
            "hostname": tk.StringVar(value=detected["hostname"]),
            "interface": tk.StringVar(value=detected["interface"]),
            "ip-address": tk.StringVar(value=detected["ip-address"]),
            "gateway": tk.StringVar(value=detected["gateway"]),
            "dns-forwarder": tk.StringVar(value=detected["dns-forwarder"]),
            "allowed-cidr": tk.StringVar(value=detected["allowed-cidr"]),
            "function-level": tk.StringVar(value="2016"),
        }
        self.password = tk.StringVar()
        self.password_again = tk.StringVar()
        # Mot de passe du compte qui joint ce DC au domaine existant (#14).
        self.domain_password = tk.StringVar()
        self.maintenance_mode = tk.StringVar(value="correction")
        self.dns_servers = tk.StringVar(value=current_dns_servers())
        self.demote_target = tk.StringVar()
        self.exchange_schema = tk.BooleanVar(value=False)
        self.hardening = {
            "mdb": tk.BooleanVar(value=True),
            "smb": tk.BooleanVar(value=True),
            "winbind": tk.BooleanVar(value=True),
            "audit": tk.BooleanVar(value=True),
            "backup": tk.BooleanVar(value=True),
            "password": tk.BooleanVar(value=False),
            "openrsat": tk.BooleanVar(value=True),
            "powershell": tk.BooleanVar(value=True),
            "hgpm": tk.BooleanVar(value=True),
        }
        initial_status = (
            tr('Domaine actif detecte : les options de durcissement peuvent etre modifiees.')
            if self.resume_mode else tr('Pret a configurer ce serveur.')
        )
        self.status = tk.StringVar(value=initial_status)
        self.dns_status = tk.StringVar(value=tr('Test DNS non effectue.'))
        self.dns_test_ok = False
        self.dns_test_generation = 0
        self.result_queue = queue.Queue()
        self.dns_result_queue = queue.Queue()
        self._configure_styles()
        self._build()
        self.pack(fill="both", expand=True)
        self.show_step(0)

    def _configure_styles(self):
        style = ttk.Style(self.master)
        style.theme_use("clam")
        style.configure("Wizard.TEntry", fieldbackground="#ffffff", padding=7)
        style.configure("Wizard.TCombobox", fieldbackground="#ffffff", padding=6)
        style.configure("Primary.TButton", background="#0078d4", foreground="#ffffff", padding=(18, 8))
        style.map("Primary.TButton", background=[("active", "#106ebe"), ("disabled", "#a7c7e7")])
        style.configure("Secondary.TButton", background="#ffffff", foreground="#202020", padding=(16, 8))
        style.map("Secondary.TButton", background=[("active", "#e5e7eb")])
        style.configure("Wizard.Horizontal.TProgressbar", troughcolor="#d9e2ec", background="#0078d4")

    def _build(self):
        self.grid_rowconfigure(1, weight=1)
        self.grid_columnconfigure(0, minsize=250)
        self.grid_columnconfigure(1, weight=1)
        self._build_header()
        self._build_sidebar()
        self._build_content()
        # Mention de la communaute, en bas de la fenetre.
        tk.Label(self, text=tr("Produit de la communaute HardenAD"), background="#e9edf2", foreground="#5f6b7a",
                 font=("Segoe UI", 8), pady=4).grid(row=2, column=0, columnspan=2, sticky="ew")

    def _build_header(self):
        header = tk.Frame(self, background="#ffffff", height=82, highlightbackground="#d8dde3", highlightthickness=1)
        header.grid(row=0, column=0, columnspan=2, sticky="nsew")
        header.grid_propagate(False)
        tk.Label(
            header,
            text=tr('Configuration des services de domaine Active Directory'),
            background="#ffffff",
            foreground="#1f2937",
            font=("Segoe UI", 16, "bold"),
        ).pack(anchor="w", padx=28, pady=(17, 2))
        tk.Label(
            header,
            text=tr('Assistant de promotion Samba AD DS'),
            background="#ffffff",
            foreground="#5f6b7a",
            font=("Segoe UI", 9),
        ).pack(anchor="w", padx=29)

    def _build_sidebar(self):
        sidebar = tk.Frame(self, background="#243447", width=250)
        sidebar.grid(row=1, column=0, sticky="nsew")
        sidebar.grid_propagate(False)
        tk.Label(
            sidebar,
            text=tr('ETAPES'),
            background="#243447",
            foreground="#9fb3c8",
            font=("Segoe UI", 9, "bold"),
        ).pack(anchor="w", padx=24, pady=(27, 13))
        for index, title in enumerate(STEPS):
            row = tk.Frame(sidebar, background="#243447", height=48)
            row.pack(fill="x")
            row.pack_propagate(False)
            number = tk.Label(
                row,
                text=str(index + 1),
                width=2,
                background="#3e5368",
                foreground="#ffffff",
                font=("Segoe UI", 9, "bold"),
            )
            number.pack(side="left", padx=(20, 10), pady=11)
            label = tk.Label(
                row,
                text=title,
                anchor="w",
                background="#243447",
                foreground="#d9e2ec",
                font=("Segoe UI", 9),
            )
            label.pack(side="left", fill="x", expand=True, padx=(0, 8))
            self.step_labels.append((row, number, label))

    def _build_content(self):
        content = tk.Frame(self, background="#f3f4f6")
        content.grid(row=1, column=1, sticky="nsew")
        content.grid_rowconfigure(0, weight=1)
        content.grid_columnconfigure(0, weight=1)
        self.page_container = tk.Frame(content, background="#f3f4f6")
        self.page_container.grid(row=0, column=0, sticky="nsew", padx=38, pady=(26, 15))
        self.page_container.grid_rowconfigure(0, weight=1)
        self.page_container.grid_columnconfigure(0, weight=1)
        self._build_deployment_page()
        self._build_controller_page()
        self._build_network_page()
        self._build_hardening_page()
        self._build_review_page()
        self._build_footer(content)

    def _build_footer(self, content):
        footer = tk.Frame(content, background="#ffffff", height=73, highlightbackground="#d8dde3", highlightthickness=1)
        footer.grid(row=1, column=0, sticky="ew")
        footer.grid_propagate(False)
        self.progress = ttk.Progressbar(footer, style="Wizard.Horizontal.TProgressbar", mode="indeterminate", length=130)
        tk.Label(
            footer,
            textvariable=self.status,
            background="#ffffff",
            foreground="#5f6b7a",
            font=("Segoe UI", 9),
        ).pack(side="left", padx=24, pady=24)
        self.install_button = ttk.Button(footer, text=tr('Installer'), style="Primary.TButton", command=self.promote)
        self.next_button = ttk.Button(footer, text=tr('Suivant >'), style="Primary.TButton", command=self.next_step)
        self.back_button = ttk.Button(footer, text=tr('< Precedent'), style="Secondary.TButton", command=self.previous_step)
        self.cancel_button = ttk.Button(footer, text=tr('Annuler'), style="Secondary.TButton", command=self.master.destroy)
        self.cancel_button.pack(side="right", padx=(6, 20), pady=17)
        self.next_button.pack(side="right", padx=6, pady=17)
        self.back_button.pack(side="right", padx=6, pady=17)

    def _new_page(self, title, subtitle):
        page = tk.Frame(self.page_container, background="#f3f4f6")
        page.grid(row=0, column=0, sticky="nsew")
        page.grid_columnconfigure(0, weight=1)
        tk.Label(
            page,
            text=title,
            background="#f3f4f6",
            foreground="#1f2937",
            font=("Segoe UI", 15, "bold"),
        ).grid(row=0, column=0, sticky="w")
        tk.Label(
            page,
            text=subtitle,
            background="#f3f4f6",
            foreground="#5f6b7a",
            font=("Segoe UI", 9),
            justify="left",
        ).grid(row=1, column=0, sticky="w", pady=(4, 22))
        panel = tk.Frame(page, background="#ffffff", highlightbackground="#d8dde3", highlightthickness=1)
        panel.grid(row=2, column=0, sticky="nsew")
        panel.grid_columnconfigure(1, weight=1)
        self.pages.append(page)
        return panel

    def _field(self, panel, row, label, variable, help_text="", secret=False, disabled=False):
        tk.Label(
            panel,
            text=label,
            background="#ffffff",
            foreground="#202020",
            font=("Segoe UI", 9),
        ).grid(row=row, column=0, sticky="w", padx=(24, 18), pady=(13, 3))
        entry = ttk.Entry(panel, textvariable=variable, show="*" if secret else "", style="Wizard.TEntry")
        entry.grid(row=row, column=1, sticky="ew", padx=(0, 24), pady=(13, 3))
        if disabled:
            entry.state(["disabled"])
        if help_text:
            tk.Label(
                panel,
                text=help_text,
                background="#ffffff",
                foreground="#6b7280",
                font=("Segoe UI", 8),
            ).grid(row=row + 1, column=1, sticky="w", padx=(0, 24), pady=(0, 5))
        return entry

    def _build_deployment_page(self):
        panel = self._new_page(
            tr('Configuration de deploiement'),
            tr("Selectionnez l'operation de deploiement et indiquez le domaine Active Directory."),
        )
        tk.Label(
            panel,
            text=tr('Operation de deploiement'),
            background="#ffffff",
            foreground="#202020",
            font=("Segoe UI", 9, "bold"),
        ).grid(row=0, column=0, columnspan=2, sticky="w", padx=24, pady=(20, 8))
        ttk.Radiobutton(
            panel,
            text=tr('Creer une nouvelle foret'),
            variable=self.values["deployment"],
            value="forest",
            command=self._deployment_changed,
        ).grid(row=1, column=0, columnspan=2, sticky="w", padx=24)
        ttk.Radiobutton(
            panel,
            text=tr('Ajouter un controleur de domaine a un domaine existant'),
            variable=self.values["deployment"],
            value="dc",
            command=self._deployment_changed,
        ).grid(row=2, column=0, columnspan=2, sticky="w", padx=24, pady=(4, 0))
        self._field(
            panel, 3, tr('Domaine DNS (AD.ENTREPRISE.FR)'), self.values["realm"],
            disabled=self.resume_mode,
        )
        self.netbios_entry = self._field(
            panel, 4, tr('Domaine NetBIOS (15 caracteres max.)'), self.values["domain"],
            disabled=self.resume_mode,
        )
        # Le nom NetBIOS est toujours en majuscules (#14).
        self.values["domain"].trace_add("write", lambda *_: self._uppercase_netbios())

        self.join_panel = tk.Frame(panel, background="#eef5fb", highlightbackground="#b8d3e8", highlightthickness=1)
        self.join_panel.grid(row=5, column=0, columnspan=2, sticky="ew", padx=24, pady=(10, 16))
        self.join_panel.grid_columnconfigure(1, weight=1)
        self._field(
            self.join_panel,
            0,
            tr('Compte administrateur du domaine'),
            self.values["domain-admin"],
            disabled=self.resume_mode,
        )
        self._field(
            self.join_panel,
            1,
            tr('Mot de passe du compte du domaine'),
            self.domain_password,
            secret=True,
            disabled=self.resume_mode,
        )
        self._field(
            self.join_panel,
            2,
            tr('Serveur DNS du domaine (IPv4)'),
            self.values["domain-dns"],
            disabled=self.resume_mode,
        )
        test_row = tk.Frame(self.join_panel, background="#eef5fb")
        test_row.grid(row=3, column=0, columnspan=2, sticky="ew", padx=24, pady=(6, 12))
        self.dns_test_button = ttk.Button(
            test_row,
            text=tr('Tester le domaine (DNS et authentification)'),
            style="Secondary.TButton",
            command=self.test_dns,
        )
        self.dns_test_button.pack(side="left")
        self.dns_status_label = tk.Label(
            test_row,
            textvariable=self.dns_status,
            background="#eef5fb",
            foreground="#5f6b7a",
            font=("Segoe UI", 8),
        )
        self.dns_status_label.pack(side="left", padx=12)
        self.values["realm"].trace_add("write", self._dns_values_changed)
        self.values["domain-dns"].trace_add("write", self._dns_values_changed)
        self.values["domain-admin"].trace_add("write", self._dns_values_changed)
        self.domain_password.trace_add("write", self._dns_values_changed)
        self._deployment_changed()
        if self.resume_mode:
            self.dns_test_button.state(["disabled"])
            for child in panel.winfo_children():
                if isinstance(child, ttk.Radiobutton):
                    child.state(["disabled"])

    def _deployment_changed(self):
        self.dns_test_generation += 1
        self.dns_test_ok = False
        joining = self.values["deployment"].get() == "dc"
        if joining:
            self.join_panel.grid()
            self.dns_status.set(tr('Test du domaine non effectue.'))
        else:
            self.join_panel.grid_remove()
        # Le NetBIOS d'un domaine existant est lu apres la jonction (#14).
        if not self.resume_mode:
            self.netbios_entry.state(["disabled"] if joining else ["!disabled"])
            if joining:
                self.values["domain"].set(self.values["realm"].get().split(".", 1)[0][:15])
        if hasattr(self, "admin_password_frame"):
            if joining:
                self.admin_password_frame.grid_remove()
            else:
                self.admin_password_frame.grid()

    def _dns_values_changed(self, *_args):
        self.dns_test_generation += 1
        self.dns_test_ok = False
        if not self.resume_mode and self.values["deployment"].get() == "dc":
            self.values["domain"].set(self.values["realm"].get().split(".", 1)[0][:15])
        if hasattr(self, "dns_status"):
            self.dns_status.set(tr('Test DNS a effectuer.'))
            if hasattr(self, "dns_status_label"):
                self.dns_status_label.configure(foreground="#5f6b7a")

    def test_dns(self):
        realm = self.values["realm"].get().strip().lower()
        dns_server = self.values["domain-dns"].get().strip()
        account = self.values["domain-admin"].get().strip()
        password = self.domain_password.get()
        if not realm or not dns_server or not account or not password:
            messagebox.showerror(
                "SambaADPromo",
                tr('Indiquez le domaine DNS, le compte du domaine, son mot de passe et le serveur DNS.'),
            )
            return
        self.dns_test_ok = False
        self.dns_status.set(tr('Test de la resolution en cours...'))
        self.dns_status_label.configure(foreground="#5f6b7a")
        self.dns_test_button.state(["disabled"])
        self.dns_test_generation += 1
        generation = self.dns_test_generation
        threading.Thread(
            target=self._run_dns_test,
            args=(generation, realm, dns_server, account, password),
            daemon=True,
        ).start()
        self.master.after(100, self._poll_dns_test)

    def _run_dns_test(self, generation, realm, dns_server, account, password):
        queries = (
            (tr('zone DNS'), "SOA", realm),
            (tr('service LDAP'), "SRV", f"_ldap._tcp.dc._msdcs.{realm}"),
            (tr('service Kerberos'), "SRV", f"_kerberos._tcp.{realm}"),
        )
        results = []
        for label, record_type, name in queries:
            try:
                result = subprocess.run(
                    ["host", "-W", "3", "-t", record_type, name, dns_server],
                    text=True,
                    capture_output=True,
                    timeout=5,
                    check=False,
                )
            except (OSError, subprocess.TimeoutExpired) as error:
                self.dns_result_queue.put((generation, False, f"{label} : {error}"))
                return
            if result.returncode != 0:
                details = (result.stderr or result.stdout).strip()
                self.dns_result_queue.put((generation, False, tr('{} introuvable : {}').format(label, details)))
                return
            results.append(label)
        error = self._kerberos_test(realm, dns_server, account, password)
        if error:
            self.dns_result_queue.put((generation, False, error))
            return
        self.dns_result_queue.put(
            (generation, True, tr('Domaine valide : ') + ", ".join(results) + tr(', authentification Kerberos'))
        )

    @staticmethod
    def _kerberos_test(realm, dns_server, account, password):
        """Obtient un ticket Kerberos pour le compte du domaine aupres d'un DC
        decouvert par SRV, sans toucher a la configuration du systeme (#14)."""
        def lookup(record_type, name, pattern):
            result = subprocess.run(
                ["host", "-W", "3", "-t", record_type, name, dns_server],
                text=True, capture_output=True, timeout=5, check=False,
            )
            match = re.search(pattern, result.stdout)
            return match.group(1) if match else ""

        try:
            controller = lookup("SRV", f"_kerberos._tcp.{realm}", r"has SRV record \S+ \S+ \S+ (\S+?)\.?$")
            address = lookup("A", controller, r"has address (\S+)") if controller else ""
        except (OSError, subprocess.TimeoutExpired) as error:
            return tr('Recherche du controleur impossible : {}').format(error)
        if not address:
            return tr("Aucun controleur Kerberos joignable n'est annonce par ce serveur DNS.")
        user = account.rsplit("\\", 1)[-1]
        principal = user if "@" in user else f"{user}@{realm.upper()}"
        with tempfile.TemporaryDirectory(prefix="sambaadpromo-") as directory:
            config = Path(directory, "krb5.conf")
            config.write_text(
                f"[libdefaults]\n    default_realm = {realm.upper()}\n"
                "    dns_lookup_kdc = false\n    dns_lookup_realm = false\n\n"
                f"[realms]\n    {realm.upper()} = {{\n        kdc = {address}\n    }}\n"
            )
            environment = dict(os.environ, KRB5_CONFIG=str(config), KRB5CCNAME=f"FILE:{directory}/krb5cc")
            try:
                result = subprocess.run(
                    ["kinit", principal], input=password + "\n", text=True,
                    capture_output=True, timeout=20, check=False, env=environment,
                )
            except (OSError, subprocess.TimeoutExpired) as error:
                return tr('Authentification Kerberos impossible : {}').format(error)
        if result.returncode != 0:
            details = (result.stderr or result.stdout).strip().splitlines()
            return tr('Authentification de {} refusee : {}').format(principal, details[-1] if details else tr('erreur inconnue'))
        return ""

    def _poll_dns_test(self):
        try:
            generation, success, details = self.dns_result_queue.get_nowait()
        except queue.Empty:
            self.master.after(100, self._poll_dns_test)
            return
        self.dns_test_button.state(["!disabled"])
        if generation != self.dns_test_generation:
            return
        self.dns_test_ok = success
        self.dns_status.set(details)
        self.dns_status_label.configure(foreground="#16794c" if success else "#b42318")

    def _build_controller_page(self):
        panel = self._new_page(
            tr('Options du controleur de domaine'),
            tr('Definissez les capacites du controleur et le mot de passe du compte Administrator.'),
        )
        self._field(
            panel, 0, tr('Nom du controleur'), self.values["hostname"],
            tr('Nom court, par exemple dc01'), disabled=self.resume_mode,
        )
        tk.Label(panel, text=tr('Niveau fonctionnel'), background="#ffffff", foreground="#202020").grid(
            row=2, column=0, sticky="w", padx=(24, 18), pady=13
        )
        function_level = ttk.Combobox(
            panel,
            textvariable=self.values["function-level"],
            values=("2016", "2008_R2"),
            state="readonly",
            style="Wizard.TCombobox",
        )
        function_level.grid(row=2, column=1, sticky="ew", padx=(0, 24), pady=13)
        self.function_level_box = function_level
        self.function_level_hint = tk.StringVar()
        tk.Label(
            panel, textvariable=self.function_level_hint, background="#ffffff", foreground="#5f6b7a",
        ).grid(row=1, column=1, sticky="sw", padx=(0, 24))
        # Lors de l'ajout d'un DC, le niveau est celui du domaine rejoint (#14).
        self.values["deployment"].trace_add("write", lambda *_: self._update_function_level_state())
        self._update_function_level_state()
        options = tk.Frame(panel, background="#ffffff")
        options.grid(row=3, column=1, sticky="w", padx=(0, 24), pady=(0, 8))
        dns = tk.BooleanVar(value=True)
        catalog = tk.BooleanVar(value=True)
        ttk.Checkbutton(options, text=tr('Serveur DNS'), variable=dns, state="disabled").pack(side="left", padx=(0, 18))
        ttk.Checkbutton(options, text=tr('Catalogue global (GC)'), variable=catalog, state="disabled").pack(side="left")
        if not self.resume_mode:
            # Uniquement pour une nouvelle foret : un DC supplementaire s'authentifie
            # avec le compte du domaine saisi sur la premiere page (#14).
            self.admin_password_frame = tk.Frame(panel, background="#ffffff")
            self.admin_password_frame.grid(row=4, column=0, columnspan=2, sticky="ew")
            self.admin_password_frame.grid_columnconfigure(1, weight=1)
            self._field(self.admin_password_frame, 0, tr('Mot de passe du compte Administrator'), self.password, secret=True)
            self._field(self.admin_password_frame, 1, tr('Confirmer le mot de passe Administrator'), self.password_again, secret=True)
            self._deployment_changed()

    def _dns_servers_error(self):
        servers = self.dns_servers.get().replace(",", " ").split()
        if len(servers) > 3:
            return tr('Indiquez trois serveurs DNS au plus.')
        for server in servers:
            try:
                if ipaddress.ip_address(server).version != 4:
                    raise ValueError
            except ValueError:
                return tr('Serveur DNS invalide : {}').format(server)
        return ""

    def _dns_forwarder_error(self):
        """Un redirecteur pointant vers ce controleur fait tourner le DNS en boucle (#14)."""
        server = self.values["ip-address"].get().split("/", 1)[0]
        forwarders = self.values["dns-forwarder"].get().replace(",", " ").split()
        if not forwarders:
            return tr('Indiquez au moins un redirecteur DNS.')
        for forwarder in forwarders:
            try:
                address = ipaddress.ip_address(forwarder)
            except ValueError:
                return tr('Redirecteur DNS invalide : {}').format(forwarder)
            if address.version != 4 or address.is_loopback or forwarder == server:
                return (
                    tr('Le redirecteur {} designe ce controleur : le DNS tournerait en boucle. Indiquez un DNS externe (passerelle, DNS du FAI ou public).').format(forwarder)
                )
        return ""

    def _uppercase_netbios(self):
        value = self.values["domain"].get()
        if value != value.upper():
            self.values["domain"].set(value.upper())

    def _update_function_level_state(self):
        joining = self.values["deployment"].get() == "dc"
        if self.resume_mode or joining:
            self.function_level_box.state(["disabled"])
        else:
            self.function_level_box.state(["!disabled"])
        self.function_level_hint.set(
            tr('Detecte automatiquement depuis le domaine rejoint') if joining and not self.resume_mode else ""
        )

    def _build_network_page(self):
        # Le reseau est configure avant la promotion et n'est plus modifie (#14) :
        # seuls le DNS local du controleur et ses redirecteurs sont regles.
        panel = self._new_page(
            tr('Configuration DNS'),
            tr("L'adresse du serveur est conservee ; le controleur utilisera son propre DNS."),
        )
        self.server_address = tk.StringVar(
            value=f'{self.values["ip-address"].get()} ({self.values["interface"].get()})'
        )
        self._field(panel, 0, tr('Adresse du serveur (inchangee)'), self.server_address, disabled=True)
        self._field(panel, 1, tr('Redirecteurs DNS (separes par un espace)'), self.values["dns-forwarder"], disabled=self.resume_mode)
        tk.Label(
            panel,
            text=tr('Les ports Active Directory sont ouverts automatiquement a toutes les sources.'),
            background="#ffffff", foreground="#5f6b7a",
        ).grid(row=2, column=0, columnspan=2, sticky="w", padx=24, pady=(12, 0))

    def _build_hardening_page(self):
        panel = self._new_page(
            tr('Options de durcissement'),
            tr('Selectionnez les protections et outils a appliquer au controleur de domaine.'),
        )
        first_option_row = 0
        if self.resume_mode:
            tk.Label(
                panel,
                text=tr('Operation de maintenance'),
                background="#ffffff",
                foreground="#202020",
                font=("Segoe UI", 9, "bold"),
            ).grid(row=0, column=0, columnspan=2, sticky="w", padx=24, pady=(16, 7))
            ttk.Radiobutton(
                panel,
                text=tr('Corriger le durcissement et les GPO par defaut'),
                variable=self.maintenance_mode,
                value="correction",
            ).grid(row=1, column=0, columnspan=2, sticky="w", padx=24, pady=3)
            ttk.Radiobutton(
                panel,
                text=tr('Etendre uniquement le schema pour Exchange'),
                variable=self.maintenance_mode,
                value="exchange-only",
            ).grid(row=2, column=0, columnspan=2, sticky="w", padx=24, pady=3)
            ttk.Radiobutton(
                panel,
                text=tr('Retrograder ce controleur (il devient un serveur autonome)'),
                variable=self.maintenance_mode,
                value="demote",
            ).grid(row=3, column=0, columnspan=2, sticky="w", padx=24, pady=3)
            # Controleur qui recoit les roles FSMO lors d'une retrogradation (#14).
            controllers = other_domain_controllers(self.values["realm"].get())
            if controllers:
                self.demote_target.set(controllers[0])
            tk.Label(
                panel, text=tr('Controleur qui recoit les roles FSMO'), background="#ffffff", foreground="#202020",
            ).grid(row=4, column=0, sticky="w", padx=(48, 18), pady=(3, 6))
            ttk.Combobox(
                panel, textvariable=self.demote_target, values=controllers, state="readonly",
                style="Wizard.TCombobox",
            ).grid(row=4, column=1, sticky="ew", padx=(0, 24), pady=(3, 6))
            first_option_row = 5
        options = (
            ("mdb", tr('Backend MDB pour une nouvelle foret')),
            ("smb", tr('Durcissement SMB, NTLM, NetBIOS et impression')),
            ("winbind", tr('Integration Winbind dans NSS et PAM')),
            ("audit", tr("Journaux d'audit AD et SYSVOL")),
            ("backup", tr('Sauvegarde locale AD apres validation')),
            ("password", tr('Controle local renforce des mots de passe')),
            ("openrsat", tr('OpenRSAT et ses dependances GTK')),
            ("powershell", "PowerShell Core"),
            ("hgpm", tr('Harden Group Policy Management (HGPM)')),
        )
        for row, (name, label) in enumerate(options, start=first_option_row):
            button = ttk.Checkbutton(panel, text=label, variable=self.hardening[name])
            button.grid(row=row, column=0, columnspan=2, sticky="w", padx=24, pady=(14 if row == first_option_row else 5, 5))
            if name == "mdb" and self.resume_mode:
                button.state(["disabled"])
        if self.resume_mode:
            self._field(panel, first_option_row + 9, tr('Mot de passe du compte Administrator'), self.password, secret=True)
            self._field(panel, first_option_row + 10, tr('Confirmer le mot de passe Administrator'), self.password_again, secret=True)
            self._field(
                panel, first_option_row + 11, tr('Serveurs DNS de ce controleur'), self.dns_servers,
                tr('Tous les serveurs DNS utilises par ce serveur, 3 au plus (lui-meme et les autres DC)'),
            )
        else:
            ttk.Checkbutton(
                panel,
                text=tr('Etendre le schema Active Directory pour Exchange apres la promotion'),
                variable=self.exchange_schema,
            ).grid(row=9, column=0, columnspan=2, sticky="w", padx=24, pady=(12, 5))

    def _build_review_page(self):
        panel = self._new_page(
            tr('Verification des options'),
            tr('Verifiez vos choix avant de lancer la promotion de ce serveur.'),
        )
        self.review_text = tk.Text(
            panel,
            height=16,
            background="#ffffff",
            foreground="#202020",
            relief="flat",
            font=("Segoe UI", 9),
            padx=20,
            pady=18,
            state="disabled",
        )
        self.review_text.grid(row=0, column=0, columnspan=2, sticky="nsew")
        panel.grid_rowconfigure(0, weight=1)
        self.smb_conf_button = ttk.Button(
            panel, text=tr('Afficher le smb.conf'), style="Secondary.TButton", command=self.show_smb_conf,
        )
        self.smb_conf_button.grid(row=1, column=0, sticky="w", padx=20, pady=(0, 16))

    def show_step(self, index):
        self.current_step = index
        self.pages[index].tkraise()
        for step_index, (row, number, label) in enumerate(self.step_labels):
            active = step_index == index
            row.configure(background="#1b75bb" if active else "#243447")
            number.configure(
                background="#ffffff" if active else "#3e5368",
                foreground="#1b75bb" if active else "#ffffff",
            )
            label.configure(
                background="#1b75bb" if active else "#243447",
                foreground="#ffffff" if active else "#d9e2ec",
            )
        self.back_button.state(["disabled"] if index == 0 else ["!disabled"])
        if index == len(self.pages) - 1:
            self.next_button.pack_forget()
            self.install_button.pack(side="right", padx=6, pady=17, before=self.cancel_button)
            self._update_review()
        else:
            self.install_button.pack_forget()
            if not self.next_button.winfo_ismapped():
                self.next_button.pack(side="right", padx=6, pady=17, before=self.cancel_button)

    def _validate_step(self):
        if self.current_step == 0:
            required = ("realm", "domain")
            if not self.resume_mode and not re.fullmatch(r"[A-Z0-9][A-Z0-9-]{0,14}", self.values["domain"].get().strip()):
                messagebox.showerror(
                    "SambaADPromo",
                    tr('Le nom NetBIOS doit compter 15 caracteres au plus : lettres majuscules, chiffres et tirets.'),
                )
                return False
            if self.values["deployment"].get() == "dc":
                required += ("domain-admin", "domain-dns")
                if not self.dns_test_ok:
                    messagebox.showerror(
                        "SambaADPromo",
                        tr('Testez avec succes le domaine (DNS et authentification) avant de continuer.'),
                    )
                    return False
        elif self.current_step == 1:
            required = ("hostname", "function-level")
            joining = self.values["deployment"].get() == "dc"
            if not self.resume_mode and not joining and (
                not self.password.get() or self.password.get() != self.password_again.get()
            ):
                messagebox.showerror("SambaADPromo", tr('Les mots de passe sont absents ou differents.'))
                return False
        elif self.current_step == 2:
            required = ("dns-forwarder",)
            if not self.resume_mode:
                error = self._dns_forwarder_error()
                if error:
                    messagebox.showerror("SambaADPromo", error)
                    return False
        elif self.current_step == 3 and self.resume_mode:
            required = ()
            if self.maintenance_mode.get() == "exchange-only":
                if not exchange_ldf_available():
                    messagebox.showerror("SambaADPromo", EXCHANGE_MISSING_MESSAGE)
                    return False
            elif self.maintenance_mode.get() == "demote" and not self.demote_target.get():
                messagebox.showerror(
                    "SambaADPromo",
                    tr("Aucun autre controleur n'est annonce dans le domaine : le dernier controleur ne peut pas etre retrograde."),
                )
                return False
            elif self.maintenance_mode.get() == "correction" and self._dns_servers_error():
                messagebox.showerror("SambaADPromo", self._dns_servers_error())
                return False
            elif not self.password.get() or self.password.get() != self.password_again.get():
                messagebox.showerror("SambaADPromo", tr('Les mots de passe sont absents ou differents.'))
                return False
        else:
            required = ()
        if self.current_step == 3 and not self.resume_mode and self.exchange_schema.get():
            if not exchange_ldf_available():
                messagebox.showerror("SambaADPromo", EXCHANGE_MISSING_MESSAGE)
                return False
        if any(not self.values[name].get().strip() for name in required):
            messagebox.showerror("SambaADPromo", tr('Tous les champs de cette etape sont obligatoires.'))
            return False
        return True

    def next_step(self):
        if self._validate_step() and self.current_step < len(self.pages) - 1:
            self.show_step(self.current_step + 1)

    def previous_step(self):
        if self.current_step > 0:
            self.show_step(self.current_step - 1)

    def _update_review(self):
        joining = self.values["deployment"].get() == "dc"
        enabled_hardening = [name for name, enabled in self.hardening.items() if enabled.get()]
        rows = (
            (tr('Operation'), {
                "exchange-only": tr('Schema Exchange uniquement'),
                "demote": tr('Retrograder ce controleur, roles FSMO vers {}').format(self.demote_target.get()),
            }.get(self.maintenance_mode.get(), tr('Modifier le durcissement')) if self.resume_mode else (tr('Ajouter un DC') if joining else tr('Nouvelle foret'))),
            (tr('Domaine DNS'), self.values["realm"].get()),
            (tr('Domaine NetBIOS'), tr('Lu dans le domaine apres la jonction') if joining else self.values["domain"].get()),
            (tr('Compte du domaine'), self.values["domain-admin"].get() if joining else "Administrator"),
            (tr('DNS du domaine'), self.values["domain-dns"].get() if joining else tr('Sans objet')),
            (tr('Controleur de domaine'), self.values["hostname"].get()),
            (tr('Niveau fonctionnel'), tr('Detecte depuis le domaine') if joining else self.values["function-level"].get()),
            (tr('Adresse du serveur'), tr('{} (inchangee)').format(self.values["ip-address"].get())),
            (tr('Redirecteur DNS'), self.values["dns-forwarder"].get()),
            (tr('Pare-feu'), tr('Ports AD ouverts a toutes les sources')),
            (tr('Durcissement'), ", ".join(enabled_hardening) if enabled_hardening else tr('Aucun')),
            (tr('Schema Exchange'), tr('Extension avec les LDF embarques') if (self.exchange_schema.get() or self.maintenance_mode.get() == "exchange-only") else tr('Non demande')),
        )
        self.review_text.configure(state="normal")
        self.review_text.delete("1.0", "end")
        changes_smb_conf = not (self.resume_mode and self.maintenance_mode.get() in ("exchange-only", "demote"))
        self.smb_conf_button.state(["!disabled"] if changes_smb_conf else ["disabled"])
        self.review_text.insert("end", tr('RESUME DE LA CONFIGURATION\n\n'))
        for label, value in rows:
            self.review_text.insert("end", f"{label:<25} {value}\n")
        self.review_text.configure(state="disabled")

    def _smb_conf_preview(self):
        """Produit le smb.conf qu'installera provision-samba-ad.sh avec les choix actuels."""
        joining = not self.resume_mode and self.values["deployment"].get() == "dc"
        realm = self.values["realm"].get().strip().upper()
        if self.resume_mode:
            result = subprocess.run(
                ["testparm", "-s", "--parameter-name=ad dc functional level"],
                capture_output=True, text=True, timeout=10, check=False,
            )
            level = result.stdout.strip() or "2008_R2"
        elif joining:
            level = tr('<niveau du domaine>')
        else:
            level = self.values["function-level"].get().strip()
        enabled = {name for name, variable in self.hardening.items() if variable.get()}
        options = []
        if joining or level != "2008_R2":
            options.append("functional-level")
        if enabled & {"password", "hibp"}:
            options.append("password")
        options += [name for name in ("smb", "winbind", "ldaps", "audit") if name in enabled]
        values = {
            "NETBIOS_NAME": self.values["hostname"].get().strip().upper(),
            "REALM": realm,
            "REALM_LOWER": realm.lower(),
            "WORKGROUP": tr('<lu dans le domaine>') if joining else self.values["domain"].get().strip().upper(),
            "DNS_FORWARDER": " ".join(self.values["dns-forwarder"].get().replace(",", " ").split()),
            "FUNCTION_LEVEL": level,
        }
        command = [SMB_CONF_RENDERER, "--enable", ",".join(options)]
        for name, value in values.items():
            command += ["--set", f"{name}={value}"]
        result = subprocess.run(command, capture_output=True, text=True, timeout=10, check=False)
        if result.returncode != 0:
            raise OSError(result.stderr.strip() or f"status {result.returncode}")
        return result.stdout, joining

    def show_smb_conf(self):
        try:
            content, joining = self._smb_conf_preview()
        except (OSError, subprocess.SubprocessError) as error:
            messagebox.showerror("SambaADPromo", tr('Apercu du smb.conf impossible : {}').format(error))
            return
        window = tk.Toplevel(self.master, background="#f3f4f6")
        window.title(tr('Contenu de /etc/samba/smb.conf'))
        window.geometry("820x600")
        window.transient(self.master)
        notes = tr('Fichier qui sera installe, produit depuis le modele {}.').format(SMB_CONF_TEMPLATE)
        if joining:
            notes += "\n" + tr('Les valeurs entre < > seront lues dans le domaine pendant la jonction.')
        tk.Label(
            window, text=notes, background="#f3f4f6", foreground="#5f6b7a",
            font=("Segoe UI", 9), justify="left", anchor="w",
        ).pack(fill="x", padx=20, pady=(16, 8))
        footer = tk.Frame(window, background="#f3f4f6")
        footer.pack(side="bottom", fill="x", padx=20, pady=(8, 16))
        ttk.Button(footer, text=tr('Fermer'), style="Secondary.TButton", command=window.destroy).pack(side="right")
        frame = tk.Frame(window, background="#ffffff")
        frame.pack(fill="both", expand=True, padx=20)
        text = tk.Text(
            frame, wrap="none", background="#ffffff", foreground="#202020", relief="flat",
            font=("DejaVu Sans Mono", 9), padx=12, pady=10,
        )
        vertical = ttk.Scrollbar(frame, orient="vertical", command=text.yview)
        horizontal = ttk.Scrollbar(frame, orient="horizontal", command=text.xview)
        text.configure(yscrollcommand=vertical.set, xscrollcommand=horizontal.set)
        text.grid(row=0, column=0, sticky="nsew")
        vertical.grid(row=0, column=1, sticky="ns")
        horizontal.grid(row=1, column=0, sticky="ew")
        frame.grid_rowconfigure(0, weight=1)
        frame.grid_columnconfigure(0, weight=1)
        text.insert("1.0", content.expandtabs(4))
        text.configure(state="disabled")

    def _set_running(self, running):
        state = ["disabled"] if running else ["!disabled"]
        for button in (self.install_button, self.back_button, self.cancel_button):
            button.state(state)
        if running:
            self.status.set(
                tr('Extension du schema Exchange en cours...')
                if self.resume_mode and self.maintenance_mode.get() == "exchange-only"
                else tr('Promotion du serveur en cours...')
            )
            self.progress.pack(side="left", padx=(24, 10), pady=24)
            self.progress.start(12)
        else:
            self.progress.stop()
            self.progress.pack_forget()
            self.status.set(tr('Operation terminee.'))

    def promote(self):
        if self.completed:
            self.master.destroy()
            return
        exchange_only = self.resume_mode and self.maintenance_mode.get() == "exchange-only"
        if self.resume_mode and self.maintenance_mode.get() == "demote":
            self._demote()
            return
        if not messagebox.askyesno(
            tr("Confirmer l'installation"),
            (
            tr('Le schema Active Directory sera etendu pour Exchange. Cette operation est irreversible. Continuer ?')
            if exchange_only else
                tr('Les options de durcissement vont etre appliquees et les deux GPO par defaut seront sauvegardees puis remises a leur etat Windows par defaut. Continuer ?')
                if self.resume_mode
                else tr('Le serveur va etre promu en controleur de domaine Samba AD. Continuer ?')
            ),
        ):
            return
        command = []
        if os.geteuid() != 0:
            command.append("pkexec")
        if exchange_only:
            command.extend([
                "/usr/sbin/exchange-schema-upgrade",
                "--yes",
            ])
            self.password.set("")
            self.password_again.set("")
            self._set_running(True)
            threading.Thread(target=self._run_promotion, args=(command, ""), daemon=True).start()
            self.master.after(100, self._poll_promotion)
            return
        command.extend([
            "/usr/lib/hardenbox/provision-samba-ad.sh",
            "--non-interactive",
            "--offline",
            "--password-stdin",
        ])
        if self.resume_mode:
            command.append("--resume")
            if self.dns_servers.get().strip():
                command.extend(("--dns-servers", self.dns_servers.get().strip()))
        else:
            command.extend(("--cleanup-partial", "--deployment", self.values["deployment"].get()))
        for option in (
            "realm", "domain", "hostname", "dns-forwarder", "function-level",
        ):
            if option == "domain" and self.values["deployment"].get() == "dc":
                continue  # Lu dans le domaine apres la jonction.
            command.extend((f"--{option}", self.values[option].get().strip()))
        if self.values["deployment"].get() == "dc":
            command.extend(("--domain-admin", self.values["domain-admin"].get().strip()))
            command.extend(("--domain-dns", self.values["domain-dns"].get().strip()))
        selected_hardening = [name for name, enabled in self.hardening.items() if enabled.get()]
        command.extend(("--hardening", ",".join(selected_hardening) or "none"))
        if self.exchange_schema.get():
            command.append("--exchange-schema")
        joining = self.values["deployment"].get() == "dc"
        password = self.domain_password.get() if joining else self.password.get()
        self.password.set("")
        self.password_again.set("")
        self.domain_password.set("")
        self._set_running(True)
        threading.Thread(target=self._run_promotion, args=(command, password), daemon=True).start()
        self.master.after(100, self._poll_promotion)

    def _demote(self):
        target = self.demote_target.get()
        if not messagebox.askyesno(
            tr('Confirmer la retrogradation'),
            tr('Les roles FSMO vont etre transferes a {}, puis ce serveur ne sera plus controleur de domaine : Samba AD sera arrete et sa configuration retiree (une sauvegarde est conservee). Cette operation est irreversible. Continuer ?').format(target),
            icon="warning",
        ):
            return
        command = [] if os.geteuid() == 0 else ["pkexec"]
        command.extend([
            "/usr/sbin/demote-samba-ad.sh",
            "--target-dc", target,
            "--domain-admin", "Administrator",
            "--password-stdin",
            "--yes",
        ])
        password = self.password.get()
        self.password.set("")
        self.password_again.set("")
        self._set_running(True)
        threading.Thread(target=self._run_promotion, args=(command, password), daemon=True).start()
        self.master.after(100, self._poll_promotion)

    def _run_promotion(self, command, password):
        result = subprocess.run(
            command,
            input=password + "\n" if password else None,
            text=True,
            capture_output=True,
            check=False,
        )
        self.result_queue.put(result)

    def _poll_promotion(self):
        try:
            result = self.result_queue.get_nowait()
        except queue.Empty:
            self.master.after(100, self._poll_promotion)
            return
        self._promotion_finished(result)

    @staticmethod
    def _launch_checks():
        checker = Path("/usr/bin/sambaad-check-gui")
        if checker.is_file():
            subprocess.Popen([str(checker)], start_new_session=True,
                             stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    def _bring_to_front(self):
        """Ramene la fenetre au premier plan : apres pkexec et plusieurs minutes de
        travail, GNOME laissait sinon le message de fin derriere les autres fenetres."""
        self.master.deiconify()
        self.master.lift()
        self.master.attributes("-topmost", True)
        self.master.focus_force()
        self.master.update_idletasks()

    def _release_front(self):
        self.master.attributes("-topmost", False)

    def _promotion_finished(self, result):
        self._set_running(False)
        self._bring_to_front()
        if result.returncode == 0:
            self.completed = True
            self.install_button.configure(text="Fermer", command=self.master.destroy)
            self.install_button.state(["!disabled"])
            self.back_button.state(["disabled"])
            self.cancel_button.pack_forget()
            success_message = (
                tr('Controleur retrograde : ce serveur est maintenant autonome. Redemarrez la machine.')
                if self.resume_mode and self.maintenance_mode.get() == "demote" else
                tr('Schema Active Directory etendu pour Exchange avec succes.')
                if self.resume_mode and self.maintenance_mode.get() == "exchange-only" else
                tr('Durcissement applique et GPO par defaut corrigees avec succes.')
                if self.resume_mode else tr('Promotion terminee avec succes. Redemarrez la machine.')
            )
            self.status.set(success_message)
            messagebox.showinfo("SambaADPromo", success_message, parent=self.master)
            self._release_front()
            # Apres une promotion ou une correction reussie, les tests du controleur
            # (paquet hb-sambaad-check) s'ouvrent pour verifier le resultat.
            if self.maintenance_mode.get() not in ("demote", "exchange-only") or not self.resume_mode:
                self._launch_checks()
        else:
            output = (result.stderr or result.stdout or "").splitlines()
            errors = [line for line in output if line.startswith("ERREUR")][-3:]
            details = "\n".join(errors or output[-12:])
            messagebox.showerror(
                "SambaADPromo",
                (tr('La retrogradation a echoue.') if self.resume_mode and self.maintenance_mode.get() == "demote" else tr('La promotion a echoue.'))
                + tr('\n\n{}\n\nJournal complet : /var/log/hardenbox/').format(details)
                + ("demote-samba-ad.log" if self.resume_mode and self.maintenance_mode.get() == "demote"
                   else "provision-samba-ad.log"),
                parent=self.master,
            )
            self._release_front()


def main():
    root = tk.Tk()
    root.title(tr('SambaADPromo - Configuration AD DS'))
    root.geometry("980x700")
    root.minsize(900, 660)
    PromoApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
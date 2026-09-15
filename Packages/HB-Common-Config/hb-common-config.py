#!/usr/bin/python3

import ipaddress
import json
import os
import pwd
import queue
import re
import socket
import subprocess
import threading
import tkinter as tk
from pathlib import Path
from tkinter import messagebox, ttk


COMPLETED_MARKER = Path("/var/lib/hardenbox/hb-common.completed")


def system_defaults():
    users = [
        entry.pw_name
        for entry in pwd.getpwall()
        if 1000 <= entry.pw_uid < 60000 and entry.pw_name != "nobody"
    ]
    defaults = {
        "profile": "gnome",
        "hostname": socket.getfqdn().strip().lower(),
        "admin-user": users[0] if users else "",
        "desktop-user": users[0] if users else "",
        "ssh-port": "22",
        "ssh-allowed-cidr": "0.0.0.0/0",
        "network-interface": "",
        "ip-address": "",
        "gateway": "",
        "dns": "",
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
        defaults["network-interface"] = route["dev"]
        defaults["gateway"] = route.get("gateway", "")
        links = json.loads(subprocess.run(
            ["ip", "-j", "-4", "address", "show", "dev", route["dev"], "scope", "global"],
            text=True,
            capture_output=True,
            timeout=3,
            check=True,
        ).stdout)
        address = next(item for item in links[0].get("addr_info", []) if item.get("family") == "inet")
        defaults["ip-address"] = f'{address["local"]}/{address["prefixlen"]}'
    except (IndexError, KeyError, OSError, StopIteration, subprocess.SubprocessError, ValueError):
        pass

    try:
        nameservers = []
        for line in Path("/etc/resolv.conf").read_text().splitlines():
            fields = line.split()
            if len(fields) == 2 and fields[0] == "nameserver":
                ipaddress.ip_address(fields[1])
                nameservers.append(fields[1])
        defaults["dns"] = ",".join(nameservers)
    except (OSError, ValueError):
        pass
    return defaults, users


class CommonConfigApp(tk.Frame):
    def __init__(self, master):
        super().__init__(master, background="#f3f4f6")
        self.master = master
        defaults, self.users = system_defaults()
        self.values = {name: tk.StringVar(value=value) for name, value in defaults.items()}
        self.configure_network = tk.BooleanVar(value=False)
        self.result_queue = queue.Queue()
        self.completed = False
        self._configure_styles()
        self._build()
        self.pack(fill="both", expand=True)

    def _configure_styles(self):
        style = ttk.Style(self.master)
        style.theme_use("clam")
        style.configure("Wizard.TEntry", fieldbackground="#ffffff", padding=7)
        style.configure("Wizard.TCombobox", fieldbackground="#ffffff", padding=6)
        style.configure("Primary.TButton", background="#0078d4", foreground="#ffffff", padding=(18, 8))
        style.map("Primary.TButton", background=[("active", "#106ebe"), ("disabled", "#a7c7e7")])
        style.configure("Secondary.TButton", background="#ffffff", foreground="#202020", padding=(16, 8))

    def _build(self):
        header = tk.Frame(self, background="#ffffff", height=82, highlightbackground="#d8dde3", highlightthickness=1)
        header.pack(fill="x")
        header.pack_propagate(False)
        tk.Label(
            header,
            text="Configuration initiale HardenBox",
            background="#ffffff",
            foreground="#1f2937",
            font=("Segoe UI", 16, "bold"),
        ).pack(anchor="w", padx=28, pady=(17, 2))
        tk.Label(
            header,
            text="Appliquez le profil commun apres la premiere ouverture de session",
            background="#ffffff",
            foreground="#5f6b7a",
            font=("Segoe UI", 9),
        ).pack(anchor="w", padx=29)

        content = tk.Frame(self, background="#f3f4f6")
        content.pack(fill="both", expand=True, padx=32, pady=24)
        content.grid_columnconfigure(1, weight=1)
        self._combo(content, 0, "Profil", self.values["profile"], ("gnome", "server"))
        self._entry(content, 1, "Nom d'hote complet", self.values["hostname"])
        self._combo(content, 2, "Compte administrateur", self.values["admin-user"], self.users)
        self._combo(content, 3, "Compte du bureau", self.values["desktop-user"], self.users)
        self._entry(content, 4, "Port SSH", self.values["ssh-port"])
        self._entry(content, 5, "Reseau autorise pour SSH", self.values["ssh-allowed-cidr"])

        ttk.Checkbutton(
            content,
            text="Configurer une adresse IPv4 statique",
            variable=self.configure_network,
            command=self._toggle_network,
        ).grid(row=6, column=0, columnspan=2, sticky="w", pady=(16, 7))
        self.network_widgets = []
        self._entry(content, 7, "Interface reseau", self.values["network-interface"], network=True)
        self._entry(content, 8, "Adresse IPv4 / prefixe", self.values["ip-address"], network=True)
        self._entry(content, 9, "Passerelle IPv4", self.values["gateway"], network=True)
        self._entry(content, 10, "Serveurs DNS (separes par des virgules)", self.values["dns"], network=True)
        self._toggle_network()

        footer = tk.Frame(self, background="#ffffff", height=72, highlightbackground="#d8dde3", highlightthickness=1)
        footer.pack(fill="x")
        footer.pack_propagate(False)
        self.status = tk.StringVar(value="Pret a appliquer la configuration.")
        tk.Label(
            footer,
            textvariable=self.status,
            background="#ffffff",
            foreground="#5f6b7a",
            font=("Segoe UI", 9),
        ).pack(side="left", padx=24, pady=24)
        self.apply_button = ttk.Button(footer, text="Appliquer", style="Primary.TButton", command=self.apply)
        self.apply_button.pack(side="right", padx=(6, 20), pady=17)
        self.cancel_button = ttk.Button(footer, text="Plus tard", style="Secondary.TButton", command=self.master.destroy)
        self.cancel_button.pack(side="right", padx=6, pady=17)

    def _entry(self, parent, row, label, variable, network=False):
        label_widget = tk.Label(parent, text=label, background="#f3f4f6", foreground="#202020")
        label_widget.grid(row=row, column=0, sticky="w", padx=(0, 20), pady=6)
        entry = ttk.Entry(parent, textvariable=variable, style="Wizard.TEntry")
        entry.grid(row=row, column=1, sticky="ew", pady=6)
        if network:
            self.network_widgets.extend((label_widget, entry))

    def _combo(self, parent, row, label, variable, values):
        tk.Label(parent, text=label, background="#f3f4f6", foreground="#202020").grid(
            row=row, column=0, sticky="w", padx=(0, 20), pady=6
        )
        combo = ttk.Combobox(parent, textvariable=variable, values=values, style="Wizard.TCombobox")
        combo.grid(row=row, column=1, sticky="ew", pady=6)

    def _toggle_network(self):
        state = "normal" if self.configure_network.get() else "disabled"
        for widget in self.network_widgets:
            if isinstance(widget, ttk.Entry):
                widget.configure(state=state)
            else:
                widget.configure(foreground="#202020" if state == "normal" else "#8a8f98")

    def _validate(self):
        profile = self.values["profile"].get().strip()
        hostname = self.values["hostname"].get().strip()
        admin_user = self.values["admin-user"].get().strip()
        desktop_user = self.values["desktop-user"].get().strip() or admin_user
        if profile not in ("gnome", "server"):
            raise ValueError("Le profil doit etre gnome ou server.")
        if not re.fullmatch(r"(?=.{1,253}$)[A-Za-z0-9](?:[A-Za-z0-9.-]*[A-Za-z0-9])?", hostname):
            raise ValueError("Le nom d'hote n'est pas valide.")
        known_users = {entry.pw_name for entry in pwd.getpwall()}
        if admin_user not in known_users or (profile == "gnome" and desktop_user not in known_users):
            raise ValueError("Selectionnez des comptes utilisateur existants.")
        try:
            port = int(self.values["ssh-port"].get())
            if not 1 <= port <= 65535:
                raise ValueError
        except ValueError as error:
            raise ValueError("Le port SSH doit etre compris entre 1 et 65535.") from error
        try:
            ipaddress.ip_network(self.values["ssh-allowed-cidr"].get().strip(), strict=False)
        except ValueError as error:
            raise ValueError("Le reseau autorise pour SSH n'est pas valide.") from error
        if self.configure_network.get():
            required = ("network-interface", "ip-address", "gateway", "dns")
            if any(not self.values[name].get().strip() for name in required):
                raise ValueError("Tous les champs de configuration reseau sont obligatoires.")
            try:
                ipaddress.ip_interface(self.values["ip-address"].get().strip())
                ipaddress.ip_address(self.values["gateway"].get().strip())
                for server in self.values["dns"].get().split(","):
                    ipaddress.ip_address(server.strip())
            except ValueError as error:
                raise ValueError("La configuration IPv4 statique n'est pas valide.") from error
        return desktop_user

    def apply(self):
        if self.completed:
            self.master.destroy()
            return
        try:
            desktop_user = self._validate()
        except ValueError as error:
            messagebox.showerror("Configuration HardenBox", str(error))
            return
        if not messagebox.askyesno(
            "Confirmer la configuration",
            "La configuration commune HardenBox va etre appliquee. Continuer ?",
        ):
            return
        command = [] if os.geteuid() == 0 else ["pkexec"]
        command.extend([
            "/usr/lib/hardenbox/hb-common-config-apply",
            "--profile", self.values["profile"].get().strip(),
            "--hostname", self.values["hostname"].get().strip(),
            "--admin-user", self.values["admin-user"].get().strip(),
            "--desktop-user", desktop_user,
            "--ssh-port", self.values["ssh-port"].get().strip(),
            "--ssh-allowed-cidr", self.values["ssh-allowed-cidr"].get().strip(),
        ])
        if self.configure_network.get():
            for name in ("network-interface", "ip-address", "gateway", "dns"):
                command.extend((f"--{name}", self.values[name].get().strip()))
        self.status.set("Application de la configuration en cours...")
        self.apply_button.state(["disabled"])
        self.cancel_button.state(["disabled"])
        threading.Thread(target=self._run_apply, args=(command,), daemon=True).start()
        self.master.after(100, self._poll_apply)

    def _run_apply(self, command):
        self.result_queue.put(subprocess.run(command, text=True, capture_output=True, check=False))

    def _poll_apply(self):
        try:
            result = self.result_queue.get_nowait()
        except queue.Empty:
            self.master.after(100, self._poll_apply)
            return
        self.apply_button.state(["!disabled"])
        self.cancel_button.state(["!disabled"])
        if result.returncode == 0:
            self.completed = True
            self.status.set("Configuration appliquee avec succes. Redemarrez la machine.")
            self.apply_button.configure(text="Fermer")
            self.cancel_button.pack_forget()
            messagebox.showinfo("Configuration HardenBox", self.status.get())
        else:
            self.status.set("Echec de la configuration.")
            details = (result.stderr or result.stdout)[-3000:]
            messagebox.showerror("Configuration HardenBox", f"La configuration a echoue.\n\n{details}")


def main():
    if COMPLETED_MARKER.exists():
        return
    root = tk.Tk()
    root.title("HardenBox - Configuration initiale")
    root.geometry("820x720")
    root.minsize(720, 680)
    CommonConfigApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
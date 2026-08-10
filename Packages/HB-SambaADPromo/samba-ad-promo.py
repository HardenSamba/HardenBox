#!/usr/bin/python3

import os
import subprocess
import tkinter as tk
from tkinter import messagebox, ttk


FIELDS = (
    ("realm", "Domaine DNS", "AD.EXEMPLE.FR"),
    ("domain", "Domaine NetBIOS", "EXEMPLE"),
    ("hostname", "Nom du controleur", "dc01"),
    ("interface", "Interface reseau", "ens18"),
    ("ip-address", "Adresse IP/CIDR", "192.168.10.10/24"),
    ("gateway", "Passerelle", "192.168.10.1"),
    ("dns-forwarder", "DNS redirecteur", "1.1.1.1"),
    ("allowed-cidr", "Reseau autorise", "192.168.10.0/24"),
)


class PromoApp(ttk.Frame):
    def __init__(self, master):
        super().__init__(master, padding=18)
        self.master = master
        self.values = {}
        self.pack(fill="both", expand=True)
        self._build()

    def _build(self):
        for row, (name, label, default) in enumerate(FIELDS):
            ttk.Label(self, text=label).grid(row=row, column=0, sticky="w", pady=4)
            value = tk.StringVar(value=default)
            self.values[name] = value
            ttk.Entry(self, textvariable=value, width=38).grid(
                row=row, column=1, sticky="ew", padx=(12, 0), pady=4
            )

        row = len(FIELDS)
        ttk.Label(self, text="Mot de passe Administrator").grid(row=row, column=0, sticky="w", pady=4)
        self.password = tk.StringVar()
        ttk.Entry(self, textvariable=self.password, show="*", width=38).grid(
            row=row, column=1, sticky="ew", padx=(12, 0), pady=4
        )
        self.promote_button = ttk.Button(self, text="Promouvoir", command=self.promote)
        self.promote_button.grid(row=row + 1, column=1, sticky="e", pady=(16, 0))
        self.columnconfigure(1, weight=1)

    def promote(self):
        password = self.password.get()
        if any(not value.get().strip() for value in self.values.values()) or not password:
            messagebox.showerror("SambaADPromo", "Tous les champs sont obligatoires.")
            return
        if not messagebox.askyesno(
            "SambaADPromo", "La promotion modifie le reseau et les services Samba. Continuer ?"
        ):
            return

        command = ["/usr/lib/hardenbox/provision-samba-ad.sh", "--non-interactive"]
        for option, _, _ in FIELDS:
            command.extend((f"--{option}", self.values[option].get().strip()))
        command.extend(("--function-level", "2016", "--hardening", "mdb,smb,winbind,audit,backup"))
        environment = os.environ.copy()
        environment["SAMBA_ADMIN_PASSWORD"] = password
        environment["HARDENBOX_OFFLINE"] = "1"
        self.password.set("")
        self.promote_button.state(["disabled"])
        try:
            result = subprocess.run(command, env=environment, text=True, capture_output=True, check=False)
        finally:
            self.promote_button.state(["!disabled"])
        if result.returncode == 0:
            messagebox.showinfo("SambaADPromo", "Promotion terminee avec succes. Redemarrez la machine.")
        else:
            details = (result.stderr or result.stdout)[-3000:]
            messagebox.showerror("SambaADPromo", f"La promotion a echoue.\n\n{details}")


def main():
    if os.geteuid() != 0:
        raise SystemExit("SambaADPromo doit etre execute avec les privileges root.")
    root = tk.Tk()
    root.title("SambaADPromo")
    root.minsize(560, 470)
    PromoApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
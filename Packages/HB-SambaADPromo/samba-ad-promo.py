#!/usr/bin/python3

import os
import queue
import subprocess
import threading
import tkinter as tk
from tkinter import messagebox, ttk


STEPS = (
    "Configuration de deploiement",
    "Options du controleur",
    "Configuration reseau",
    "Verification des options",
)


class PromoApp(tk.Frame):
    def __init__(self, master):
        super().__init__(master, background="#f3f4f6")
        self.master = master
        self.current_step = 0
        self.pages = []
        self.step_labels = []
        self.values = {
            "realm": tk.StringVar(value="AD.EXEMPLE.FR"),
            "domain": tk.StringVar(value="EXEMPLE"),
            "hostname": tk.StringVar(value="dc01"),
            "interface": tk.StringVar(value="ens18"),
            "ip-address": tk.StringVar(value="192.168.10.10/24"),
            "gateway": tk.StringVar(value="192.168.10.1"),
            "dns-forwarder": tk.StringVar(value="1.1.1.1"),
            "allowed-cidr": tk.StringVar(value="192.168.10.0/24"),
            "function-level": tk.StringVar(value="2016"),
        }
        self.password = tk.StringVar()
        self.password_again = tk.StringVar()
        self.status = tk.StringVar(value="Pret a configurer ce serveur.")
        self.result_queue = queue.Queue()
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

    def _build_header(self):
        header = tk.Frame(self, background="#ffffff", height=82, highlightbackground="#d8dde3", highlightthickness=1)
        header.grid(row=0, column=0, columnspan=2, sticky="nsew")
        header.grid_propagate(False)
        tk.Label(
            header,
            text="Configuration des services de domaine Active Directory",
            background="#ffffff",
            foreground="#1f2937",
            font=("Segoe UI", 16, "bold"),
        ).pack(anchor="w", padx=28, pady=(17, 2))
        tk.Label(
            header,
            text="Assistant de promotion Samba AD DS",
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
            text="ETAPES",
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
        self.install_button = ttk.Button(footer, text="Installer", style="Primary.TButton", command=self.promote)
        self.next_button = ttk.Button(footer, text="Suivant >", style="Primary.TButton", command=self.next_step)
        self.back_button = ttk.Button(footer, text="< Precedent", style="Secondary.TButton", command=self.previous_step)
        self.cancel_button = ttk.Button(footer, text="Annuler", style="Secondary.TButton", command=self.master.destroy)
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

    def _field(self, panel, row, label, variable, help_text="", secret=False):
        tk.Label(
            panel,
            text=label,
            background="#ffffff",
            foreground="#202020",
            font=("Segoe UI", 9),
        ).grid(row=row, column=0, sticky="w", padx=(24, 18), pady=(13, 3))
        entry = ttk.Entry(panel, textvariable=variable, show="*" if secret else "", style="Wizard.TEntry")
        entry.grid(row=row, column=1, sticky="ew", padx=(0, 24), pady=(13, 3))
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
            "Configuration de deploiement",
            "Selectionnez l'operation de deploiement et indiquez le nom de la nouvelle foret.",
        )
        tk.Label(
            panel,
            text="Operation de deploiement",
            background="#ffffff",
            foreground="#202020",
            font=("Segoe UI", 9, "bold"),
        ).grid(row=0, column=0, columnspan=2, sticky="w", padx=24, pady=(20, 8))
        deployment = tk.StringVar(value="forest")
        ttk.Radiobutton(
            panel,
            text="Ajouter une nouvelle foret",
            variable=deployment,
            value="forest",
        ).grid(row=1, column=0, columnspan=2, sticky="w", padx=24)
        self._field(panel, 2, "Nom de domaine racine", self.values["realm"], "Exemple : AD.ENTREPRISE.FR")
        self._field(panel, 4, "Nom de domaine NetBIOS", self.values["domain"], "15 caracteres maximum")

    def _build_controller_page(self):
        panel = self._new_page(
            "Options du controleur de domaine",
            "Definissez les capacites du premier controleur et son mot de passe d'administration.",
        )
        self._field(panel, 0, "Nom du controleur", self.values["hostname"], "Nom court, par exemple dc01")
        tk.Label(panel, text="Niveau fonctionnel", background="#ffffff", foreground="#202020").grid(
            row=2, column=0, sticky="w", padx=(24, 18), pady=13
        )
        ttk.Combobox(
            panel,
            textvariable=self.values["function-level"],
            values=("2016", "2008_R2"),
            state="readonly",
            style="Wizard.TCombobox",
        ).grid(row=2, column=1, sticky="ew", padx=(0, 24), pady=13)
        options = tk.Frame(panel, background="#ffffff")
        options.grid(row=3, column=1, sticky="w", padx=(0, 24), pady=(0, 8))
        dns = tk.BooleanVar(value=True)
        catalog = tk.BooleanVar(value=True)
        ttk.Checkbutton(options, text="Serveur DNS", variable=dns, state="disabled").pack(side="left", padx=(0, 18))
        ttk.Checkbutton(options, text="Catalogue global (GC)", variable=catalog, state="disabled").pack(side="left")
        self._field(panel, 4, "Mot de passe Administrator", self.password, secret=True)
        self._field(panel, 5, "Confirmer le mot de passe", self.password_again, secret=True)

    def _build_network_page(self):
        panel = self._new_page(
            "Configuration DNS et reseau",
            "Configurez l'adresse statique utilisee par le controleur de domaine.",
        )
        self._field(panel, 0, "Interface reseau", self.values["interface"])
        self._field(panel, 1, "Adresse IPv4 / CIDR", self.values["ip-address"])
        self._field(panel, 2, "Passerelle", self.values["gateway"])
        self._field(panel, 3, "Redirecteur DNS", self.values["dns-forwarder"])
        self._field(panel, 4, "Reseau autorise", self.values["allowed-cidr"])

    def _build_review_page(self):
        panel = self._new_page(
            "Verification des options",
            "Verifiez vos choix avant de lancer la promotion de ce serveur.",
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
        elif self.current_step == 1:
            required = ("hostname", "function-level")
            if not self.password.get() or self.password.get() != self.password_again.get():
                messagebox.showerror("SambaADPromo", "Les mots de passe sont absents ou differents.")
                return False
        elif self.current_step == 2:
            required = ("interface", "ip-address", "gateway", "dns-forwarder", "allowed-cidr")
        else:
            required = ()
        if any(not self.values[name].get().strip() for name in required):
            messagebox.showerror("SambaADPromo", "Tous les champs de cette etape sont obligatoires.")
            return False
        return True

    def next_step(self):
        if self._validate_step() and self.current_step < len(self.pages) - 1:
            self.show_step(self.current_step + 1)

    def previous_step(self):
        if self.current_step > 0:
            self.show_step(self.current_step - 1)

    def _update_review(self):
        rows = (
            ("Operation", "Nouvelle foret"),
            ("Domaine DNS", self.values["realm"].get()),
            ("Domaine NetBIOS", self.values["domain"].get()),
            ("Controleur de domaine", self.values["hostname"].get()),
            ("Niveau fonctionnel", self.values["function-level"].get()),
            ("Interface", self.values["interface"].get()),
            ("Adresse IPv4", self.values["ip-address"].get()),
            ("Passerelle", self.values["gateway"].get()),
            ("Redirecteur DNS", self.values["dns-forwarder"].get()),
            ("Reseau autorise", self.values["allowed-cidr"].get()),
            ("Options", "DNS, catalogue global, audit, sauvegarde, durcissement SMB"),
        )
        self.review_text.configure(state="normal")
        self.review_text.delete("1.0", "end")
        self.review_text.insert("end", "RESUME DE LA CONFIGURATION\n\n")
        for label, value in rows:
            self.review_text.insert("end", f"{label:<25} {value}\n")
        self.review_text.configure(state="disabled")

    def _set_running(self, running):
        state = ["disabled"] if running else ["!disabled"]
        for button in (self.install_button, self.back_button, self.cancel_button):
            button.state(state)
        if running:
            self.status.set("Promotion du serveur en cours...")
            self.progress.pack(side="left", padx=(24, 10), pady=24)
            self.progress.start(12)
        else:
            self.progress.stop()
            self.progress.pack_forget()
            self.status.set("Promotion terminee.")

    def promote(self):
        if not messagebox.askyesno(
            "Confirmer l'installation",
            "Le serveur va etre promu en controleur de domaine Samba AD. Continuer ?",
        ):
            return
        command = ["/usr/lib/hardenbox/provision-samba-ad.sh", "--non-interactive"]
        for option in (
            "realm", "domain", "hostname", "interface", "ip-address", "gateway",
            "dns-forwarder", "allowed-cidr", "function-level",
        ):
            command.extend((f"--{option}", self.values[option].get().strip()))
        command.extend(("--hardening", "mdb,smb,winbind,audit,backup"))
        environment = os.environ.copy()
        environment["SAMBA_ADMIN_PASSWORD"] = self.password.get()
        environment["HARDENBOX_OFFLINE"] = "1"
        self.password.set("")
        self.password_again.set("")
        self._set_running(True)
        threading.Thread(target=self._run_promotion, args=(command, environment), daemon=True).start()
        self.master.after(100, self._poll_promotion)

    def _run_promotion(self, command, environment):
        result = subprocess.run(command, env=environment, text=True, capture_output=True, check=False)
        self.result_queue.put(result)

    def _poll_promotion(self):
        try:
            result = self.result_queue.get_nowait()
        except queue.Empty:
            self.master.after(100, self._poll_promotion)
            return
        self._promotion_finished(result)

    def _promotion_finished(self, result):
        self._set_running(False)
        if result.returncode == 0:
            messagebox.showinfo("SambaADPromo", "Promotion terminee avec succes. Redemarrez la machine.")
        else:
            details = (result.stderr or result.stdout)[-3000:]
            messagebox.showerror("SambaADPromo", f"La promotion a echoue.\n\n{details}")


def main():
    if os.geteuid() != 0:
        raise SystemExit("SambaADPromo doit etre execute avec les privileges root.")
    root = tk.Tk()
    root.title("SambaADPromo - Configuration AD DS")
    root.geometry("980x650")
    root.minsize(900, 610)
    PromoApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()
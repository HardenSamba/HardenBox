#!/usr/bin/env python3
"""Interface graphique des tests du controleur de domaine Harden Box.

Lance /usr/lib/hardenbox/sambaad-check.py avec pkexec, affiche les resultats
par categorie avec leur detail et permet d'exporter le rapport.
"""

import json
import os
import queue
import subprocess
import threading
import tkinter as tk
from datetime import datetime
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

CHECK = "/usr/lib/hardenbox/sambaad-check.py"

EN = {
    "Tests du controleur de domaine": "Domain controller tests",
    "Verification de la promotion Samba AD : DNS, redirection, ports, Kerberos, partages, base et GPO":
        "Samba AD promotion check: DNS, forwarding, ports, Kerberos, shares, database and GPOs",
    "Mot de passe Administrator (facultatif, tests Kerberos et partages)":
        "Administrator password (optional, Kerberos and share tests)",
    "Nom externe pour la redirection DNS": "External name for DNS forwarding",
    "Lancer les tests": "Run the tests",
    "Exporter le rapport": "Export the report",
    "Fermer": "Close",
    "Categorie": "Category",
    "Test": "Test",
    "Resultat": "Result",
    "Detail": "Detail",
    "Pret : lancez les tests.": "Ready: run the tests.",
    "Tests en cours, cela peut prendre une minute...": "Tests running, this can take a minute...",
    "Les tests n'ont pas pu etre lances : {}": "The tests could not be run: {}",
    "Reponse illisible du programme de test.": "Unreadable answer from the test program.",
    "Controleur {} - domaine {} : {}": "Controller {} - domain {}: {}",
    "Rapport enregistre : {}": "Report saved: {}",
    "Aucun resultat a exporter.": "No result to export.",
    "OK": "OK",
    "AVERTISSEMENT": "WARNING",
    "ECHEC": "FAILED",
    "IGNORE": "SKIPPED",
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


STATUS = {
    "ok": (tr("OK"), "#16794c"),
    "warn": (tr("AVERTISSEMENT"), "#b54708"),
    "fail": (tr("ECHEC"), "#b42318"),
    "skip": (tr("IGNORE"), "#6b7280"),
}


class CheckApp(tk.Frame):
    def __init__(self, master):
        super().__init__(master, background="#f3f5f8")
        self.master = master
        self.grid(row=0, column=0, sticky="nsew")
        master.grid_rowconfigure(0, weight=1)
        master.grid_columnconfigure(0, weight=1)
        self.grid_rowconfigure(2, weight=1)
        self.grid_columnconfigure(0, weight=1)
        self.password = tk.StringVar()
        self.external_name = tk.StringVar(value="debian.org")
        self.status = tk.StringVar(value=tr("Pret : lancez les tests."))
        self.queue = queue.Queue()
        self.report = None
        self._configure_style()
        self._build_header()
        self._build_options()
        self._build_results()
        self._build_footer()

    def _configure_style(self):
        style = ttk.Style(self.master)
        style.theme_use("clam")
        style.configure("Wizard.TEntry", fieldbackground="#ffffff", padding=7)
        style.configure("Primary.TButton", background="#0078d4", foreground="#ffffff", padding=(18, 8))
        style.map("Primary.TButton", background=[("active", "#106ebe"), ("disabled", "#a7c7e7")])
        style.configure("Secondary.TButton", background="#ffffff", foreground="#202020", padding=(16, 8))
        style.map("Secondary.TButton", background=[("active", "#e5e7eb")])
        style.configure("Wizard.Horizontal.TProgressbar", troughcolor="#d9e2ec", background="#0078d4")
        style.configure("Treeview", rowheight=26, font=("Segoe UI", 9))
        style.configure("Treeview.Heading", font=("Segoe UI", 9, "bold"))

    def _build_header(self):
        header = tk.Frame(self, background="#ffffff", height=82, highlightbackground="#d8dde3", highlightthickness=1)
        header.grid(row=0, column=0, sticky="ew")
        header.grid_propagate(False)
        tk.Label(header, text=tr("Tests du controleur de domaine"), background="#ffffff", foreground="#1f2937",
                 font=("Segoe UI", 16, "bold")).pack(anchor="w", padx=28, pady=(17, 2))
        tk.Label(header, text=tr("Verification de la promotion Samba AD : DNS, redirection, ports, Kerberos, partages, base et GPO"),
                 background="#ffffff", foreground="#5f6b7a", font=("Segoe UI", 9)).pack(anchor="w", padx=29)

    def _build_options(self):
        panel = tk.Frame(self, background="#ffffff", highlightbackground="#d8dde3", highlightthickness=1)
        panel.grid(row=1, column=0, sticky="ew", padx=20, pady=(16, 8))
        panel.grid_columnconfigure(1, weight=1)
        for row, (label, variable, secret) in enumerate((
            (tr("Mot de passe Administrator (facultatif, tests Kerberos et partages)"), self.password, True),
            (tr("Nom externe pour la redirection DNS"), self.external_name, False),
        )):
            tk.Label(panel, text=label, background="#ffffff", foreground="#202020",
                     font=("Segoe UI", 9)).grid(row=row, column=0, sticky="w", padx=(20, 14), pady=8)
            ttk.Entry(panel, textvariable=variable, show="*" if secret else "",
                      style="Wizard.TEntry").grid(row=row, column=1, sticky="ew", padx=(0, 20), pady=8)

    def _build_results(self):
        frame = tk.Frame(self, background="#f3f5f8")
        frame.grid(row=2, column=0, sticky="nsew", padx=20, pady=4)
        frame.grid_rowconfigure(0, weight=1)
        frame.grid_columnconfigure(0, weight=1)
        columns = ("category", "test", "result", "detail")
        self.tree = ttk.Treeview(frame, columns=columns, show="headings", selectmode="browse")
        for column, title, width in zip(columns, (tr("Categorie"), tr("Test"), tr("Resultat"), tr("Detail")),
                                        (140, 330, 120, 420)):
            self.tree.heading(column, text=title, anchor="w")
            self.tree.column(column, width=width, anchor="w", stretch=column == "detail")
        for status, (_label, color) in STATUS.items():
            self.tree.tag_configure(status, foreground=color)
        scrollbar = ttk.Scrollbar(frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scrollbar.set)
        self.tree.grid(row=0, column=0, sticky="nsew")
        scrollbar.grid(row=0, column=1, sticky="ns")
        self.detail = tk.Text(frame, height=4, wrap="word", background="#ffffff", relief="flat",
                              font=("Segoe UI", 9), state="disabled")
        self.detail.grid(row=1, column=0, columnspan=2, sticky="ew", pady=(8, 0))
        self.tree.bind("<<TreeviewSelect>>", self._show_detail)

    def _build_footer(self):
        footer = tk.Frame(self, background="#ffffff", height=64, highlightbackground="#d8dde3", highlightthickness=1)
        footer.grid(row=3, column=0, sticky="ew", pady=(10, 0))
        footer.grid_propagate(False)
        # Grille plutot que pack : seule la colonne du statut retrecit et passe a la
        # ligne. Un long resume apres les tests ne pousse plus les boutons hors de
        # la fenetre.
        footer.grid_rowconfigure(0, weight=1)
        footer.grid_columnconfigure(0, weight=1)
        status_label = tk.Label(footer, textvariable=self.status, background="#ffffff", foreground="#5f6b7a",
                                font=("Segoe UI", 9), anchor="w", justify="left", width=1)
        status_label.grid(row=0, column=0, sticky="ew", padx=(20, 10))
        status_label.bind("<Configure>", lambda event: status_label.configure(wraplength=max(event.width - 4, 100)))
        self.progress = ttk.Progressbar(footer, mode="indeterminate", length=160,
                                        style="Wizard.Horizontal.TProgressbar")
        self.progress.grid(row=0, column=1, padx=10)
        self.progress.grid_remove()
        self.run_button = ttk.Button(footer, text=tr("Lancer les tests"), style="Primary.TButton",
                                     command=self.start)
        self.run_button.grid(row=0, column=2, padx=6)
        self.export_button = ttk.Button(footer, text=tr("Exporter le rapport"), style="Secondary.TButton",
                                        command=self.export)
        self.export_button.grid(row=0, column=3, padx=6)
        self.export_button.state(["disabled"])
        ttk.Button(footer, text=tr("Fermer"), style="Secondary.TButton",
                   command=self.master.destroy).grid(row=0, column=4, padx=(6, 20))

    def start(self):
        self.run_button.state(["disabled"])
        self.export_button.state(["disabled"])
        self.tree.delete(*self.tree.get_children())
        self.status.set(tr("Tests en cours, cela peut prendre une minute..."))
        self.progress.grid()
        self.progress.start(12)
        command = ([] if os.geteuid() == 0 else ["pkexec"]) + [
            "/usr/bin/python3", CHECK, "--json", "--password-stdin",
            "--external-name", self.external_name.get().strip() or "debian.org",
        ]
        password = self.password.get()
        self.password.set("")
        threading.Thread(target=self._run, args=(command, password), daemon=True).start()
        self.master.after(150, self._poll)

    def _run(self, command, password):
        try:
            # Langue de l'interface transmise au moteur, que pkexec lance sans environnement.
            if command[0] == "pkexec":
                command = ["pkexec", "env", f"LANGUAGE={LANGUAGE}"] + command[1:]
            result = subprocess.run(command, input=password + "\n", text=True, capture_output=True, check=False)
            self.queue.put(("done", result))
        except OSError as error:
            self.queue.put(("error", str(error)))

    def _poll(self):
        try:
            kind, payload = self.queue.get_nowait()
        except queue.Empty:
            self.master.after(150, self._poll)
            return
        self.progress.stop()
        self.progress.grid_remove()
        self.run_button.state(["!disabled"])
        if kind == "error":
            self.status.set(tr("Les tests n'ont pas pu etre lances : {}").format(payload))
            return
        try:
            report = json.loads(payload.stdout.strip().splitlines()[-1])
        except (IndexError, ValueError):
            details = (payload.stderr or payload.stdout).strip()
            self.status.set(tr("Reponse illisible du programme de test."))
            messagebox.showerror(tr("Tests du controleur de domaine"), details[-1500:] or tr("Reponse illisible du programme de test."),
                                 parent=self.master)
            return
        if "error" in report:
            self.status.set(report["error"])
            return
        self.report = report
        for result in report["results"]:
            label, _color = STATUS[result["status"]]
            self.tree.insert("", "end", values=(result["category"], result["title"], label, result["detail"]),
                             tags=(result["status"],))
        self.status.set(tr("Controleur {} - domaine {} : {}").format(report["controller"], report["realm"], report["summary"]))
        self.export_button.state(["!disabled"])
        self.master.lift()

    def _show_detail(self, _event):
        selection = self.tree.selection()
        if not selection:
            return
        values = self.tree.item(selection[0], "values")
        self.detail.configure(state="normal")
        self.detail.delete("1.0", "end")
        self.detail.insert("end", f"{values[1]}\n{values[2]} - {values[3]}")
        self.detail.configure(state="disabled")

    def export(self):
        if not self.report:
            messagebox.showinfo(tr("Tests du controleur de domaine"), tr("Aucun resultat a exporter."), parent=self.master)
            return
        default = f"sambaad-check-{datetime.now():%Y%m%d-%H%M%S}.txt"
        path = filedialog.asksaveasfilename(parent=self.master, initialfile=default, defaultextension=".txt",
                                            filetypes=(("Texte", "*.txt"), ("JSON", "*.json")))
        if not path:
            return
        if path.endswith(".json"):
            content = json.dumps(self.report, ensure_ascii=False, indent=2)
        else:
            lines = [tr("Controleur {} - domaine {} : {}").format(
                self.report["controller"], self.report["realm"], self.report["summary"]), ""]
            for result in self.report["results"]:
                lines.append(f"[{result['category']}] {STATUS[result['status']][0]:13} {result['title']}"
                             + (f" - {result['detail']}" if result["detail"] else ""))
            content = "\n".join(lines) + "\n"
        Path(path).write_text(content, encoding="utf-8")
        self.status.set(tr("Rapport enregistre : {}").format(path))


def main():
    root = tk.Tk()
    root.title(tr("Tests du controleur de domaine") + " - Harden Box")
    root.geometry("1080x680")
    root.minsize(900, 560)
    CheckApp(root)
    root.mainloop()


if __name__ == "__main__":
    main()

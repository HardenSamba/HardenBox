# SambaAD — ADWS : instructions projet

## 1. Contexte
Implémentation Python des Active Directory Web Services (TCP 9389) pour
un DC Samba AD. Point d'entrée unique : `python3 -m src.main`.

« Souverain » = ouvert, auditable, sans binaire ni code Microsoft en
production. Le service fait le pont entre les requêtes WS-* des clients
Windows et l'annuaire Samba (LDB).

État actuel : phases 1→4 TERMINÉES (CRUD, recherche/énumération LDAP,
opérations de groupe, compat module PowerShell ActiveDirectory + ADAC).
Phase 5 EN COURS : performance + durcissement sécurité.
→ Le travail est de MAINTENIR et ÉTENDRE, pas de réécrire.

## 2. Avant toute chose : lire l'existant
1. `documentation/` est la source de vérité (FR). Lire dans l'ordre :
   01-architecture → 03-protocole → 04-handlers, puis 05-tests / 06-depannage.
2. Référence protocolaire :
   - Microsoft Open Specifications = LA SPEC, fait foi.
   - `tools/work/windows/ILSpy/` (C# décompilé) = clarifie les comportements non
     documentés UNIQUEMENT. Ne jamais recopier — réimplémenter d'après
     la spec.

## 3. Comportement attendu (déjà acquis, à ne pas régresser)
Depuis une machine membre du domaine, contre l'annuaire Samba :
- Lecture : Get-ADUser/Computer/Group/Object/Domain/DomainController/Forest,
  Get-ADRootDSE, recherches et énumérations filtrées/paginées.
- Écriture : New-AD*, Set-AD*, Remove-AD*, Set-ADAccountPassword.
- Groupes : Get-ADGroupMember, Get-ADPrincipalGroupMembership (+ récursif).
- Auth intégrée Kerberos via GSSAPI, respect des droits AD.

## 4. Protocole — ancrage technique
TCP 9389. Empilement (bas → haut) :
- [MS-NMF] cadrage .NET Message Framing       → src/nettcp/nmf.py
- GSSAPI/SPNEGO (Kerberos AES, seal/unseal)   → src/nettcp/stream/gssapi.py
  (côté Windows c'est NegotiateStream [MS-NNS] ; côté serveur Linux on
   passe par samba.gensec, pas de NegotiateStream .NET)
- [MS-NBFX]/[MS-NBFS] XML binaire WCF + dict. → src/wcf/
- SOAP 1.2 + WS-Addressing
- [MS-ADDM] data model AD, [MS-WSTIM] Get/Put IMDA,
  [MS-WSDS] énumération, [MS-WSPELD] contrôles LDAP v3

## 5. Invariants NON NÉGOCIABLES (pièges du projet)
- Serveur en `ForkingTCPServer` : `python3-samba` n'est PAS thread-safe.
  → JAMAIS ouvrir `SamDB` au niveau module. Passer par le helper
    `classes/directory/SambaDB` (ouverture paresseuse par process).
- Tout objet rendu : vérifier `context.is_imda` et choisir le bon rendu
  (IMDA = un `<da:PartialAttribute>` par attribut ; WS-Transfer = wrapper
  objectClass). Mauvais wrapper → la cmdlet Windows casse.
- En IMDA, toujours émettre `distinguishedName` comme attribut explicite.
- Toute erreur = `ADWSException` typée (jamais `Exception` nue) ; le
  dispatcher la traduit en SOAP fault.
- Tout fault DOIT porter `<a:RelatesTo>{message_id}` sinon PowerShell
  ignore la réponse et la cmdlet bloque jusqu'au timeout.
- Un fichier = une action SOAP, héritant de `BaseHandler.handle()`.

## 6. Normes de code
Langue : prose/doc en FR, code/identifiants/commits en EN.

### 6.1 Imposées (bloquantes)
- 1 classe max et 5 fonctions max par fichier.
- Fonction ≤ 20 lignes (hors lignes vides).
- Imbrication ≤ 2 niveaux.
- Ligne ≤ 100 caractères.
- Typage complet : tout paramètre, retour et variable non triviale est
  annoté. `mypy --strict` (platform=linux, ignore_missing_imports car
  samba/ldb n'ont pas de stubs).
- Pas d'état global mutable ; constantes UPPER_CASE.
- snake_case / PascalCase / UPPER_CASE.
- Pas de `import *`, pas de `except:` nu, pas de défaut mutable.
- En-tête obligatoire : toute `class` et toute fonction est précédée d'un
  bloc de commentaires `#` (1 à 3 lignes) décrivant ce qu'elle fait,
  immédiatement au-dessus du `def`/`class` (et de ses décorateurs). Format :
  `# Résout le DN cible, ajoute l'objet, renvoie l'EPR ResourceCreated.`
  Décrire le QUOI (rôle), pas le COMMENT. Pas d'autre commentaire en ligne
  dans le corps : le nom + les types documentent le détail. Docstring de
  module : une ligne max, optionnelle.

### 6.2 Cibles (viser, mesurer ; non bloquant rétroactif)
- McCabe ≤ 5, ≤ 4 paramètres/fonction.

### 6.3 Enforcement
- Vérificateur AST maison `tools/norms` (1 classe / 5 fonctions, ≤20
  lignes, imbrication ≤2, ligne ≤100) — lui-même conforme aux normes.
- ruff + flake8 + flake8-functions (style, naming, imports, longueur).
- mypy --strict (platform=linux) pour le typage complet.
- pylint en complément (max-nested-blocks=2, max-args=4).
- pre-commit : `tools/norms` + ruff + flake8 + mypy --strict BLOQUANTS sur
  `src/` (entièrement typé). pylint en stage `manual`.

## 7. Tests (cf. 05-tests.md) — workflow
NB : `test_adws.py` (diagnostic, tourne partout) et `test_handlers.py`
(handlers isolés, DC-only) sont dans `tools/test/`.
1. `test_adws.py` (diagnostic env) sur le DC.
2. `test_handlers.py` (handlers isolés, sans réseau) sur le DC.
3. `python3 -m src.main` puis `Test-ADWS-Sovereign.ps1 -SkipMutations`,
   puis sans `-SkipMutations`.
Toute correction d'un comportement décrit met à jour la doc dans le
même commit.

## 8. À préciser (hors dépôt)
- Version de Samba AD ciblée, version Windows des clients, niveau
  fonctionnel du domaine.

## 9. Livrables
- Code conforme à §6.1 (et §6.2 selon décisions).
- Configs linters + hook pre-commit + vérificateur AST maison.
- Les deux suites de test Python manquantes (§7).
- Doc `documentation/` tenue à jour avec tout changement de comportement.

## 10. Outillage agent (skills & agents projet)
Disponibles dans `.claude/` (créés pour ce projet) :
- Skill `add-handler` — scaffold un handler SOAP + câblage routing.
- Skill `check-norms` — lance `python -m tools.norms` (vérificateur §6.1).
- Skill `decode-wcf` — décode un payload binaire WCF → XML (debug).
- Agent `adws-protocol-expert` — recherche protocolaire (specs + C#).
- Agent `norms-auditor` — audit normes §6.1 + invariants §5 sur un diff.
- Agent `interop-parity-auditor` — diff statique de notre sérialisation `classes/`/`src/`
  contre la référence C# `tools/work/windows/ILSpy/` (xsi:type, LdapSyntax, namespaces EPR,
  table RootDSE). Préventif.
- Agent `wire-triage` — forensic réactif : d'un payload WCF capturé / d'une
  stack trace .NET vers la cause racine et le `file:line` à corriger.
- Agent `doc-sync-auditor` — vérifie qu'un changement de comportement met à jour
  `documentation/` (§7/§9).
- Agent `regression-tester` — lance l'échelle de tests (§7) et signale les
  régressions vs le dernier run (`tools/test/adws-test-results/*.json`).
- Agent `adws-coverage-auditor` — autorité sur la surface fonctionnelle ADWS
  complète ; repère les fonctions manquantes/non testées et écrit les tests
  (boucle : couverture → tests → régression-tester → correctifs).

Enforcement : `tools/norms/` (vérificateur AST, lui-même conforme §6.1),
branché dans `.pre-commit-config.yaml` (bloquant §6.1 sur `src/` et `classes/` ; mypy /
pylint / longueur de ligne en stage `manual`). Configs : `pyproject.toml`,
`.flake8`, `.pylintrc`. Outils dev : `tools/installation/requirements-dev.txt`.

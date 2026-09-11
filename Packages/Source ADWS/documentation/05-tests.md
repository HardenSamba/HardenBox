# Tests

Trois suites de tests, à des niveaux différents.

| Suite | Où | Quoi | Quand |
|---|---|---|---|
| `Test-ADWS-Sovereign.ps1` | Windows | Bout en bout, vraies cmdlets PowerShell | Validation finale |
| `test_handlers.py` | DC Linux | Handlers en isolation (sans réseau) | Avant déploiement |
| `test_adws.py` | DC Linux | Diagnostic (port, imports, parser, …) | Premier démarrage |

---

## 1. `Test-ADWS-Sovereign.ps1` (validation principale)

Suite Pester-like en pur PowerShell. **C'est le test de référence** : si tout passe ici, le serveur est utilisable en production.

### Lancer

```powershell
.\tools\test\Test-ADWS-Sovereign.ps1 -Server samba.local
```

Paramètres :

| Paramètre | Défaut | Description |
|---|---|---|
| `-Server` | (requis) | FQDN ou IP du DC Samba |
| `-Credential` | (interactif) | `pscredential` admin domaine. Demandé en interactif sinon |
| `-OutputDirectory` | `.\adws-test-results` | Répertoire de sortie |
| `-TestUserPrefix` | `adwsTest` | Préfixe pour les comptes de test temporaires |
| `-SkipMutations` | `$false` | Ne fait que des lectures (Create/Modify/Delete sautés) |
| `-SkipStress` | `$false` | Saute le test de charge (50 requêtes parallèles) |

### Catégories couvertes

| Catégorie | Tests | Notes |
|---|---|---|
| `Connectivity` | `Get-ADRootDSE`, `Get-ADDomain`, `Get-ADForest`, `Get-ADDomainController` | Doit toujours passer |
| `Read` | `Get-ADUser`, `Get-ADGroup`, `Get-ADComputer -Filter *` | Vérifie le rendu IMDA + WS-Transfer |
| `Enumerate` | `Get-ADUser -Filter *`, filtres AND/OR/NOT, pagination `Get-ADObject` | Couvre `Enumerate`/`Pull`/`Release` |
| `Create` / `Modify` / `Delete` | Cycle complet sur un user `${TestUserPrefix}-{ts}` | Skipé avec `-SkipMutations` |
| `SetPassword` / `ChangePassword` | Reset puis change avec ancien mdp | Vérifie GSSAPI seal et politiques de mot de passe |
| `GroupRead` / `GroupModify` | Crée groupe + 2 membres, ajoute, liste, récursif, supprime | |
| `Schema` | Liste 10 `classSchema` + 10 `attributeSchema` | Lit la partition Schema |
| `Stress` | 50 `Get-ADUser -Filter *` en parallèle (throttle 10) | Skipé avec `-SkipStress` |

### Sorties

Deux fichiers horodatés sont créés dans `-OutputDirectory` :

- `adws-test-{timestamp}.log` — texte lisible, une section par test
- `adws-test-{timestamp}.json` — résultat structuré pour CI/automatisation

Format JSON :

```json
[
  {
    "Name": "Get-ADUser Administrator -Properties *",
    "Category": "Read",
    "StartTime": "2026-04-26T10:23:45.123+02:00",
    "Status": "Pass",
    "DurationMs": 142,
    "Output": "...",
    "Error": null
  },
  …
]
```

`Status` vaut `Pass` ou `Fail`. Avec `$ErrorActionPreference = "Stop"` (défini en haut du script), toute exception PowerShell coupe le test → un faux Pass est impossible.

### Lecture rapide d'un run

```powershell
$results = Get-Content .\adws-test-results\adws-test-*.json | ConvertFrom-Json
$results | Group-Object Status | Format-Table Count, Name
$results | Where-Object Status -eq Fail | Select-Object Name, Error
```

---

## 2. `test_handlers.py` (handlers en isolation)

À exécuter sur **le DC Samba** :

```bash
cd /root/SambaAD_ADWS
python3 tools/test/test_handlers.py
```

Charge directement les handlers Python et leur passe des `RequestContext` construits à la main (pas de NMF, pas de WCF, pas de réseau). Utile pour :

- isoler un bug applicatif d'un bug protocolaire,
- ajouter un cas de régression sans devoir tout déployer,
- itérer rapidement pendant l'écriture d'un nouveau handler.

---

## 3. `test_adws.py` (diagnostic)

```bash
python3 tools/test/test_adws.py
```

Vérifie en séquence :

1. Ouverture du port `9389/tcp`.
2. Imports critiques (`samba`, `ldb`, `gssapi`, `lxml`).
3. Connexion `SamDB` (lecture du DN par défaut).
4. Parser XML/WCF (encode/decode aller-retour).
5. Instanciation de chaque handler.
6. Routage via `HandlerRegistry`.

C'est la première chose à lancer après installation pour confirmer que l'environnement est sain — bien avant d'essayer une cmdlet PowerShell.

---

## 4. Validation contre un vrai client Windows

La validation de référence se fait depuis un **poste Windows joint au domaine**,
avec le module `ActiveDirectory` réel, en exécutant
`.\tools\test\Test-ADWS-Sovereign.ps1 -Server <DC>` (cf. §1).

### État — validé contre un vrai client Windows 11 (8 juin 2026)

Testé depuis un poste **Windows 11 joint au domaine** (`samba.local`), module
`ActiveDirectory` réel, contre le serveur sur le DC Samba. Suite
`Test-ADWS-Sovereign.ps1` : **26/28**.

- ✅ Connexion + authentification (NMF + GSSAPI/Negotiate + WCF binaire).
- ✅ Lecture : RootDSE, Domain/Forest/DomainController,
  Get-ADUser/Group/Computer/Object, énumérations filtrées (AND/OR/bitwise-NOT)
  et paginées, découverte de schéma.
- ✅ Écriture : New-AD*, Set-AD*, Remove-AD*, `Set-ADAccountPassword -Reset`,
  opérations de groupe (membres directs + récursifs).
- ✅ Admin avancé — **batterie de parité 18/18** : OU (New/Get/Remove),
  `Enable`/`Disable-ADAccount`, `Rename-ADObject`, `Move-ADObject`, suppression
  récursive (`-Recursive`), `Search-ADAccount` (-AccountDisabled/-Inactive/
  -LockedOut), pipeline `Get-AD*|Set-AD*`, `Get-ADUser -Properties *`.
- ⚠️ `Set-ADAccountPassword` **changement self-service** (ancien→nouveau) :
  échoue côté Samba (`Failed to obtain remote address for the LDAP client`).
  Le reset administrateur fonctionne. Voir [limites connues](01-architecture.md#limites-connues).
- ⚠️ Test `Stress` (50 requêtes parallèles) : limite de l'harnais
  (`ForEach-Object -Parallel` absent de Windows PowerShell 5.1), pas du serveur.

**Cause racine du blocker RootDSE historique (résolu).** Le client n'accepte
sur le fil que deux `xsi:type` : `xsd:string` et `xsd:base64Binary` (réf.
`DirectoryValue.Get/SetXmlValue`). Le serveur émettait `xsd:integer` / `long`
/ `boolean` / `dateTime` → `KeyNotFoundException` dans
`ADValueSerializer.Deserialize`. Le type LDAP réel est porté par `LdapSyntax`,
jamais par `xsi:type`. Corrigé dans `value_codec.py`, `value_special.py`,
`syntax_constants.py`. `New-ADUser` exigeait en plus la résolution du DN via
`relativeDistinguishedName` + `container-hierarchy-parent`, et une réponse
`ResourceCreated` dont `Address`/`ReferenceParameters` sont en namespace
WS-Addressing (`wsa`, pas `wst`) — corrigé dans `handlers/create.py`.

**Autres correctifs de parité.** `Rename-ADObject`/`Move-ADObject` arrivent en
Put modifiant `relativeDistinguishedName`/`container-hierarchy-parent` : traités
en modrdn LDB (`db.rename`) dans `handlers/modify.py`. La suppression récursive
(`Remove-AD* -Recursive`) honore le contrôle tree-delete dans
`handlers/delete.py`. Les filtres à opérande NOT « nu » (`(!attr=val)`, envoyés
par `Search-ADAccount`, tolérés par AD mais refusés par ldb) sont normalisés en
`(!(attr=val))` par `filter_parser.py`.

---

## Workflow recommandé

```
                ┌────────────────────────────┐
                │ 1. Modification de code    │
                └─────────────┬──────────────┘
                              ▼
                ┌────────────────────────────┐
                │ 2. test_adws.py            │  ← imports + sanité
                │    sur DC                  │
                └─────────────┬──────────────┘
                              ▼
                ┌────────────────────────────┐
                │ 3. test_handlers.py        │  ← logique isolée
                │    sur DC                  │
                └─────────────┬──────────────┘
                              ▼
                ┌────────────────────────────┐
                │ 4. python3 -m src.main         │  ← serveur live
                │    sur DC                  │
                └─────────────┬──────────────┘
                              ▼
                ┌────────────────────────────┐
                │ 5. Test-ADWS-Sovereign.ps1 │  ← E2E réel
                │    -SkipMutations          │     (lectures d'abord)
                └─────────────┬──────────────┘
                              ▼
                ┌────────────────────────────┐
                │ 6. Test-ADWS-Sovereign.ps1 │  ← E2E + writes
                │    (sans -SkipMutations)   │
                └────────────────────────────┘
```

À chaque étape qui échoue, on remonte pas plus loin que nécessaire :

- Tout `Connectivity` rouge → reprendre à l'étape 4 et lire les logs serveur.
- Tout `Read` rouge mais `Connectivity` vert → bug dans `Get`/`Enumerate`/`Pull` (étape 3).
- Tout `Mutation` rouge mais `Read` vert → bug dans `Create`/`Modify`/`Delete` (étape 3).

Les erreurs typiques côté serveur sont décodées dans [06-depannage.md](06-depannage.md).

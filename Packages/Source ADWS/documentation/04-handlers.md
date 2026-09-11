# Handlers ADWS

Un handler = un fichier dans [classes/handlers/](../classes/handlers/). Il traite **une action SOAP**.

## Catalogue

### WS-Transfer (objet unique)

| Action SOAP | Handler | Cmdlet PowerShell typique |
|---|---|---|
| `…/transfer/Get` | [`get.py`](../classes/handlers/get.py) — `GetHandler` | `Get-ADUser`, `Get-ADRootDSE`, `Get-ADObject` |
| `…/transfer/Put` | [`transfer.py`](../classes/handlers/transfer.py) — `TransferHandler` | `Set-ADUser` (forme par DN) |
| `…/transfer/Delete` | [`transfer.py`](../classes/handlers/transfer.py) | `Remove-ADUser` |
| `…/transfer/Create` | [`crud.py`](../classes/handlers/crud.py) — `CRUDHandler` | `New-ADUser`, `New-ADGroup` |

### WS-Enumeration (recherche paginée)

| Action SOAP | Handler | Description |
|---|---|---|
| `…/enumeration/Enumerate` | [`enumerate.py`](../classes/handlers/enumerate.py) — `EnumerateHandler` | Démarre une session d'énumération, exécute le `samdb.search()`, stocke les résultats sous un UUID |
| `…/enumeration/Pull` | [`pull.py`](../classes/handlers/pull.py) — `PullHandler` | Tire un lot d'objets depuis le contexte UUID |
| `…/enumeration/Release` | [`release.py`](../classes/handlers/release.py) — `ReleaseHandler` | Libère le contexte côté serveur |
| `…/enumeration/Renew` | [`renew.py`](../classes/handlers/renew.py) — `RenewHandler` | Prolonge l'expiration du contexte |
| `…/enumeration/GetStatus` | [`get_status.py`](../classes/handlers/get_status.py) — `GetStatusHandler` | Statut/expiration du contexte |

### Actions ADWS spécifiques

| Action (suffixe URL) | Handler | Cmdlet associée |
|---|---|---|
| `Search` | [`search.py`](../classes/handlers/search.py) | recherches IMDA arbitraires |
| `Schema` | [`schema.py`](../classes/handlers/schema.py) | introspection de classes/attributs |
| `Groups` | [`groups.py`](../classes/handlers/groups.py) | opérations groupe en bloc |
| `GetADGroupMember` | [`get_ad_group_member.py`](../classes/handlers/get_ad_group_member.py) | `Get-ADGroupMember` |
| `GetADPrincipalGroupMembership` | [`get_ad_principal_group_membership.py`](../classes/handlers/get_ad_principal_group_membership.py) | `Get-ADPrincipalGroupMembership` |
| `GetADPrincipalAuthorizationGroup` | [`get_ad_principal_authorization_group.py`](../classes/handlers/get_ad_principal_authorization_group.py) | `Get-ADPrincipalAuthorizationGroup` (token groups récursif) |
| `SetPassword` | [`set_password.py`](../classes/handlers/set_password.py) | `Set-ADAccountPassword -Reset` |
| `ChangePassword` | [`change_password.py`](../classes/handlers/change_password.py) | `Set-ADAccountPassword` (avec ancien mdp) |
| `TranslateName` | [`translate_name.py`](../classes/handlers/translate_name.py) | conversion DN ↔ NT4 ↔ UPN ↔ canonical |
| `GetADDomain` | [`get_ad_domain.py`](../classes/handlers/get_ad_domain.py) | `Get-ADDomain` |
| `GetADDomainController` | [`get_ad_domain_controller.py`](../classes/handlers/get_ad_domain_controller.py) | `Get-ADDomainController` |
| `GetADForest` | [`get_ad_forest.py`](../classes/handlers/get_ad_forest.py) | `Get-ADForest` |
| `GetVersion` | [`get_version.py`](../classes/handlers/get_version.py) | numéro de version ADWS |
| `MoveADOperationMasterRole` | [`move_operation_master_role.py`](../classes/handlers/move_operation_master_role.py) | `Move-ADDirectoryServerOperationMasterRole` (FSMO transfer/seize) |
| `ChangeOptionalFeature` | [`change_optional_feature.py`](../classes/handlers/change_optional_feature.py) | `Enable-ADOptionalFeature` |

### CRUD interne

| Fichier | Rôle |
|---|---|
| [`create.py`](../classes/handlers/create.py) — `CreateHandler` | Logique de création utilisée par `CRUDHandler` |
| [`modify.py`](../classes/handlers/modify.py) — `ModifyHandler` | Logique de modification utilisée par `TransferHandler` (Put) |
| [`delete.py`](../classes/handlers/delete.py) — `DeleteHandler` | Logique de suppression utilisée par `TransferHandler` (Delete) |

## Routage

Carte action → handler dans [`src/handlers/handler_routing.py`](../src/handlers/handler_routing.py) :

```python
ACTION_HANDLER_KEYS = {
    "http://schemas.xmlsoap.org/ws/2004/09/transfer/get":    "Get",
    "http://schemas.xmlsoap.org/ws/2004/09/transfer/create": "Create",
    …
}

HANDLER_FACTORIES = (
    ("Get",         GetHandler),
    ("Enumerate",   EnumerateHandler),
    …
)
```

`HandlerRegistry.resolve(action)` :

1. Cherche d'abord la correspondance **exacte** insensible à la casse dans `ACTION_HANDLER_KEYS`.
2. À défaut, prend le **dernier segment** de l'URL (`http://…/Foo/Bar` → `Bar`) et cherche dans `HANDLER_FACTORIES`.

C'est ce qui permet aux actions ADWS spécifiques (URLs propriétaires Microsoft) de tomber sur le bon handler par simple convention de nommage.

## Contrat d'un handler

Tous héritent de [`BaseHandler`](../classes/handlers/base.py) :

```python
class BaseHandler:
    def __init__(self, logger, session_info=None):
        self.logger = logger
        self.session_info = session_info

    def log_request(self, context: RequestContext) -> None: ...

    def handle(self, context: RequestContext) -> str:
        raise NotImplementedError
```

`handle()` doit :

1. Lire `context.action`, `context.message_id`, `context.base_dn`, `context.ldap_filter`, `context.is_imda`, etc.
2. Effectuer l'opération LDB via `samba.samdb`.
3. Retourner une **string XML SOAP complète** (enveloppe + headers + body).

Le dispatcher s'occupe de :
- la sérialisation XML → records WCF,
- du préfixage `\x00` (octet de framing WCF, **ne pas l'oublier**),
- de la conversion d'exceptions en SOAP fault avec `<a:RelatesTo>`.

## Ajouter un nouveau handler

Exemple : implémenter `GetADReplicationFailure`.

### 1. Créer le fichier

`classes/handlers/get_ad_replication_failure.py` :

```python
from .base import BaseHandler
from classes.parsing.context import RequestContext


class GetADReplicationFailureHandler(BaseHandler):
    def handle(self, context: RequestContext) -> str:
        self.log_request(context)
        body = self._build_body(context)
        return self.envelope.wrap(
            context.action + "Response", context.message_id, body)

    def _build_body(self, context: RequestContext) -> str:
        return "<adws:GetADReplicationFailureResponse xmlns:adws='…'/>"
```

> Respecter les règles projet : **5 fonctions max, 20 lignes max par fonction, pas de commentaire**.

### 2. L'exposer

Dans [`classes/handlers/__init__.py`](../classes/handlers/__init__.py) :

```python
from .get_ad_replication_failure import GetADReplicationFailureHandler
__all__ = [..., "GetADReplicationFailureHandler"]
```

### 3. L'enregistrer

Dans [`src/handlers/handler_routing.py`](../src/handlers/handler_routing.py) :

```python
from classes.handlers import (..., GetADReplicationFailureHandler)

HANDLER_FACTORIES = (
    …
    ("GetADReplicationFailure", GetADReplicationFailureHandler),
)
```

Le routage par dernier segment d'URL fait le reste : si PowerShell envoie `Action=…/GetADReplicationFailure`, le handler est trouvé.

### 4. Tester

```powershell
Get-ADReplicationFailure -Server samba.local
```

Et journaliser sur le DC :
```bash
sudo journalctl -u samba-adws -f | grep ReplicationFailure
```

## Helpers fréquemment utilisés

Dans `classes/` et `src/` (organisés par phase) :

| Module | Quand l'utiliser |
|---|---|
| `object_serializer.ObjectSerializer` | sérialise un objet entier (`whole_object` = forme WS-Transfer / IMDA forme b) ou la projection IMDA forme a (`imda_projection`). Partagé par `get.py` et `pull.py` |
| `xml_renderer.AttributeRenderer` | rendu d'un dict d'attributs LDB → `<addata:nom LdapSyntax="…">` (option `wrap_partial` pour envelopper chaque attribut dans `<da:PartialAttribute>`) |
| `value_codec.ValueCodec` | encodage **valeur** par syntaxe : string, integer, long, dateTime, boolean ; tout binaire (OctetString / SidString / NTSecurityDescriptor) → `xsd:base64Binary` |
| `syntaxes.SyntaxRegistry` | `syntax_for` (OID interne, pilote l'encodage de valeur) **et** `ldap_syntax_name_for` (nom d'énum `DirectorySyntaxType` pour l'attribut `LdapSyntax`) |
| lecture `nTSecurityDescriptor` | ressort en `base64Binary` brut, comme l'ADWS réel ; le codec SDDL côté écriture n'est pas encore câblé |
| `filter_parser.LdapFilterParser` | parser LDAP RFC 4515 (utilisé surtout dans `enumerate.py` et `search.py`) |
| `ldap_attr_builder.LdapAttributeBuilder` | construction d'objets LDB pour Create/Modify |
| `domain_loader.DomainLoader` | infos domaine cachées (DN, NetBIOS, GUID, …) |
| `principal_loader.PrincipalLoader` | résolution `sAMAccountName` ↔ DN ↔ SID |
| `instance_map.InstanceMap` | identifiants `wsa:InstanceId` AD (`11111111-1111-1111-1111-111111111111`, …) |
| `typed_faults` + `fault_codes` | lever une `ADWSException` typée pour qu'elle ressorte avec un code MS standard |

## Bonnes pratiques

- **Toujours** lever une `ADWSException` (ou une de ses sous-classes typées) plutôt qu'une `Exception` brute. Le dispatcher saura traduire en SOAP fault correct.
- **Toujours** consulter `context.is_imda` avant de générer des objets : un mauvais wrapper plante l'analyseur XML côté Windows et la cmdlet retourne `Site distant a fermé le flux`.
- **Toujours** ajouter `distinguishedName` comme attribut explicite quand on rend un objet en IMDA (PowerShell le réclame en clé d'identité).
- **Ne pas ouvrir** `SamDB` au niveau module : le serveur fork à chaque connexion. Ouvrir dans `handle()` ou utiliser `ldap_session_pool` qui gère ça.
- Pour les opérations longues (énumérations volumineuses), respecter la limite `MaxElements` du Pull et stocker la suite dans `enumeration.EnumerationContext`.

## Format fil de sérialisation (vérifié sur l'ADWS Microsoft décompilé)

Toute la sérialisation d'objets passe par `ObjectSerializer`. Les règles
ci-dessous reproduisent le comportement de `ObjectSerializer.cs`,
`DirectoryValue.cs` et `DirectoryDataAccessImplementation.cs`.

### Attribut `LdapSyntax` = nom d'énumération, jamais un OID

L'attribut `LdapSyntax` porte le **nom de membre** de l'énumération
`DirectorySyntaxType` (`ObjectSerializer.cs` → `attribute.Syntax.ToString()`),
p. ex. `UnicodeString`, `Integer`, `LargeInteger`, `Boolean`,
`GeneralizedTimeString`, `ObjectIdentifier`, `DSDNString`, `OctetString`,
`SidString`, `NTSecurityDescriptor`. Émettre un OID (`2.5.4.3`, …) fait
échouer le `Enum.Parse` côté client : PowerShell **rejette** la réponse.
→ `SyntaxRegistry.ldap_syntax_name_for(attr)`.

### Type `xsi:type` de la valeur = type CLR runtime

`DirectoryValue.GetXmlValue` choisit `xsi:type` selon le **type .NET** de la
valeur, indépendamment de la syntaxe : `string` → `xsd:string`, `Guid` →
`xsd:string` (forme canonique), `byte[]` → `xsd:base64Binary`. Or l'ADWS
charge en `byte[]` tout attribut de syntaxe **OctetString, SidString,
ReplicaLink ou NTSecurityDescriptor** : `objectSid`, `objectGUID`,
`nTSecurityDescriptor`, `tokenGroups`, … sortent donc en `base64Binary` du
binaire **brut** (jamais une chaîne SDDL ni un GUID textuel).
→ `ValueCodec._encode_binary`.

> `objectReferenceProperty` est l'EPR (un `Guid` CLR) → `xsd:string` forme
> canonique. À ne pas confondre avec l'attribut `objectGUID` (base64).

### Attributs synthétiques en espace de noms `ad:` sans `LdapSyntax`

`distinguishedName`, `relativeDistinguishedName`, `objectReferenceProperty`,
`containerHierarchyParent` se rendent comme `<ad:Nom><ad:value
xsi:type="xsd:string">…</ad:value></ad:Nom>` — espace `ad:`, **sans**
`LdapSyntax`. Ils sont filtrés du flux d'attributs normaux pour éviter un
double `<addata:distinguishedName>`. → `ObjectSerializer.SYNTHETIC_KEYS`.

### Get IMDA — deux formes selon les `AttributeType` demandés

Discriminant : `context.is_imda and context.requested_attributes`
(cf. `ActiveDirectoryWebService.cs:260-375`).

- **Forme a** (≥1 `AttributeType` demandé) — `imda_projection` : un
  `<da:PartialAttribute>` **par attribut** demandé ; `distinguishedName`
  toujours émis (synthétique `ad:`). Pas de wrapper `<addata:classe>`.
- **Forme b** (aucun `AttributeType`) — l'objet entier dans **un seul**
  `<da:PartialAttribute>` (`_wrap_imda_object` autour de `whole_object`).

### WS-Transfer Get et items WS-Enumeration Pull = objet entier

`whole_object` produit `<addata:{classeStructurelle}>` =
`objectReferenceProperty` (si GUID connu) + attributs + `relativeDistinguishedName`
+ `distinguishedName`. Les items de `Pull` utilisent **exactement** cette
forme (et non des `PartialAttribute`). L'enveloppe Pull doit déclarer
`xsd` (les valeurs portent `xsi:type="xsd:…"`).

### RootDSE

`Get-ADRootDSE` passe par WS-Transfer Get → `<addata:top>` +
`objectReferenceProperty = 11111111-1111-1111-1111-111111111111`. Les
attributs opérationnels ont leurs syntaxes propres (`namingContexts` etc. =
`DSDNString`, `supportedControl` = `ObjectIdentifier`, `currentTime` =
`GeneralizedTimeString`, `highestCommittedUSN` = `LargeInteger`,
`isSynchronized` = `Boolean`, …), classées dans `syntax_constants.py`.

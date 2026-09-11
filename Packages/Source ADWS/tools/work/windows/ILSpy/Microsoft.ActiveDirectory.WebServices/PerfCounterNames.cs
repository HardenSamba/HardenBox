namespace Microsoft.ActiveDirectory.WebServices;

internal static class PerfCounterNames
{
	internal const int CurrentVersion = 6;

	internal static readonly string Category = "ADWS";

	internal static readonly string CreateOpsRate = AdwsStrings.CREATEOPSRATE_NAME;

	internal static readonly string DeleteOpsRate = AdwsStrings.DELETEOPSRATE_NAME;

	internal static readonly string GetOpsRate = AdwsStrings.GETOPSRATE_NAME;

	internal static readonly string PutOpsRate = AdwsStrings.PUTOPSRATE_NAME;

	internal static readonly string EnumOpsRate = AdwsStrings.ENUMOPSRATE_NAME;

	internal static readonly string PullOpsRate = AdwsStrings.PULLOPSRATE_NAME;

	internal static readonly string OpenEnumCtxs = AdwsStrings.OPENENUMCTXSCOUNT_NAME;

	internal static readonly string GetGroupMemberOpsRate = AdwsStrings.GETADGROUPMEMBEROPSRATE_NAME;

	internal static readonly string GetPrincGroupMembershipOpsRate = AdwsStrings.GETADPRINCGROUPMEMBERSHIPOPSRATE_NAME;

	internal static readonly string SetPassOpsRate = AdwsStrings.SETPASSOPSRATE_NAME;

	internal static readonly string ChangePassOpsRate = AdwsStrings.CHANGEPASSOPSRATE_NAME;

	internal static readonly string GetPrincAuthzGroupOpsRate = AdwsStrings.GETADPRINCAUTHZGROUPOPSRATES_NAME;

	internal static readonly string TranslateNameOpsRate = AdwsStrings.TRANSLATENAMESOPSRATE_NAME;

	internal static readonly string GetDomCtrlOpsRate = AdwsStrings.GETADDOMCTRLOPSRATE_NAME;

	internal static readonly string GetDomainOpsRate = AdwsStrings.GETADDOMAINOPSRATE_NAME;

	internal static readonly string MoveRoleOpsRate = AdwsStrings.MOVEROLEOPSRATE_NAME;

	internal static readonly string GetForestOpsRate = AdwsStrings.GETADFORESTOPSRATE_NAME;

	internal static readonly string ChangeOptionalFeatureOpsRate = AdwsStrings.CHANGEOPTIONALFEATUREOPSRATE_NAME;

	internal static readonly string GetVersionOpsRate = AdwsStrings.GETVERSIONOPSRATE_NAME;

	internal static readonly string NumberDirInstances = AdwsStrings.NUMDIRINSTANCES_NAME;

	internal static readonly string PossibleConns = AdwsStrings.POSSCONNSCOUNT_NAME;

	internal static readonly string AllocConns = AdwsStrings.ALLOCCONNSCOUNT_NAME;

	internal static readonly string ReservedConns = AdwsStrings.RESERVEDCONNSCOUNT_NAME;

	internal static readonly string NonReservedInUseConns = AdwsStrings.NONRESERVEDCONNSINUSECOUNT_NAME;

	internal static readonly string ReservedInUseConns = AdwsStrings.RESERVEDCONNSINUSECOUNT_NAME;

	internal static readonly string OpenWebServiceSessions = AdwsStrings.OPENWEBSERVICESESSIONCOUNT_NAME;

	internal static readonly string ActiveWebServiceSessions = AdwsStrings.ACTIVEWEBSERVICESESSIONCOUNT_NAME;

	internal static readonly string OpenWebServiceSessionsRate = AdwsStrings.OPENWEBSERVICESESSIONRATE_NAME;

	internal static readonly string CustActLdapPossibleConns = AdwsStrings.CUSTACT_LDAP_CACHE_POSSCONNSCOUNT_NAME;

	internal static readonly string CustActLdapCreateRate = AdwsStrings.CUSTACT_LDAP_CACHE_CREATERATE_NAME;

	internal static readonly string CustActLdapReuseRate = AdwsStrings.CUSTACT_LDAP_CACHE_REUSERATE_NAME;

	internal static readonly string CustActDsRpcPossibleConns = AdwsStrings.CUSTACT_DSRPC_CACHE_POSSCONNSCOUNT_NAME;

	internal static readonly string CustActDsRpcCreateRate = AdwsStrings.CUSTACT_DSRPC_CACHE_CREATERATE_NAME;

	internal static readonly string CustActDsRpcReuseRate = AdwsStrings.CUSTACT_DSRPC_CACHE_REUSERATE_NAME;

	internal static readonly string CustActCombinedConnCount = AdwsStrings.CUSTACT_COMBINED_CACHE_CONNCOUNT_NAME;
}

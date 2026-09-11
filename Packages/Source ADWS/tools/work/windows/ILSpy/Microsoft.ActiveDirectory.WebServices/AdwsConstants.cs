using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.ActiveDirectory.WebServices;

internal class AdwsConstants
{
	internal static readonly int VersionMajor = 1;

	internal static readonly int VersionMinor = 1;

	internal static readonly string VersionString = "Active Directory Web Services v1.1";

	internal static readonly string WcfNetEndpointPrefix = "net.tcp://";

	internal static readonly string WcfNetEndpointSuffix = ":9389/ActiveDirectoryWebServices";

	internal static readonly string DefaultDNSHostName = "localhost";

	internal static readonly Uri WcfNetEndpointBase = new Uri(WcfNetEndpointPrefix + DefaultDNSHostName + WcfNetEndpointSuffix);

	internal static readonly string WcfWindowsEndpointPrefix = "Windows/";

	internal static readonly string WcfUserNameEndpointPrefix = "UserName/";

	internal static readonly string WcfResourceEndpoint = "Resource";

	internal static readonly string WcfResourceFactoryEndpoint = "ResourceFactory";

	internal static readonly string WcfEnumEndpoint = "Enumeration";

	internal static readonly string WcfMexEndpoint = "mex";

	internal static readonly string WcfAcctMgmtEndpoint = "AccountManagement";

	internal static readonly string WcfTopoMgmtEndpoint = "TopologyManagement";

	internal static readonly string NetTcpBindingConfigName = "ActiveDirectoryWebServicesNetTcpBindingConfiguration";

	internal static readonly string RegKeyNtdsParameters = "System\\CurrentControlSet\\Services\\NTDS\\Parameters";

	internal static readonly string RegKeyServices = "System\\CurrentControlSet\\Services";

	internal static readonly string RegKeyADAMPrefix = "ADAM_";

	internal static readonly string RegKeyDMT = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Active Directory\\DMT";

	internal static readonly string RegKeySnapshotPrefix = "$SNAP_";

	internal static readonly string RegKeyParameters = "Parameters";

	internal static readonly string RegValueLdapPort = "Port LDAP";

	internal static readonly string RegValueLdapsPort = "Port SSL";

	internal static readonly string RegKeyAdwsService = "System\\CurrentControlSet\\Services\\ADWS";

	internal static readonly string RegKeyPerformance = "Performance";

	internal static readonly string RegValuePerfFirstCounter = "First Counter";

	internal static readonly string RegValuePerfCounterVersion = "PerformanceCounterVersion";

	internal static readonly int AdLdapPort = 389;

	internal static readonly int AdLdapsPort = 636;

	internal static readonly int AdGcPort = 3268;

	internal static readonly int AdGcsPort = 3269;

	internal static readonly int EventLogNoiseSuppressionInterval = 2;

	internal static readonly string AddsInstanceName = "NTDS";

	internal static readonly string AddsGcInstanceName = "GC";

	internal static readonly string LdapInstancePrefix = "ldap:";

	internal static readonly string AdNs = "http://schemas.microsoft.com/2008/1/ActiveDirectory";

	internal static readonly string AddataNs = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data";

	internal static readonly string AdCustomActionNs = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions";

	internal static readonly string AdFault = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault";

	internal static readonly string XsiNs = "http://www.w3.org/2001/XMLSchema-instance";

	internal static readonly string XsdNs = "http://www.w3.org/2001/XMLSchema";

	internal static readonly string IdentityManagementDirectoryAccess = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess";

	internal static readonly string WSTransfer = "http://schemas.xmlsoap.org/ws/2004/09/transfer";

	internal static readonly string WSEnumeration = "http://schemas.xmlsoap.org/ws/2004/09/enumeration";

	internal static readonly string XPathLvl1Selection = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/XPath-Level-1";

	internal static readonly string QNameSelection = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/QName";

	internal static readonly string XPath10SubsetQuery = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/XpathQuery";

	internal static readonly string LdapQuery = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/LdapQuery";

	internal static readonly string AdPrefix = "ad";

	internal static readonly string AddataPrefix = "addata";

	internal static readonly string XsiInstancePrefix = "xsi";

	internal static readonly string XsdInstancePrefix = "xsd";

	internal static readonly string IdentityManagementDirectoryAccessPrefix = "da";

	internal static readonly string TransferPrefix = "wst";

	internal static readonly string EnumerationPrefix = "wsen";

	internal static readonly string LdapQueryPrefix = "adlq";

	internal static readonly string XmlNs = "xmlns";

	internal static readonly string Control = "control";

	internal static readonly string Controls = "controls";

	internal static readonly string CtrlType = "type";

	internal static readonly string CtrlCriticality = "criticality";

	internal static readonly string CtrlValue = "controlValue";

	internal static readonly string Value = "Value";

	internal static readonly string InstanceType = "Type";

	internal static readonly string LdapSyntax = "LdapSyntax";

	internal static readonly string rangeLow = "RangeLow";

	internal static readonly string rangeHigh = "RangeHigh";

	internal static readonly string OperationDelete = "delete";

	internal static readonly string OperationAdd = "add";

	internal static readonly string OperationReplace = "replace";

	internal static readonly string objectReferenceProperty = "objectReferenceProperty";

	internal static readonly string containerHierarchyParent = "container-hierarchy-parent";

	internal static readonly string objectClass = "objectClass";

	internal static readonly string distinguishedName = "distinguishedName";

	internal static readonly string relativeDistinguishedName = "relativeDistinguishedName";

	internal static readonly string all = "all";

	internal static readonly string star = "*";

	internal static readonly Guid rootDSEGuid = new Guid("11111111-1111-1111-1111-111111111111");

	internal const uint DefaultPullElements = 1u;

	internal const int NTDSDSA_OPT_IS_GC = 1;

	internal const int pageSize = 1000;

	internal const string LdapShowDeletedControlOID = "1.2.840.113556.1.4.417";

	internal const string LdapPagedControlOID = "1.2.840.113556.1.4.319";

	internal const string LdapSortControlReqOID = "1.2.840.113556.1.4.473";

	internal const string LdapSortControlRespOID = "1.2.840.113556.1.4.474";

	internal const string LdapNotifSearchControlOID = "1.2.840.113556.1.4.528";

	internal const string LdapShutdownNotifyControlOID = "1.2.840.113556.1.4.1907";

	internal const string LdapCapabilityActiveDirectoryOID = "1.2.840.113556.1.4.800";

	internal const string LdapCapabilityAdamOID = "1.2.840.113556.1.4.1851";

	internal const string anonymousUserName = "NT AUTHORITY\\ANONYMOUS LOGON";

	internal const string anonymousUserStringSid = "S-1-5-7";

	internal const string quotasContainerBinary = "B:32:6227F0AF1FC2410D8E3BB10615BB5B0F:";

	internal const string microsoftContainerBinary = "B:32:F4BE92A4C777485E878E9421D53087DB:";

	internal const string programDataContainerBinary = "B:32:09460C08AE1E4A4EA0F64AEE7DAA1E5A:";

	internal const string foreignSecurityPrincipalsContainerBinary = "B:32:22B70C67D56E4EFB91E9300FCA3DC1AA:";

	internal const string deletedObjectsContainerBinary = "B:32:18E2EA80684F11D2B9AA00C04F79F805:";

	internal const string infraStructureContainerBinary = "B:32:2FBAC1870ADE11D297C400C04FD8D5CD:";

	internal const string lostAndFoundContainerBinary = "B:32:AB8153B7768811D1ADED00C04FD8D5CD:";

	internal const string systemsContainerBinary = "B:32:AB1D30F3768811D1ADED00C04FD8D5CD:";

	internal const string domainControllersContainerBinary = "B:32:A361B2FFFFD211D1AA4B00C04FD7D83A:";

	internal const string computersContainerBinary = "B:32:AA312825768811D1ADED00C04FD8D5CD:";

	internal const string usersContainerBinary = "B:32:A9D1CA15768811D1ADED00C04FD8D5CD:";

	internal const string EventlogSource = "ADWS";

	internal const string EventlogName = "Active Directory Web Services";

	internal const string EventlogResourceName = "adwsres.dll";

	internal const long EventLogMsgIdMask = 65535L;

	internal const string rangeQualifier = ";range=";

	internal const string minus = "-";

	internal static readonly Regex guidRegex = new Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

	internal static readonly Regex extendedDnRegex = new Regex("^<GUID=(?'guid'.{36})>;(<SID=(?'sid'.*)>;)?(?'dn'.*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

	internal static readonly Regex dnWithBinaryRegex = new Regex("^(?'prefix'B:[0-9]+:[0-9a-fA-F]*:)(?'dn'.*)$", RegexOptions.Compiled | RegexOptions.Singleline);

	internal static readonly Regex dnWithStringRegex = new Regex("^(?'prefix'S:[0-9]+:.*?:)(?'dn'.*)$", RegexOptions.Compiled | RegexOptions.Singleline);

	internal static readonly Regex dnAccessPointRegex = new Regex("^(?'prefix'.*?#X500:)(?'dn'.*)$", RegexOptions.Compiled | RegexOptions.Singleline);

	internal static readonly Regex predicateRegex = new Regex("^\\[\\s*(?'predicate'.+=.+)]$", RegexOptions.Compiled | RegexOptions.Singleline);

	internal static readonly Regex predicateCrackRegex = new Regex("(?'left'[^=]+)=(?'right'.+)", RegexOptions.Compiled | RegexOptions.Singleline);

	internal static readonly Regex gpLinkRegEx = new Regex("LDAP://(?'dn'[^;]+);.*$", RegexOptions.Compiled | RegexOptions.Singleline);

	internal const int LDAP_SUCCESS = 0;

	internal const int LDAP_OPERATIONS_ERROR = 1;

	internal const int LDAP_PROTOCOL_ERROR = 2;

	internal const int LDAP_TIMELIMIT_EXCEEDED = 3;

	internal const int LDAP_SIZELIMIT_EXCEEDED = 4;

	internal const int LDAP_COMPARE_FALSE = 5;

	internal const int LDAP_COMPARE_TRUE = 6;

	internal const int LDAP_AUTH_METHOD_NOT_SUPPORTED = 7;

	internal const int LDAP_STRONG_AUTH_REQUIRED = 8;

	internal const int LDAP_REFERRAL_V2 = 9;

	internal const int LDAP_PARTIAL_RESULTS = 9;

	internal const int LDAP_REFERRAL = 10;

	internal const int LDAP_ADMIN_LIMIT_EXCEEDED = 11;

	internal const int LDAP_UNAVAILABLE_CRIT_EXTENSION = 12;

	internal const int LDAP_CONFIDENTIALITY_REQUIRED = 13;

	internal const int LDAP_SASL_BIND_IN_PROGRESS = 14;

	internal const int LDAP_NO_SUCH_ATTRIBUTE = 16;

	internal const int LDAP_UNDEFINED_TYPE = 17;

	internal const int LDAP_INAPPROPRIATE_MATCHING = 18;

	internal const int LDAP_CONSTRAINT_VIOLATION = 19;

	internal const int LDAP_ATTRIBUTE_OR_VALUE_EXISTS = 20;

	internal const int LDAP_INVALID_SYNTAX = 21;

	internal const int LDAP_NO_SUCH_OBJECT = 32;

	internal const int LDAP_ALIAS_PROBLEM = 33;

	internal const int LDAP_INVALID_DN_SYNTAX = 34;

	internal const int LDAP_IS_LEAF = 35;

	internal const int LDAP_ALIAS_DEREF_PROBLEM = 36;

	internal const int LDAP_INAPPROPRIATE_AUTH = 48;

	internal const int LDAP_INVALID_CREDENTIALS = 49;

	internal const int LDAP_INSUFFICIENT_RIGHTS = 50;

	internal const int LDAP_BUSY = 51;

	internal const int LDAP_UNAVAILABLE = 52;

	internal const int LDAP_UNWILLING_TO_PERFORM = 53;

	internal const int LDAP_LOOP_DETECT = 54;

	internal const int LDAP_SORT_CONTROL_MISSING = 60;

	internal const int LDAP_OFFSET_RANGE_ERROR = 61;

	internal const int LDAP_NAMING_VIOLATION = 64;

	internal const int LDAP_OBJECT_CLASS_VIOLATION = 65;

	internal const int LDAP_NOT_ALLOWED_ON_NONLEAF = 66;

	internal const int LDAP_NOT_ALLOWED_ON_RDN = 67;

	internal const int LDAP_ALREADY_EXISTS = 68;

	internal const int LDAP_NO_OBJECT_CLASS_MODS = 69;

	internal const int LDAP_RESULTS_TOO_LARGE = 70;

	internal const int LDAP_AFFECTS_MULTIPLE_DSAS = 71;

	internal const int LDAP_VIRTUAL_LIST_VIEW_ERROR = 76;

	internal const int LDAP_OTHER = 80;

	internal const int LDAP_SERVER_DOWN = 81;

	internal const int LDAP_LOCAL_ERROR = 82;

	internal const int LDAP_ENCODING_ERROR = 83;

	internal const int LDAP_DECODING_ERROR = 84;

	internal const int LDAP_TIMEOUT = 85;

	internal const int LDAP_AUTH_UNKNOWN = 86;

	internal const int LDAP_FILTER_ERROR = 87;

	internal const int LDAP_USER_CANCELLED = 88;

	internal const int LDAP_PARAM_ERROR = 89;

	internal const int LDAP_NO_MEMORY = 90;

	internal const int LDAP_CONNECT_ERROR = 91;

	internal const int LDAP_NOT_SUPPORTED = 92;

	internal const int LDAP_NO_RESULTS_RETURNED = 94;

	internal const int LDAP_CONTROL_NOT_FOUND = 93;

	internal const int LDAP_MORE_RESULTS_TO_RETURN = 95;

	internal const int LDAP_CLIENT_LOOP = 96;

	internal const int LDAP_REFERRAL_LIMIT_EXCEEDED = 97;

	internal const int NO_ERROR = 0;

	internal const int ERROR_ACCESS_DENIED = 5;

	internal const int ERROR_NOT_ENOUGH_MEMORY = 8;

	internal const int ERROR_GEN_FAILURE = 31;

	internal const int ERROR_UNEXP_NET_ERR = 59;

	internal const int ERROR_INVALID_PARAMETER = 87;

	internal const int ERROR_MORE_DATA = 234;

	internal const int ERROR_PARTIAL_COPY = 299;

	internal const int ERROR_SERVICE_ALREADY_RUNNING = 1056;

	internal const int ERROR_CANCELLED = 1223;

	internal const int ERROR_CONNECTION_REFUSED = 1225;

	internal const int ERROR_NOT_ALL_ASSIGNED = 1300;

	internal const int ERROR_LOGON_FAILURE = 1326;

	internal const int ERROR_TIMEOUT = 1460;

	internal const int ERROR_OBJECT_ALREADY_EXISTS = 5010;

	internal const int ERROR_DS_NO_ATTRIBUTE_OR_VALUE = 8202;

	internal const int ERROR_DS_INVALID_ATTRIBUTE_SYNTAX = 8203;

	internal const int ERROR_DS_ATTRIBUTE_TYPE_UNDEFINED = 8204;

	internal const int ERROR_DS_ATTRIBUTE_OR_VALUE_EXISTS = 8205;

	internal const int ERROR_DS_BUSY = 8206;

	internal const int ERROR_DS_UNAVAILABLE = 8207;

	internal const int ERROR_DS_OBJ_CLASS_VIOLATION = 8212;

	internal const int ERROR_DS_CANT_ON_NON_LEAF = 8213;

	internal const int ERROR_DS_CANT_ON_RDN = 8214;

	internal const int ERROR_DS_CANT_MOD_OBJ_CLASS = 8215;

	internal const int ERROR_DS_OPERATIONS_ERROR = 8224;

	internal const int ERROR_DS_PROTOCOL_ERROR = 8225;

	internal const int ERROR_DS_TIMELIMIT_EXCEEDED = 8226;

	internal const int ERROR_DS_SIZELIMIT_EXCEEDED = 8227;

	internal const int ERROR_DS_ADMIN_LIMIT_EXCEEDED = 8228;

	internal const int ERROR_DS_COMPARE_FALSE = 8229;

	internal const int ERROR_DS_COMPARE_TRUE = 8230;

	internal const int ERROR_DS_AUTH_METHOD_NOT_SUPPORTED = 8231;

	internal const int ERROR_DS_STRONG_AUTH_REQUIRED = 8232;

	internal const int ERROR_DS_INAPPROPRIATE_AUTH = 8233;

	internal const int ERROR_DS_AUTH_UNKNOWN = 8234;

	internal const int ERROR_DS_REFERRAL = 8235;

	internal const int ERROR_DS_UNAVAILABLE_CRIT_EXTENSION = 8236;

	internal const int ERROR_DS_CONFIDENTIALITY_REQUIRED = 8237;

	internal const int ERROR_DS_INAPPROPRIATE_MATCHING = 8238;

	internal const int ERROR_DS_CONSTRAINT_VIOLATION = 8239;

	internal const int ERROR_DS_NO_SUCH_OBJECT = 8240;

	internal const int ERROR_DS_ALIAS_PROBLEM = 8241;

	internal const int ERROR_DS_INVALID_DN_SYNTAX = 8242;

	internal const int ERROR_DS_IS_LEAF = 8243;

	internal const int ERROR_DS_ALIAS_DEREF_PROBLEM = 8244;

	internal const int ERROR_DS_UNWILLING_TO_PERFORM = 8245;

	internal const int ERROR_DS_LOOP_DETECT = 8246;

	internal const int ERROR_DS_NAMING_VIOLATION = 8247;

	internal const int ERROR_DS_OBJECT_RESULTS_TOO_LARGE = 8248;

	internal const int ERROR_DS_AFFECTS_MULTIPLE_DSAS = 8249;

	internal const int ERROR_DS_SERVER_DOWN = 8250;

	internal const int ERROR_DS_LOCAL_ERROR = 8251;

	internal const int ERROR_DS_ENCODING_ERROR = 8252;

	internal const int ERROR_DS_DECODING_ERROR = 8253;

	internal const int ERROR_DS_FILTER_UNKNOWN = 8254;

	internal const int ERROR_DS_PARAM_ERROR = 8255;

	internal const int ERROR_DS_NOT_SUPPORTED = 8256;

	internal const int ERROR_DS_NO_RESULTS_RETURNED = 8257;

	internal const int ERROR_DS_CONTROL_NOT_FOUND = 8258;

	internal const int ERROR_DS_CLIENT_LOOP = 8259;

	internal const int ERROR_DS_REFERRAL_LIMIT_EXCEEDED = 8260;

	internal const int ERROR_DS_SORT_CONTROL_MISSING = 8261;

	internal const int ERROR_DS_OFFSET_RANGE_ERROR = 8262;

	internal const int ERROR_DS_GENERIC_ERROR = 8341;

	internal const int ERROR_DS_NAME_ERROR_RESOLVING = 8469;

	internal const int SEC_I_CONTINUE_NEEDED = 590610;

	internal static readonly Dictionary<int, int> LdapErrorToWin32Error = new Dictionary<int, int>
	{
		{ 0, 0 },
		{ 1, 8224 },
		{ 2, 8225 },
		{ 3, 8226 },
		{ 4, 8227 },
		{ 5, 8229 },
		{ 6, 8230 },
		{ 7, 8231 },
		{ 8, 8232 },
		{ 9, 299 },
		{ 10, 8235 },
		{ 11, 8228 },
		{ 12, 8236 },
		{ 13, 8237 },
		{ 14, 590610 },
		{ 16, 8202 },
		{ 17, 8204 },
		{ 18, 8238 },
		{ 19, 8239 },
		{ 20, 8205 },
		{ 21, 8203 },
		{ 32, 8240 },
		{ 33, 8241 },
		{ 34, 8242 },
		{ 35, 8243 },
		{ 36, 8244 },
		{ 48, 8233 },
		{ 49, 1326 },
		{ 50, 5 },
		{ 51, 8206 },
		{ 52, 8207 },
		{ 53, 8245 },
		{ 54, 8246 },
		{ 60, 8261 },
		{ 61, 8262 },
		{ 64, 8247 },
		{ 65, 8212 },
		{ 66, 8213 },
		{ 67, 8214 },
		{ 68, 5010 },
		{ 69, 8215 },
		{ 70, 8248 },
		{ 71, 8249 },
		{ 76, 8341 },
		{ 80, 31 },
		{ 81, 8250 },
		{ 82, 8251 },
		{ 83, 8252 },
		{ 84, 8253 },
		{ 85, 1460 },
		{ 86, 8234 },
		{ 87, 8254 },
		{ 88, 1223 },
		{ 89, 8255 },
		{ 90, 8 },
		{ 91, 1225 },
		{ 92, 8256 },
		{ 94, 8257 },
		{ 93, 8258 },
		{ 95, 234 },
		{ 96, 8259 },
		{ 97, 8260 }
	};

	internal static readonly TimeSpan EnumCacheTimerAlarmInterval = new TimeSpan(0, 0, 30);

	internal static readonly int scmWaitTime = 5000;

	internal static readonly TimeSpan MaxGroupOrMemberEntriesTimeout = new TimeSpan(0, 5, 10);
}

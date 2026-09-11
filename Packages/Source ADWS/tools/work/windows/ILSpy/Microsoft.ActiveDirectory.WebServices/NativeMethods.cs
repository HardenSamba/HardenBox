using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.ActiveDirectory.WebServices;

internal static class NativeMethods
{
	[Flags]
	public enum ServiceBitFlags : uint
	{
		DS_WS_FLAG = 0x2000u
	}

	[Flags]
	public enum DS_NAME_FLAGS : uint
	{
		DS_NAME_NO_FLAGS = 0u,
		DS_NAME_FLAG_SYNTACTICAL_ONLY = 1u,
		DS_NAME_FLAG_EVAL_AT_DC = 2u,
		DS_NAME_FLAG_GCVERIFY = 4u,
		DS_NAME_FLAG_TRUST_REFERRAL = 8u
	}

	public enum DS_NAME_FORMAT : uint
	{
		DS_UNKNOWN_NAME = 0u,
		DS_FQDN_1779_NAME = 1u,
		DS_NT4_ACCOUNT_NAME = 2u,
		DS_DISPLAY_NAME = 3u,
		DS_UNIQUE_ID_NAME = 6u,
		DS_CANONICAL_NAME = 7u,
		DS_USER_PRINCIPAL_NAME = 8u,
		DS_CANONICAL_NAME_EX = 9u,
		DS_SERVICE_PRINCIPAL_NAME = 10u,
		DS_SID_OR_SID_HISTORY_NAME = 11u,
		DS_DNS_DOMAIN_NAME = 12u
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public sealed class DS_NAME_RESULTW
	{
		public uint cItems;

		public IntPtr rItems;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public sealed class DS_NAME_RESULT_ITEMW
	{
		public uint status;

		public string pDomain;

		public string pName;
	}

	public enum DS_ROLE
	{
		DS_ROLE_SCHEMA_OWNER,
		DS_ROLE_DOMAIN_OWNER,
		DS_ROLE_PDC_OWNER,
		DS_ROLE_RID_OWNER,
		DS_ROLE_INFRASTRUCTURE_OWNER
	}

	public enum COMPUTER_NAME_FORMAT
	{
		ComputerNameNetBIOS,
		ComputerNameDnsHostname,
		ComputerNameDnsDomain,
		ComputerNameDnsFullyQualified,
		ComputerNamePhysicalNetBIOS,
		ComputerNamePhysicalDnsHostname,
		ComputerNamePhysicalDnsDomain,
		ComputerNamePhysicalDnsFullyQualified
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct TokenPrivileges
	{
		public uint privilegeCount;

		public LUID luid;

		public uint attribute;
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	internal struct LUID
	{
		public uint lowPart;

		public uint highPart;
	}

	[Flags]
	internal enum TOKEN_RIGHTS : uint
	{
		TOKEN_ASSIGN_PRIMARY = 1u,
		TOKEN_DUPLICATE = 2u,
		TOKEN_IMPERSONATE = 4u,
		TOKEN_QUERY = 8u,
		TOKEN_QUERY_SOURCE = 0x10u,
		TOKEN_ADJUST_PRIVILEGES = 0x20u,
		TOKEN_ADJUST_GROUPS = 0x40u,
		TOKEN_ADJUST_DEFAULT = 0x80u,
		TOKEN_ADJUST_SESSIONID = 0x100u
	}

	internal const uint SE_PRIVILEGE_REMOVED = 4u;

	internal const string SE_BACKUP_NAME = "SeBackupPrivilege";

	internal const string SE_RESTORE_NAME = "SeRestorePrivilege";

	internal const string SE_ASSIGNPRIMARYTOKEN_NAME = "SeAssignPrimaryTokenPrivilege";

	internal const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";

	internal const string SE_DEBUG_NAME = "SeDebugPrivilege";

	internal const string SE_TCB_NAME = "SeTcbPrivilege";

	internal const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

	[DllImport("netapi32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	public static extern uint NetLogonSetServiceBits(string ServerName, ServiceBitFlags ServiceBitsOfInterest, ServiceBitFlags ServiceBits);

	[DllImport("ntdsapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	internal static extern uint DsMakePasswordCredentialsW(string User, string Domain, string Password, out IntPtr pAuthIdentity);

	[DllImport("ntdsapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	internal static extern void DsFreePasswordCredentials(IntPtr AuthIdentity);

	[DllImport("ntdsapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	public static extern uint DsBindW(string DomainControllerName, string DnsDomainName, out IntPtr phDS);

	[DllImport("ntdsapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	internal static extern uint DsBindWithCredW(string DomainControllerName, string DnsDomainName, IntPtr AuthIdentity, out IntPtr phDS);

	[DllImport("ntdsapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	public static extern uint DsUnBindW(ref IntPtr phDS);

	[DllImport("ntdsapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	public static extern uint DsCrackNamesW(IntPtr hDS, DS_NAME_FLAGS flags, DS_NAME_FORMAT formatOffered, DS_NAME_FORMAT formatDesired, uint cNames, string[] rpNames, out IntPtr ppResult);

	[DllImport("ntdsapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	public static extern void DsFreeNameResultW(IntPtr pResult);

	[DllImport("ntdsapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
	public static extern uint DsListRolesW(IntPtr hDS, out IntPtr ppResult);

	[DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern bool GetComputerNameExW(COMPUTER_NAME_FORMAT NameType, StringBuilder lpBuffer, ref uint lpnSize);

	[DllImport("advapi32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
	internal static extern bool OpenProcessToken(IntPtr processHandle, TOKEN_RIGHTS desiredAccess, ref IntPtr tokenHandle);

	[DllImport("advapi32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, EntryPoint = "LookupPrivilegeValueW", SetLastError = true)]
	internal static extern bool LookupPrivilegeValue(string systemName, string name, ref LUID luid);

	[DllImport("advapi32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
	internal static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TokenPrivileges NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

	[DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
	internal static extern bool CloseHandle(IntPtr handle);
}

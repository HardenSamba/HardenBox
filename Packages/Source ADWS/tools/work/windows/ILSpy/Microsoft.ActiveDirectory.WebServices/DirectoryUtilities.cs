using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

namespace Microsoft.ActiveDirectory.WebServices;

internal class DirectoryUtilities
{
	internal static LdapConnection CreateLdapConnection(InstanceType instance)
	{
		return CreateLdapConnection(new LdapDirectoryIdentifier("localhost", instance.Port));
	}

	internal static LdapConnection CreateLdapConnection(LdapDirectoryIdentifier dirIdent)
	{
		NetworkCredential credential = null;
		AuthType authType = AuthType.Negotiate;
		LdapConnection ldapConnection = new LdapConnection(dirIdent, credential, authType);
		ldapConnection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
		ldapConnection.Timeout = ConfigurationSettings.MaxPullTimeout;
		ldapConnection.SessionOptions.Signing = true;
		ldapConnection.SessionOptions.Sealing = true;
		return ldapConnection;
	}

	internal static DirectoryContext CreateDirectoryContext(string hostname)
	{
		return new DirectoryContext(DirectoryContextType.DirectoryServer, hostname);
	}

	internal static DirectoryEntry CreateDirectoryEntry(string adsPath)
	{
		AuthenticationTypes authenticationType = AuthenticationTypes.Secure | AuthenticationTypes.Signing | AuthenticationTypes.Sealing;
		return new DirectoryEntry(adsPath, null, null, authenticationType);
	}

	internal static PrincipalContext CreatePrincipalContext(ContextType ctxType, string hostname, string containerDn)
	{
		return new PrincipalContext(ctxType, hostname, containerDn);
	}

	internal static DsBindHandle CreateDsRpcBinding(string hostname)
	{
		IntPtr phDS = IntPtr.Zero;
		IntPtr zero = IntPtr.Zero;
		DsBindHandle dsBindHandle = new DsBindHandle();
		uint num = 0u;
		try
		{
			num = NativeMethods.DsBindWithCredW(hostname, null, zero, out phDS);
			if (num != 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "CreateDsRpcBinding: failed to DsBind, err={0}", num);
				throw new Win32Exception((int)num);
			}
			dsBindHandle.hDS = phDS;
		}
		finally
		{
			if (num != 0)
			{
				dsBindHandle.Dispose();
			}
		}
		return dsBindHandle;
	}

	internal static DsBindHandle CreateDsRpcBindingByInstance(InstanceType instance)
	{
		DsBindHandle dsBindHandle = null;
		string text = "localhost";
		if (instance.DirectoryType == DirectoryType.ADLDS)
		{
			text = text + ":" + instance.Port;
		}
		try
		{
			return CreateDsRpcBinding(text);
		}
		catch (Win32Exception ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "CreateDsRpcBindingByInstance: can't DsBind, err={0}", ex.NativeErrorCode);
			throw;
		}
	}

	internal static void FreeDsRpcBinding(DsBindHandle handle)
	{
		if (handle.hDS != IntPtr.Zero)
		{
			IntPtr phDS = handle.hDS;
			NativeMethods.DsUnBindW(ref phDS);
			handle.hDS = IntPtr.Zero;
		}
		if (handle.hAuthIdentity != IntPtr.Zero)
		{
			NativeMethods.DsFreePasswordCredentials(handle.hAuthIdentity);
			handle.hAuthIdentity = IntPtr.Zero;
		}
	}

	internal static DirectoryType DetermineTypeOfServer(string hostname, TimeSpan timeAllowed)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "DetermineTypeOfServer: Got hostname {0}", hostname);
		string text = hostname;
		int num = AdwsConstants.AdLdapPort;
		int num2 = hostname.LastIndexOf(':');
		if (num2 != -1)
		{
			if (num2 + 1 == hostname.Length)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "DetermineTypeOfServer: hostname string '{0}' is missing port number", hostname);
				throw new ArgumentException(AdwsStrings.NotAValidHostWithPortString);
			}
			if (num2 == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "DetermineTypeOfServer: hostname string '{0}' is missing server", hostname);
				throw new ArgumentException(AdwsStrings.NotAValidHostWithPortString);
			}
			text = hostname.Substring(0, num2);
			string s = hostname.Substring(num2 + 1);
			try
			{
				num = int.Parse(s);
			}
			catch (FormatException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "DetermineTypeOfServer: hostname string '{0}' has non-numeric port", hostname);
				throw new ArgumentException(AdwsStrings.NotAValidHostWithPortString);
			}
			catch (OverflowException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "DetermineTypeOfServer: hostname string '{0}' has too big/small port", hostname);
				throw new ArgumentException(AdwsStrings.NotAValidHostWithPortString);
			}
			if (num <= 0 || num > 65535)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "DetermineTypeOfServer: hostname string '{0}' has out-of-range port {1}", hostname, num);
				throw new ArgumentException(AdwsStrings.NotAValidHostWithPortString);
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "DetermineTypeOfServer: Using server name {0}, port number {1}", text, num);
		if (num != AdwsConstants.AdLdapPort && num != AdwsConstants.AdLdapsPort && num != AdwsConstants.AdGcPort && num != AdwsConstants.AdGcsPort)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "DetermineTypeOfServer: Not a well-known port, must be AD LDS");
			return DirectoryType.ADLDS;
		}
		LdapConnection ldapConnection = null;
		try
		{
			ldapConnection = CreateLdapConnection(new LdapDirectoryIdentifier(text, num));
			SearchRequest request = new SearchRequest("", "(objectclass=*)", System.DirectoryServices.Protocols.SearchScope.Base, "supportedCapabilities");
			SearchResponse searchResponse = (SearchResponse)ldapConnection.SendRequest(request, timeAllowed);
			CheckForReferralsAndThrow(searchResponse, "DetermineTypeOfServer", "", new InstanceType(text, num, num));
			if (searchResponse.Entries.Count == 0 || searchResponse.ResultCode != ResultCode.Success)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "DetermineTypeOfServer: no search results for rootDse, count={0}, err={1}", searchResponse.Entries.Count, searchResponse.ResultCode);
				throw new InvalidOperationException(AdwsStrings.CouldNotDetermineTypeOfServer);
			}
			string[] multiValuedAttrVal = GetMultiValuedAttrVal<string>(searchResponse.Entries[0], "supportedCapabilities");
			if (multiValuedAttrVal == null || multiValuedAttrVal.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "DetermineTypeOfServer: couldn't get supportedCapabilities for server {0}, port {1}", text, num);
				throw new InvalidOperationException(AdwsStrings.CouldNotDetermineTypeOfServer);
			}
			string[] array = multiValuedAttrVal;
			foreach (string strA in array)
			{
				if (string.Compare(strA, "1.2.840.113556.1.4.800", ignoreCase: true, CultureInfo.InvariantCulture) == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "DetermineTypeOfServer: server is AD DS");
					return DirectoryType.NTDS;
				}
				if (string.Compare(strA, "1.2.840.113556.1.4.1851", ignoreCase: true, CultureInfo.InvariantCulture) == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "DetermineTypeOfServer: server is AD LDS");
					return DirectoryType.ADLDS;
				}
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "DetermineTypeOfServer: found neither AD DS nor AD LDS OID for server {0}, port {1}", text, num);
			throw new InvalidOperationException(AdwsStrings.CouldNotDetermineTypeOfServer);
		}
		finally
		{
			if (ldapConnection != null)
			{
				ldapConnection.Dispose();
				ldapConnection = null;
			}
		}
	}

	internal static T FindDirAttribute<T>(IList<T> attrs, string Name, string Namespace, bool RemoveIfFound, bool OneValueEnforced) where T : DirectoryAttribute
	{
		T val = null;
		bool flag = false;
		do
		{
			flag = false;
			for (int i = 0; i < attrs.Count; i++)
			{
				if (string.Compare(attrs[i].Name, Name, ignoreCase: true) == 0 && string.Compare(attrs[i].Xmlns, Namespace, ignoreCase: true) == 0)
				{
					if (val != null && (val.AttrValues.Count != 0 || attrs[i].AttrValues.Count != 0))
					{
						throw new ArgumentException(AdwsStrings.DuplicateAttributeWithValues);
					}
					val = attrs[i];
					if (OneValueEnforced && val.AttrValues.Count != 1)
					{
						throw new ArgumentException(AdwsStrings.MissingOrMultipleAttributeValues);
					}
					if (RemoveIfFound)
					{
						attrs.RemoveAt(i);
						flag = true;
						break;
					}
				}
			}
		}
		while (flag);
		return val;
	}

	internal static string GetRdnFromDn(string dn, out string trimmedDn)
	{
		trimmedDn = string.Empty;
		int i = 0;
		bool flag = false;
		for (; i < dn.Length; i++)
		{
			if (dn[i] == '\\')
			{
				if (i + 1 < dn.Length)
				{
					i++;
				}
			}
			else if (dn[i] == '"')
			{
				flag = !flag;
			}
			else if (!flag && dn[i] == ',')
			{
				break;
			}
		}
		if (i < dn.Length)
		{
			trimmedDn = dn.Substring(i + 1);
		}
		return dn.Substring(0, i);
	}

	internal static string MakeGuidDn(Guid objectGuid)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<GUID=");
		stringBuilder.Append(objectGuid.ToString());
		stringBuilder.Append(">");
		return stringBuilder.ToString();
	}

	internal static string MakeGuidDn(string objectGuidString)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("<GUID=");
		stringBuilder.Append(objectGuidString);
		stringBuilder.Append(">");
		return stringBuilder.ToString();
	}

	internal static TimeSpan GetTimeRemaining(DateTime startTime, TimeSpan totalTimeAllowed)
	{
		DateTime utcNow = DateTime.UtcNow;
		if (utcNow - startTime >= totalTimeAllowed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "GetTimeRemaining: no time remaining");
			return new TimeSpan(0L);
		}
		TimeSpan result = totalTimeAllowed - (utcNow - startTime);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "GetTimeRemaining: remaining time is {0}", result.ToString());
		return result;
	}

	internal static TimeSpan GetTimeRemainingAndThrowLdapException(DateTime startTime, TimeSpan totalTimeAllowed)
	{
		TimeSpan timeRemaining = GetTimeRemaining(startTime, totalTimeAllowed);
		if (timeRemaining.Ticks == 0L)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "GetTimeRemainingAndThrowLdapException: no time remaining, throwing exception");
			throw new LdapException(85, AdwsStrings.OperationTimeout);
		}
		return timeRemaining;
	}

	internal static void CheckForReferralsAndThrow(DirectoryResponse dirResponse, string caller, string dn, InstanceType instance)
	{
		if (dirResponse.ResultCode != ResultCode.Success)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "CheckForReferralsAndThrow ({0}): got back a failure or referral for {1} (instance {2}), result code is {3}", caller, dn, instance.InstanceName, dirResponse.ResultCode);
			throw new DirectoryOperationException(dirResponse);
		}
	}

	internal static T GetSingleValuedAttrVal<T>(SearchResultEntry srchEntry, string attrName) where T : class
	{
		System.DirectoryServices.Protocols.DirectoryAttribute directoryAttribute = srchEntry.Attributes[attrName];
		if (directoryAttribute != null)
		{
			object[] array = null;
			try
			{
				array = directoryAttribute.GetValues(typeof(T));
			}
			catch (NotSupportedException)
			{
				return null;
			}
			if (array != null && array.Length != 0)
			{
				return (T)array[0];
			}
		}
		return null;
	}

	internal static T[] GetMultiValuedAttrVal<T>(SearchResultEntry srchEntry, string attrName) where T : class
	{
		System.DirectoryServices.Protocols.DirectoryAttribute directoryAttribute = srchEntry.Attributes[attrName];
		if (directoryAttribute != null)
		{
			object[] array = null;
			try
			{
				array = directoryAttribute.GetValues(typeof(T));
			}
			catch (NotSupportedException)
			{
				return null;
			}
			return (T[])array;
		}
		return null;
	}

	internal static bool ContainsShowDeletedControl(IList<DirectoryControl> ldapControls)
	{
		if (ldapControls != null && ldapControls.Count > 0)
		{
			foreach (DirectoryControl ldapControl in ldapControls)
			{
				if (ldapControl.Type.Equals("1.2.840.113556.1.4.417"))
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "ContainsShowDeletedControl: found Show Deleted control");
					return true;
				}
			}
		}
		return false;
	}

	internal static T FindSpecifiedControl<T>(IList<DirectoryControl> ldapControls, string oid) where T : DirectoryControl
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "FindSpecifiedControl: looking for {0}", oid);
		if (ldapControls != null && ldapControls.Count > 0)
		{
			foreach (DirectoryControl ldapControl in ldapControls)
			{
				if (ldapControl.Type.Equals(oid))
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "FindSpecifiedControl: found matching control");
					return (T)ldapControl;
				}
			}
		}
		return null;
	}

	internal static ActiveDirectoryNameTranslateResult[] CrackNames(InstanceType instance, NativeMethods.DS_NAME_FLAGS flags, NativeMethods.DS_NAME_FORMAT formatOffered, NativeMethods.DS_NAME_FORMAT formatDesired, string[] names)
	{
		ActiveDirectoryNameTranslateResult[] array = new ActiveDirectoryNameTranslateResult[names.Length];
		ICustomActionConnCacheEntry<SidAndInstanceType, DsBindHandle> customActionConnCacheEntry = null;
		DsBindHandle dsBindHandle = null;
		try
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "CrackNames: {0} names to translate", names.Length);
			if (names.Length != 0)
			{
				try
				{
					customActionConnCacheEntry = CustomActionCaches.GetDsRpcBinding(instance);
				}
				catch (Win32Exception ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "CrackNames: can't GetDsRpcBinding, err={0}", ex.NativeErrorCode);
					throw;
				}
				dsBindHandle = customActionConnCacheEntry.ConnectionObject;
				IntPtr ppResult = IntPtr.Zero;
				try
				{
					uint num = NativeMethods.DsCrackNamesW(dsBindHandle.hDS, flags, formatOffered, formatDesired, (uint)names.Length, names, out ppResult);
					if (num != 0)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "CrackNames: can't DsCrackNames, err={0}", num);
						throw new Win32Exception((int)num);
					}
					NativeMethods.DS_NAME_RESULTW dS_NAME_RESULTW = (NativeMethods.DS_NAME_RESULTW)Marshal.PtrToStructure(ppResult, typeof(NativeMethods.DS_NAME_RESULTW));
					IntPtr ptr = dS_NAME_RESULTW.rItems;
					for (int i = 0; i < dS_NAME_RESULTW.cItems && i < array.Length; i++)
					{
						ActiveDirectoryNameTranslateResult activeDirectoryNameTranslateResult = new ActiveDirectoryNameTranslateResult();
						NativeMethods.DS_NAME_RESULT_ITEMW dS_NAME_RESULT_ITEMW = (NativeMethods.DS_NAME_RESULT_ITEMW)Marshal.PtrToStructure(ptr, typeof(NativeMethods.DS_NAME_RESULT_ITEMW));
						activeDirectoryNameTranslateResult.Result = dS_NAME_RESULT_ITEMW.status;
						activeDirectoryNameTranslateResult.Name = dS_NAME_RESULT_ITEMW.pName;
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "CrackNames: '{0}' --> '{1}' (result: {2})", names[i], (activeDirectoryNameTranslateResult.Name != null) ? activeDirectoryNameTranslateResult.Name : "NULL", activeDirectoryNameTranslateResult.Result.ToString());
						array[i] = activeDirectoryNameTranslateResult;
						ptr = (IntPtr)(ptr.ToInt64() + Marshal.SizeOf(typeof(NativeMethods.DS_NAME_RESULT_ITEMW)));
					}
				}
				finally
				{
					if (ppResult != IntPtr.Zero)
					{
						NativeMethods.DsFreeNameResultW(ppResult);
						ppResult = IntPtr.Zero;
					}
				}
			}
		}
		finally
		{
			if (customActionConnCacheEntry != null)
			{
				CustomActionCaches.ReleaseDsRpcBinding(customActionConnCacheEntry);
				customActionConnCacheEntry = null;
				dsBindHandle = null;
			}
		}
		return array;
	}

	internal static string GetDnsNameFromDN(InstanceType instance, string dn)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "GetDnsNameFromDN: translating {0}", (dn != null) ? dn : "NULL");
		string text = null;
		if (dn == null)
		{
			throw new ArgumentNullException("dn");
		}
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		ActiveDirectoryNameTranslateResult[] array = CrackNames(instance, NativeMethods.DS_NAME_FLAGS.DS_NAME_FLAG_SYNTACTICAL_ONLY, NativeMethods.DS_NAME_FORMAT.DS_FQDN_1779_NAME, NativeMethods.DS_NAME_FORMAT.DS_CANONICAL_NAME, new string[1] { dn });
		if (array == null || array.Length == 0 || array[0] == null || array[0].Result != 0 || array[0].Name == null || array[0].Name.Length == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryUtilities", "GetDnsNameFromDN: CrackNames didn't return value, status={0}", (array != null && array[0] != null) ? array[0].Result.ToString() : "NULL");
			throw new Win32Exception(8469);
		}
		text = array[0].Name;
		if (text.IndexOf('/') == text.Length - 1)
		{
			text = text.Substring(0, text.Length - 1);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryUtilities", "GetDnsNameFromDN: resultant DNS name is {0}", text);
		return text;
	}
}

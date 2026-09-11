using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace Microsoft.ActiveDirectory.WebServices;

internal static class PerfCounters
{
	private static AdwsPerfCounter createOpsRate = new AdwsPerfCounter(PerfCounterNames.CreateOpsRate, PerfCounterHelp.CreateOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter deleteOpsRate = new AdwsPerfCounter(PerfCounterNames.DeleteOpsRate, PerfCounterHelp.DeleteOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter getOpsRate = new AdwsPerfCounter(PerfCounterNames.GetOpsRate, PerfCounterHelp.GetOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter putOpsRate = new AdwsPerfCounter(PerfCounterNames.PutOpsRate, PerfCounterHelp.PutOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter enumOpsRate = new AdwsPerfCounter(PerfCounterNames.EnumOpsRate, PerfCounterHelp.EnumOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter pullOpsRate = new AdwsPerfCounter(PerfCounterNames.PullOpsRate, PerfCounterHelp.PullOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter openEnumCtxs = new AdwsPerfCounter(PerfCounterNames.OpenEnumCtxs, PerfCounterHelp.OpenEnumCtxs, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter getGroupMemberOpsRate = new AdwsPerfCounter(PerfCounterNames.GetGroupMemberOpsRate, PerfCounterHelp.GetGroupMemberOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter getPrincGroupMembershipOpsRate = new AdwsPerfCounter(PerfCounterNames.GetPrincGroupMembershipOpsRate, PerfCounterHelp.GetPrincGroupMembershipOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter setPassOpsRate = new AdwsPerfCounter(PerfCounterNames.SetPassOpsRate, PerfCounterHelp.SetPassOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter changePassOpsRate = new AdwsPerfCounter(PerfCounterNames.ChangePassOpsRate, PerfCounterHelp.ChangePassOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter getPrincAuthzGroupOpsRate = new AdwsPerfCounter(PerfCounterNames.GetPrincAuthzGroupOpsRate, PerfCounterHelp.GetPrincAuthzGroupOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter translateNameOpsRate = new AdwsPerfCounter(PerfCounterNames.TranslateNameOpsRate, PerfCounterHelp.TranslateNameOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter getDomCtrlOpsRate = new AdwsPerfCounter(PerfCounterNames.GetDomCtrlOpsRate, PerfCounterHelp.GetDomCtrlOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter getDomainOpsRate = new AdwsPerfCounter(PerfCounterNames.GetDomainOpsRate, PerfCounterHelp.GetDomainOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter moveRoleOpsRate = new AdwsPerfCounter(PerfCounterNames.MoveRoleOpsRate, PerfCounterHelp.MoveRoleOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter getForestOpsRate = new AdwsPerfCounter(PerfCounterNames.GetForestOpsRate, PerfCounterHelp.GetForestOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter changeOptionalFeatureOpsRate = new AdwsPerfCounter(PerfCounterNames.ChangeOptionalFeatureOpsRate, PerfCounterHelp.ChangeOptionalFeatureOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter getVersionOpsRate = new AdwsPerfCounter(PerfCounterNames.GetVersionOpsRate, PerfCounterHelp.GetVersionOpsRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter numberDirInstances = new AdwsPerfCounter(PerfCounterNames.NumberDirInstances, PerfCounterHelp.NumberDirInstances, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter possibleConns = new AdwsPerfCounter(PerfCounterNames.PossibleConns, PerfCounterHelp.PossibleConns, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter allocConns = new AdwsPerfCounter(PerfCounterNames.AllocConns, PerfCounterHelp.AllocConns, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter reservedConns = new AdwsPerfCounter(PerfCounterNames.ReservedConns, PerfCounterHelp.ReservedConns, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter nonReservedInUseConns = new AdwsPerfCounter(PerfCounterNames.NonReservedInUseConns, PerfCounterHelp.NonReservedInUseConns, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter reservedInUseConns = new AdwsPerfCounter(PerfCounterNames.ReservedInUseConns, PerfCounterHelp.ReservedInUseConns, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter openWebServiceSessions = new AdwsPerfCounter(PerfCounterNames.OpenWebServiceSessions, PerfCounterHelp.OpenWebServiceSessions, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter activeWebServiceSessions = new AdwsPerfCounter(PerfCounterNames.ActiveWebServiceSessions, PerfCounterHelp.ActiveWebServiceSessions, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter openWebServiceSessionsRate = new AdwsPerfCounter(PerfCounterNames.OpenWebServiceSessionsRate, PerfCounterHelp.OpenWebServiceSessionsRate, PerformanceCounterType.RateOfCountsPerSecond64);

	private static AdwsPerfCounter custActLdapPossibleConns = new AdwsPerfCounter(PerfCounterNames.CustActLdapPossibleConns, PerfCounterHelp.CustActLdapPossibleConns, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter custActLdapCreateRate = new AdwsPerfCounter(PerfCounterNames.CustActLdapCreateRate, PerfCounterHelp.CustActLdapCreateRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter custActLdapReuseRate = new AdwsPerfCounter(PerfCounterNames.CustActLdapReuseRate, PerfCounterHelp.CustActLdapReuseRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter custActDsRpcPossibleConns = new AdwsPerfCounter(PerfCounterNames.CustActDsRpcPossibleConns, PerfCounterHelp.CustActDsRpcPossibleConns, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter custActDsRpcCreateRate = new AdwsPerfCounter(PerfCounterNames.CustActDsRpcCreateRate, PerfCounterHelp.CustActDsRpcCreateRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter custActDsRpcReuseRate = new AdwsPerfCounter(PerfCounterNames.CustActDsRpcReuseRate, PerfCounterHelp.CustActDsRpcReuseRate, PerformanceCounterType.RateOfCountsPerSecond32);

	private static AdwsPerfCounter custActCombinedConnCount = new AdwsPerfCounter(PerfCounterNames.CustActCombinedConnCount, PerfCounterHelp.CustActCombinedConnCount, PerformanceCounterType.NumberOfItems32);

	private static AdwsPerfCounter[] allCounters = new AdwsPerfCounter[35]
	{
		createOpsRate, deleteOpsRate, getOpsRate, putOpsRate, enumOpsRate, pullOpsRate, openEnumCtxs, getGroupMemberOpsRate, getPrincGroupMembershipOpsRate, setPassOpsRate,
		changePassOpsRate, getPrincAuthzGroupOpsRate, translateNameOpsRate, getDomCtrlOpsRate, getDomainOpsRate, moveRoleOpsRate, getForestOpsRate, changeOptionalFeatureOpsRate, getVersionOpsRate, numberDirInstances,
		possibleConns, allocConns, reservedConns, nonReservedInUseConns, reservedInUseConns, openWebServiceSessions, activeWebServiceSessions, openWebServiceSessionsRate, custActLdapPossibleConns, custActLdapCreateRate,
		custActLdapReuseRate, custActDsRpcPossibleConns, custActDsRpcCreateRate, custActDsRpcReuseRate, custActCombinedConnCount
	};

	private static bool isInitialized = false;

	internal static AdwsPerfCounter CreateOpsRate => createOpsRate;

	internal static AdwsPerfCounter DeleteOpsRate => deleteOpsRate;

	internal static AdwsPerfCounter GetOpsRate => getOpsRate;

	internal static AdwsPerfCounter PutOpsRate => putOpsRate;

	internal static AdwsPerfCounter EnumOpsRate => enumOpsRate;

	internal static AdwsPerfCounter PullOpsRate => pullOpsRate;

	internal static AdwsPerfCounter OpenEnumCtxs => openEnumCtxs;

	internal static AdwsPerfCounter GetGroupMemberOpsRate => getGroupMemberOpsRate;

	internal static AdwsPerfCounter GetPrincGroupMembershipOpsRate => getPrincGroupMembershipOpsRate;

	internal static AdwsPerfCounter SetPassOpsRate => setPassOpsRate;

	internal static AdwsPerfCounter ChangePassOpsRate => changePassOpsRate;

	internal static AdwsPerfCounter GetPrincAuthzGroupOpsRate => getPrincAuthzGroupOpsRate;

	internal static AdwsPerfCounter TranslateNameOpsRate => translateNameOpsRate;

	internal static AdwsPerfCounter GetDomCtrlOpsRate => getDomCtrlOpsRate;

	internal static AdwsPerfCounter GetDomainOpsRate => getDomainOpsRate;

	internal static AdwsPerfCounter MoveRoleOpsRate => moveRoleOpsRate;

	internal static AdwsPerfCounter GetForestOpsRate => getForestOpsRate;

	internal static AdwsPerfCounter ChangeOptionalFeatureOpsRate => changeOptionalFeatureOpsRate;

	internal static AdwsPerfCounter GetVersionOpsRate => getVersionOpsRate;

	internal static AdwsPerfCounter NumberDirInstances => numberDirInstances;

	internal static AdwsPerfCounter PossibleConns => possibleConns;

	internal static AdwsPerfCounter AllocConns => allocConns;

	internal static AdwsPerfCounter ReservedConns => reservedConns;

	internal static AdwsPerfCounter NonReservedInUseConns => nonReservedInUseConns;

	internal static AdwsPerfCounter ReservedInUseConns => reservedInUseConns;

	internal static AdwsPerfCounter OpenWebServiceSessions => openWebServiceSessions;

	internal static AdwsPerfCounter ActiveWebServiceSessions => activeWebServiceSessions;

	internal static AdwsPerfCounter OpenWebServiceSessionsRate => openWebServiceSessionsRate;

	internal static AdwsPerfCounter CustActLdapPossibleConns => custActLdapPossibleConns;

	internal static AdwsPerfCounter CustActLdapCreateRate => custActLdapCreateRate;

	internal static AdwsPerfCounter CustActLdapReuseRate => custActLdapReuseRate;

	internal static AdwsPerfCounter CustActDsRpcPossibleConns => custActDsRpcPossibleConns;

	internal static AdwsPerfCounter CustActDsRpcCreateRate => custActDsRpcCreateRate;

	internal static AdwsPerfCounter CustActDsRpcReuseRate => custActDsRpcReuseRate;

	internal static AdwsPerfCounter CustActCombinedConnCount => custActCombinedConnCount;

	internal static void Initialize()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "Initialize: initializing performance counters");
		if (isInitialized)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "Initialize: trying to reinitialized perf counters");
			throw new InvalidOperationException(AdwsStrings.PerformanceCountersAlreadyInitialized);
		}
		AdwsPerfCounter[] array = allCounters;
		foreach (AdwsPerfCounter obj in array)
		{
			obj.Init();
			obj.Set(0);
		}
		isInitialized = true;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "Initialize: all performance counters initialized");
	}

	internal static void Uninitialize()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "Uninitialize: shutting down performance counters");
		AdwsPerfCounter[] array = allCounters;
		foreach (AdwsPerfCounter obj in array)
		{
			obj.Set(0);
			obj.Dispose();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "Uninitialize: all performance counters uninitialized");
	}

	internal static bool AreCountersInstalled()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "AreCountersInstalled: entered");
		bool flag = false;
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(AdwsConstants.RegKeyAdwsService, writable: false);
			if (registryKey != null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "AreCountersInstalled: {0} key is present", AdwsConstants.RegKeyAdwsService);
				using RegistryKey registryKey2 = registryKey.OpenSubKey(AdwsConstants.RegKeyPerformance, writable: false);
				if (registryKey2 != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "AreCountersInstalled: {0}\\{1} key is present", AdwsConstants.RegKeyAdwsService, AdwsConstants.RegKeyPerformance);
					if (registryKey2.GetValue(AdwsConstants.RegValuePerfFirstCounter) != null)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "AreCountersInstalled: {0} value is present", AdwsConstants.RegValuePerfFirstCounter);
						flag = true;
					}
				}
			}
		}
		catch (SecurityException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "AreCountersInstalled: couldn't open registry key or value (SecurityException): {0}", ex.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotCheckIfPerformanceCountersInstalled, ex);
		}
		catch (IOException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "AreCountersInstalled: couldn't open registry key or value (IOException): {0}", ex2.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotCheckIfPerformanceCountersInstalled, ex2);
		}
		catch (UnauthorizedAccessException ex3)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "AreCountersInstalled: couldn't open registry key or value (UnauthorizedAccessException): {0}", ex3.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotCheckIfPerformanceCountersInstalled, ex3);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "AreCountersInstalled: perf counters are {0} installed", flag ? "" : "NOT");
		return flag;
	}

	internal static bool AreCountersCurrent()
	{
		bool flag = false;
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(AdwsConstants.RegKeyAdwsService, writable: false);
			if (registryKey == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "AreCountersCurrent: missing ADWS service key: {0}", AdwsConstants.RegKeyAdwsService);
				throw new InvalidOperationException(AdwsStrings.CouldNotCheckPerformanceCounterVersionMissingServiceKey);
			}
			object value = registryKey.GetValue(AdwsConstants.RegValuePerfCounterVersion);
			if (value != null && value is int num)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "AreCountersCurrent: installed perf counter version: {0}", num);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "AreCountersCurrent: desired perf counter version: {0}", 6);
				if (num == 6)
				{
					flag = true;
				}
			}
		}
		catch (SecurityException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "AreCountersCurrent: couldn't open registry key or value (SecurityException): {0}", ex.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotCheckPerformanceCounterVersion, ex);
		}
		catch (IOException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "AreCountersCurrent: couldn't open registry key or value (IOException): {0}", ex2.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotCheckPerformanceCounterVersion, ex2);
		}
		catch (UnauthorizedAccessException ex3)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "AreCountersCurrent: couldn't open registry key or value (UnauthorizedAccessException): {0}", ex3.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotCheckPerformanceCounterVersion, ex3);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "AreCountersCurrent: perf counter category {0} is {1} current", PerfCounterNames.Category, flag ? "" : "NOT");
		return flag;
	}

	internal static void InstallCountersIfNeeded()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "InstallCountersIfNeeded: entered");
		if (!AreCountersInstalled())
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "InstallCountersIfNeeded: counters not installed, so installing");
			InstallCounters();
		}
		else if (!AreCountersCurrent())
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "InstallCountersIfNeeded: counters are wrong version, replacing");
			UninstallCounters();
			InstallCounters();
		}
		else
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "InstallCountersIfNeeded: counters already installed and current, no work needed");
		}
	}

	internal static void InstallCounters()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "InstallCounters: entered");
		try
		{
			CounterCreationDataCollection counterCreationDataCollection = new CounterCreationDataCollection();
			AdwsPerfCounter[] array = allCounters;
			foreach (AdwsPerfCounter adwsPerfCounter in array)
			{
				counterCreationDataCollection.Add(new CounterCreationData(adwsPerfCounter.CounterName, adwsPerfCounter.HelpText, adwsPerfCounter.CounterType));
			}
			PerformanceCounterCategory.Create(PerfCounterNames.Category, PerfCounterHelp.Category, PerformanceCounterCategoryType.SingleInstance, counterCreationDataCollection);
			MarkCountersCurrent();
		}
		catch (Win32Exception ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "InstallCounters: caught Win32Exception, err={0}, exception={1}", ex.NativeErrorCode.ToString(), ex.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotInstallPerformanceCounters, ex);
		}
		catch (UnauthorizedAccessException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "InstallCounters: couldn't install counters (UnauthorizedAccessException): {0}", ex2.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotInstallPerformanceCounters, ex2);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "InstallCounters: counters installed successfully");
	}

	internal static void UninstallCounters()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "UninstallCounters: entered");
		if (AreCountersInstalled())
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "UninstallCounters: uninstalling counters");
			try
			{
				PerformanceCounterCategory.Delete(PerfCounterNames.Category);
			}
			catch (Win32Exception ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "UninstallCounters: caught Win32Exception, err={0}, exception={1}", ex.NativeErrorCode.ToString(), ex.ToString());
				throw new InvalidOperationException(AdwsStrings.CouldNotUninstallPerformanceCounters, ex);
			}
			catch (UnauthorizedAccessException ex2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "UninstallCounters: couldn't uninstall counters (UnauthorizedAccessException): {0}", ex2.ToString());
				throw new InvalidOperationException(AdwsStrings.CouldNotUninstallPerformanceCounters, ex2);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "UninstallCounters: counters uninstalled successfully");
		}
	}

	private static void MarkCountersCurrent()
	{
		try
		{
			using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(AdwsConstants.RegKeyAdwsService, writable: true);
			if (registryKey != null)
			{
				registryKey.SetValue(AdwsConstants.RegValuePerfCounterVersion, 6, RegistryValueKind.DWord);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "PerfCounters", "MarkCountersCurrent: set perf counter version to {0}", 6);
				return;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "MarkCountersCurrent: missing ADWS service key: {0}", AdwsConstants.RegKeyAdwsService);
			throw new InvalidOperationException(AdwsStrings.CouldNotUpdatePerformanceCounterVersionMissingServiceKey);
		}
		catch (SecurityException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "MarkCountersCurrent: couldn't open registry key or write value (SecurityException): {0}", ex.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotUpdatePerformanceCounterVersion, ex);
		}
		catch (UnauthorizedAccessException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "PerfCounters", "MarkCountersCurrent: couldn't open registry key or write value (UnauthorizedAccessException): {0}", ex2.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotUpdatePerformanceCounterVersion, ex2);
		}
	}
}

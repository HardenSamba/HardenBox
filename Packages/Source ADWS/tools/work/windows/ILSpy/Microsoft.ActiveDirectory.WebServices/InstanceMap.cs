using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading;
using System.Timers;
using Microsoft.Win32;

namespace Microsoft.ActiveDirectory.WebServices;

internal class InstanceMap : IDisposable
{
	private bool disposed;

	private ReaderWriterLockSlim rwLock;

	private Dictionary<string, InstanceType> instanceTypeMap;

	private bool isDC;

	private bool isGC;

	private int syncPoint;

	private System.Timers.Timer timer;

	private string regKeyNtdsParameters = AdwsConstants.RegKeyNtdsParameters;

	private string regKeyServices = AdwsConstants.RegKeyServices;

	private string regKeyDMT = AdwsConstants.RegKeyDMT;

	private bool ntdsEncounteredErrorsOnPreviousRun;

	private bool gcEncounteredErrorsOnPreviousRun;

	private bool adamEncounteredErrorsOnPreviousRun;

	private bool snapshotEncounteredErrorsOnPreviousRun;

	private InstanceType ntdsInstanceType = new InstanceType(AdwsConstants.AddsInstanceName, AdwsConstants.AdLdapPort, AdwsConstants.AdLdapsPort, DirectoryType.NTDS);

	private InstanceType gcInstanceType = new InstanceType(AdwsConstants.AddsGcInstanceName, AdwsConstants.AdGcPort, AdwsConstants.AdGcsPort, DirectoryType.GC);

	private InstanceMap()
	{
		rwLock = new ReaderWriterLockSlim();
		instanceTypeMap = new Dictionary<string, InstanceType>();
		TimeSpan instanceRediscoveryInterval = ConfigurationSettings.InstanceRediscoveryInterval;
		GlobalDebug.WriteLineIf(GlobalDebug.Warn, "InstanceMap", "InstanceMap: using timer inverval {0}", instanceRediscoveryInterval);
		timer = new System.Timers.Timer(instanceRediscoveryInterval.TotalMilliseconds);
	}

	internal static InstanceMap CreateInstanceMap()
	{
		InstanceMap instanceMap = new InstanceMap();
		instanceMap.CheckAndLoadAll();
		instanceMap.timer.Elapsed += instanceMap.OnTimedEvent;
		instanceMap.timer.Enabled = true;
		return instanceMap;
	}

	internal InstanceType GetInstanceType(string identifier)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "GetInstanceType: entering, identifier={0}", (identifier != null) ? identifier : "NULL");
		CheckDisposed();
		if (identifier == null)
		{
			throw new ArgumentNullException("identifier");
		}
		if (identifier.Length == 0)
		{
			return null;
		}
		InstanceType instanceType = null;
		rwLock.EnterReadLock();
		try
		{
			if (instanceTypeMap.ContainsKey(identifier))
			{
				instanceType = instanceTypeMap[identifier];
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "GetInstanceType: mapped {0} to {1} ({2}/{3})", identifier, instanceType.InstanceName, instanceType.Port, instanceType.SslPort);
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "InstanceMap", "GetInstanceType: no mapping for {0}", identifier);
			}
		}
		finally
		{
			rwLock.ExitReadLock();
		}
		return instanceType;
	}

	public void Dispose()
	{
		if (!disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "Dispose: disposing");
			if (timer != null)
			{
				timer.Enabled = false;
			}
			while (Interlocked.CompareExchange(ref syncPoint, -1, 0) != 0)
			{
				Thread.Sleep(500);
			}
			if (timer != null)
			{
				timer.Dispose();
				timer = null;
			}
			if (rwLock != null)
			{
				rwLock.Dispose();
				rwLock = null;
			}
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "InstanceMap", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	private void OnTimedEvent(object source, ElapsedEventArgs e)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "OnTimedEvent: got an event");
		CheckAndLoadAll();
		TimeSpan instanceRediscoveryInterval = ConfigurationSettings.InstanceRediscoveryInterval;
		if (timer.Interval != instanceRediscoveryInterval.TotalMilliseconds)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "OnTimedEvent: changing the timer interval to {0}", instanceRediscoveryInterval.ToString());
			timer.Interval = instanceRediscoveryInterval.TotalMilliseconds;
		}
	}

	private void CheckAndLoadAll()
	{
		int num = Interlocked.CompareExchange(ref syncPoint, 1, 0);
		if (num == 0)
		{
			try
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadAll: beginning");
				CheckAndLoadNTDSInstance();
				CheckAndLoadGCInstance();
				CheckAndLoadADAMInstances();
				CheckAndLoadSnapshotInstances();
				PerfCounters.NumberDirInstances.Set(instanceTypeMap.Count);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadAll: complete");
				return;
			}
			catch (Exception ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckAndLoadAll: caught unexpected exception {0}", ex.ToString());
				throw;
			}
			finally
			{
				syncPoint = 0;
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadAll: skipped ({0})", num);
	}

	private void CheckAndLoadNTDSInstance()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadNTDSInstance: entered");
		string identifier = MakeIdentifierFromPort(AdwsConstants.AdLdapPort);
		RegistryKey localMachine = Registry.LocalMachine;
		bool flag;
		try
		{
			using RegistryKey registryKey = localMachine.OpenSubKey(regKeyNtdsParameters, writable: false);
			if (registryKey != null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadNTDSInstance: found NTDS Parameters key");
				flag = true;
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadNTDSInstance: did NOT find NTDS Parameters key");
				flag = false;
			}
		}
		catch (SecurityException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckAndLoadNTDSInstance: couldn't open registry key");
			ntdsEncounteredErrorsOnPreviousRun = true;
			ADWSEventlog.Log(3221226676L);
			return;
		}
		if (ntdsEncounteredErrorsOnPreviousRun)
		{
			ADWSEventlog.Log(1073743029L);
		}
		ntdsEncounteredErrorsOnPreviousRun = false;
		if (flag && !isDC)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadNTDSInstance: trying to change state to DC");
			AddRemoveSessionPoolAndDictionaryEntry(ref isDC, add: true, identifier, ntdsInstanceType);
		}
		else if (!flag && isDC)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadNTDSInstance: trying to change state to _not_ DC");
			AddRemoveSessionPoolAndDictionaryEntry(ref isDC, add: false, identifier, null);
		}
	}

	private void CheckAndLoadSnapshotInstances()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadSnapshotInstances: entered");
		List<InstanceType> discoveredInstances = new List<InstanceType>();
		bool instanceEncounteredErrorsOnThisRun = false;
		try
		{
			DiscoverInstancesFromRegistry(regKeyDMT, AdwsConstants.RegKeySnapshotPrefix, ref instanceEncounteredErrorsOnThisRun, discoveredInstances, DirectoryType.Snapshot);
		}
		catch (SecurityException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckAndLoadSnapshotInstances: couldn't open registry key (SecurityException): {0}", ex.ToString());
			instanceEncounteredErrorsOnThisRun = true;
			snapshotEncounteredErrorsOnPreviousRun = true;
			ADWSEventlog.Log(3221226683L);
			return;
		}
		catch (UnauthorizedAccessException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckAndLoadSnapshotInstances: couldn't open registry key (UnauthorizedAccessException): {0}", ex2.ToString());
			instanceEncounteredErrorsOnThisRun = true;
			snapshotEncounteredErrorsOnPreviousRun = true;
			ADWSEventlog.Log(3221226683L);
			return;
		}
		if (snapshotEncounteredErrorsOnPreviousRun && !instanceEncounteredErrorsOnThisRun)
		{
			ADWSEventlog.Log(1073743037L);
		}
		snapshotEncounteredErrorsOnPreviousRun = instanceEncounteredErrorsOnThisRun;
		RefreshDiscoveredInstancesAndInstanceTypeMap(discoveredInstances, AdwsConstants.RegKeySnapshotPrefix);
	}

	private void CheckAndLoadGCInstance()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadGCInstance: entered");
		bool flag = false;
		string identifier = MakeIdentifierFromPort(AdwsConstants.AdGcPort);
		if (!isDC)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadGCInstance: machine isn't a DC, so it isn't a GC");
			flag = false;
		}
		else
		{
			if (!CheckForGlobalCatalog(out flag))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckAndLoadGCInstance: CheckForGlobalCatalog could not determine if GC");
				if (!gcEncounteredErrorsOnPreviousRun)
				{
					ADWSEventlog.Log(3221226678L);
				}
				gcEncounteredErrorsOnPreviousRun = true;
				return;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadGCInstance: CheckForGlobalCatalog={0}", flag.ToString());
		}
		if (gcEncounteredErrorsOnPreviousRun)
		{
			ADWSEventlog.Log(1073743031L);
		}
		gcEncounteredErrorsOnPreviousRun = false;
		if (flag && !isGC)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadGCInstance: trying to change state to Global Catalog");
			AddRemoveSessionPoolAndDictionaryEntry(ref isGC, add: true, identifier, gcInstanceType);
		}
		else if (!flag && isGC)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadGCInstance: trying to change state to _not_ Global Catalog");
			AddRemoveSessionPoolAndDictionaryEntry(ref isGC, add: false, identifier, null);
		}
	}

	private void CheckAndLoadADAMInstances()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckAndLoadADAMInstances: entered");
		List<InstanceType> discoveredInstances = new List<InstanceType>();
		bool instanceEncounteredErrorsOnThisRun = false;
		try
		{
			DiscoverInstancesFromRegistry(regKeyServices, AdwsConstants.RegKeyADAMPrefix, ref instanceEncounteredErrorsOnThisRun, discoveredInstances, DirectoryType.ADLDS);
		}
		catch (SecurityException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckAndLoadADAMInstances: couldn't open registry key (SecurityException): {0}", ex.ToString());
			instanceEncounteredErrorsOnThisRun = true;
			adamEncounteredErrorsOnPreviousRun = true;
			ADWSEventlog.Log(3221226680L);
			return;
		}
		catch (UnauthorizedAccessException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckAndLoadADAMInstances: couldn't open registry key (UnauthorizedAccessException): {0}", ex2.ToString());
			instanceEncounteredErrorsOnThisRun = true;
			adamEncounteredErrorsOnPreviousRun = true;
			ADWSEventlog.Log(3221226680L);
			return;
		}
		if (adamEncounteredErrorsOnPreviousRun && !instanceEncounteredErrorsOnThisRun)
		{
			ADWSEventlog.Log(1073743034L);
		}
		adamEncounteredErrorsOnPreviousRun = instanceEncounteredErrorsOnThisRun;
		RefreshDiscoveredInstancesAndInstanceTypeMap(discoveredInstances, AdwsConstants.RegKeyADAMPrefix);
	}

	private bool CheckForGlobalCatalog(out bool isGC)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckForGlobalCatalog: entered");
		isGC = false;
		DateTime utcNow = DateTime.UtcNow;
		TimeSpan operationTimeout = ConfigurationSettings.OperationTimeout;
		LdapConnection ldapConnection = null;
		try
		{
			ldapConnection = DirectoryUtilities.CreateLdapConnection(ntdsInstanceType);
			string[] array = new string[2] { "dsServiceName", "isGlobalCatalogReady" };
			SearchRequest request = new SearchRequest("", "(objectclass=*)", SearchScope.Base, array);
			SearchResponse searchResponse = (SearchResponse)ldapConnection.SendRequest(request, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
			if (searchResponse.Entries.Count == 0 || searchResponse.ResultCode != ResultCode.Success)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckForGlobalCatalog: no search results for rootDse, count={0}, err={1}", searchResponse.Entries.Count, searchResponse.ResultCode);
				return false;
			}
			string singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResponse.Entries[0], array[1]);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckForGlobalCatalog: isGlobalCatalogReady: {0}", singleValuedAttrVal);
			if (string.Compare("True", singleValuedAttrVal, ignoreCase: true, CultureInfo.InvariantCulture) != 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "GlobalCatalog is not ready to service the requests.", singleValuedAttrVal);
				isGC = false;
				return true;
			}
			string singleValuedAttrVal2 = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResponse.Entries[0], array[0]);
			if (singleValuedAttrVal2 == null || singleValuedAttrVal2.Length == 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckForGlobalCatalog: could not get 'dsServiceName'");
				return false;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckForGlobalCatalog: NTDS Settings DN: {0}", singleValuedAttrVal2);
			string text = "options";
			SearchRequest request2 = new SearchRequest(singleValuedAttrVal2, "(objectclass=*)", SearchScope.Base, text);
			SearchResponse searchResponse2 = (SearchResponse)ldapConnection.SendRequest(request2, DirectoryUtilities.GetTimeRemainingAndThrowLdapException(utcNow, operationTimeout));
			if (searchResponse2.Entries.Count == 0 || searchResponse2.ResultCode != ResultCode.Success)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckForGlobalCatalog: no search results for NTDS Settings, count={0}, err={1}", searchResponse2.Entries.Count, searchResponse2.ResultCode);
				return false;
			}
			string singleValuedAttrVal3 = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResponse2.Entries[0], text);
			if (singleValuedAttrVal3 != null && int.TryParse(singleValuedAttrVal3, out var result))
			{
				isGC = (result & 1) == 1;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "CheckForGlobalCatalog: options: {0}", result.ToString());
				return true;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckForGlobalCatalog: could not get 'options': {0}", (singleValuedAttrVal3 != null) ? singleValuedAttrVal3 : "null");
			return false;
		}
		catch (DirectoryException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "CheckForGlobalCatalog: DirectoryException exception: {0}", ex.ToString());
			return false;
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

	private void RefreshDiscoveredInstancesAndInstanceTypeMap(List<InstanceType> discoveredInstances, string regKeyInstancePrefix)
	{
		if (discoveredInstances == null)
		{
			throw new ArgumentNullException("discoveredInstances");
		}
		if (regKeyInstancePrefix == null)
		{
			throw new ArgumentNullException("regKeyInstancePrefix");
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, InstanceType> item in instanceTypeMap)
		{
			if (item.Value.InstanceName.StartsWith(regKeyInstancePrefix, ignoreCase: false, CultureInfo.InvariantCulture))
			{
				if (discoveredInstances.Contains(item.Value))
				{
					discoveredInstances.Remove(item.Value);
					continue;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "refreshDiscoveredInstancesAndInstanceTypeMap: recording instance '{0}' for removal", item.Value.InstanceName);
				list.Add(item.Key);
			}
		}
		foreach (string item2 in list)
		{
			bool currentState = true;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "refreshDiscoveredInstancesAndInstanceTypeMap: removing identifier '{0}'", item2);
			AddRemoveSessionPoolAndDictionaryEntry(ref currentState, add: false, item2, null);
		}
		foreach (InstanceType discoveredInstance in discoveredInstances)
		{
			bool currentState2 = false;
			string text = MakeIdentifierFromPort(discoveredInstance.Port);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "refreshDiscoveredInstancesAndInstanceTypeMap: adding identifier '{0}' (name='{1}')", text, discoveredInstance.InstanceName);
			AddRemoveSessionPoolAndDictionaryEntry(ref currentState2, add: true, text, discoveredInstance);
		}
	}

	private void DiscoverInstancesFromRegistry(string regRootKey, string regKeyInstancePrefix, ref bool instanceEncounteredErrorsOnThisRun, List<InstanceType> discoveredInstances, DirectoryType directoryType)
	{
		if (regRootKey == null)
		{
			throw new ArgumentNullException("regRootKey");
		}
		if (regKeyInstancePrefix == null)
		{
			throw new ArgumentNullException("regKeyInstancePrefix");
		}
		if (discoveredInstances == null)
		{
			throw new ArgumentNullException("discoveredInstances");
		}
		using RegistryKey registryKey = Registry.LocalMachine.OpenSubKey(regRootKey, writable: false);
		if (registryKey == null)
		{
			return;
		}
		string[] subKeyNames = registryKey.GetSubKeyNames();
		foreach (string text in subKeyNames)
		{
			if (!text.StartsWith(regKeyInstancePrefix, ignoreCase: false, CultureInfo.InvariantCulture))
			{
				continue;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "DiscoverInstancesFromRegistry: found '{0}'", text);
			using RegistryKey registryKey2 = registryKey.OpenSubKey(text);
			if (registryKey2 == null)
			{
				continue;
			}
			using RegistryKey registryKey3 = registryKey2.OpenSubKey(AdwsConstants.RegKeyParameters);
			if (registryKey3 == null)
			{
				continue;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "DiscoverInstancesFromRegistry: found '{0}'\\Parameters", text);
			string text2 = text;
			int num = 0;
			int num2 = 0;
			object value;
			object value2;
			try
			{
				value = registryKey3.GetValue(AdwsConstants.RegValueLdapPort);
				value2 = registryKey3.GetValue(AdwsConstants.RegValueLdapsPort);
			}
			catch (IOException)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "InstanceMap", "DiscoverInstancesFromRegistry: got IOException reading values for '{0}'", text);
				if (directoryType == DirectoryType.ADLDS)
				{
					ADWSEventlog.Log(2147484857L, text2);
				}
				else
				{
					ADWSEventlog.Log(2147484860L, text2);
				}
				instanceEncounteredErrorsOnThisRun = true;
				goto end_IL_009e;
			}
			if (value != null && value2 != null)
			{
				try
				{
					num = (int)value;
					num2 = (int)value2;
				}
				catch (InvalidCastException)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "InstanceMap", "DiscoverInstancesFromRegistry: got bad values for '{0}': '{1}' and '{2}'", text, value.ToString(), value2.ToString());
					if (directoryType == DirectoryType.ADLDS)
					{
						ADWSEventlog.Log(2147484857L, text2);
					}
					else
					{
						ADWSEventlog.Log(2147484860L, text2);
					}
					instanceEncounteredErrorsOnThisRun = true;
					goto end_IL_009e;
				}
				if (num <= 0 || num > 65535 || num2 <= 0 || num2 > 65535)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "InstanceMap", "DiscoverInstancesFromRegistry: not a valid TCP port number: instance={0}, LDAP={1}, LDAPS={2}", text, num, num2);
					if (directoryType == DirectoryType.ADLDS)
					{
						ADWSEventlog.Log(2147484857L, text2);
					}
					else
					{
						ADWSEventlog.Log(2147484860L, text2);
					}
					instanceEncounteredErrorsOnThisRun = true;
				}
				else
				{
					discoveredInstances.Add(new InstanceType(text2, num, num2, directoryType));
				}
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "InstanceMap", "DiscoverInstancesFromRegistry: didn't get a port value for '{0}'", text2);
				if (directoryType == DirectoryType.ADLDS)
				{
					ADWSEventlog.Log(2147484857L, text2);
				}
				else
				{
					ADWSEventlog.Log(2147484860L, text2);
				}
				instanceEncounteredErrorsOnThisRun = true;
			}
			end_IL_009e:;
		}
	}

	private void AddRemoveSessionPoolAndDictionaryEntry(ref bool currentState, bool add, string identifier, InstanceType instanceType)
	{
		InstanceType removedInstanceType;
		if (add && !currentState)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "AddRemoveSessionPoolAndDictionaryEntry: trying to change state for identifier {0}", identifier);
			if (!AddSessionPool(instanceType))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "AddRemoveSessionPoolAndDictionaryEntry: couldn't add session pool for identifier {0}", identifier);
				return;
			}
			AddInstanceTypeToMapIfNotPresent(identifier, instanceType.InstanceName, instanceType.Port, instanceType.SslPort, instanceType.DirectoryType, out removedInstanceType);
			currentState = true;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "AddRemoveSessionPoolAndDictionaryEntry: state change successful (now hosts identifier {0})", identifier);
		}
		else if (!add & currentState)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "AddRemoveSessionPoolAndDictionaryEntry: trying to change state to _not_ identifier {0}", identifier);
			RemoveInstanceTypeFromMapIfPresent(identifier, out removedInstanceType);
			RemoveSessionPool(removedInstanceType);
			currentState = false;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "AddRemoveSessionPoolAndDictionaryEntry: state change successful (does not host identifier {0})", identifier);
		}
	}

	private bool AddSessionPool(InstanceType instanceTypeToAdd)
	{
		bool flag = true;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "AddSessionPool: adding a session pool for {0}", instanceTypeToAdd.InstanceName);
		try
		{
			ClassManager.DirectoryDataAccess.InitializeInstance(instanceTypeToAdd, ConfigurationSettings.InitialPoolConnections, ConfigurationSettings.MaxPoolConnections);
		}
		catch (InvalidOperationException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "AddSessionPool: pool ({0}) already exists", instanceTypeToAdd.InstanceName);
			flag = false;
		}
		catch (DirectoryException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "InstanceMap", "AddSessionPool: DirectoryException trying to create pool: {0}", ex2.ToString());
			flag = false;
		}
		if (flag)
		{
			ADWSEventlog.Log(1073743024L, instanceTypeToAdd.InstanceName, instanceTypeToAdd.Port, instanceTypeToAdd.SslPort);
		}
		else
		{
			ADWSEventlog.Log(3221226674L, instanceTypeToAdd.InstanceName, instanceTypeToAdd.Port, instanceTypeToAdd.SslPort);
		}
		return flag;
	}

	private void RemoveSessionPool(InstanceType instanceTypeToRemove)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "InstanceMap", "RemoveSessionPool: removing session pool for {0}", instanceTypeToRemove.InstanceName);
		ClassManager.DirectoryDataAccess.RemoveInstance(instanceTypeToRemove);
		ADWSEventlog.Log(1073743025L, instanceTypeToRemove.InstanceName, instanceTypeToRemove.Port, instanceTypeToRemove.SslPort);
	}

	private bool AddInstanceTypeToMapIfNotPresent(string identifier, string instanceName, int port, int sslPort, DirectoryType dirType, out InstanceType addedInstanceType)
	{
		rwLock.EnterWriteLock();
		try
		{
			addedInstanceType = null;
			bool result = false;
			if (!instanceTypeMap.ContainsKey(identifier))
			{
				InstanceType instanceType = new InstanceType(instanceName, port, sslPort, dirType);
				instanceTypeMap[identifier] = instanceType;
				result = true;
				addedInstanceType = instanceType;
			}
			return result;
		}
		finally
		{
			rwLock.ExitWriteLock();
		}
	}

	private bool RemoveInstanceTypeFromMapIfPresent(string identifier, out InstanceType removedInstanceType)
	{
		rwLock.EnterWriteLock();
		try
		{
			removedInstanceType = null;
			bool result = false;
			if (instanceTypeMap.ContainsKey(identifier))
			{
				removedInstanceType = instanceTypeMap[identifier];
				instanceTypeMap.Remove(identifier);
				result = true;
			}
			return result;
		}
		finally
		{
			rwLock.ExitWriteLock();
		}
	}

	private string MakeIdentifierFromPort(int port)
	{
		return AdwsConstants.LdapInstancePrefix + port;
	}
}

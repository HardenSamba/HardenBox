using System;
using System.DirectoryServices.Protocols;
using System.Security.Principal;
using System.Threading;

namespace Microsoft.ActiveDirectory.WebServices;

internal class CustomActionCaches
{
	private delegate C CreationDelegate<C>(InstanceType instance);

	private delegate void CreationPerfCounterDelegate();

	private delegate void ReusePerfCounterDelegate();

	private static CustomActionConnCache<SidAndInstanceType, LdapConnection> ldapConnCache;

	private static CustomActionConnCache<SidAndInstanceType, DsBindHandle> dsBindHandleCache;

	private static long creationCollision = 0L;

	private static DateTime initializationTime = DateTime.Now;

	internal static void StartCaches()
	{
		uint customActionConnectionCount = ConfigurationSettings.CustomActionConnectionCount;
		TimeSpan customActionIdleConnectionTimeout = ConfigurationSettings.CustomActionIdleConnectionTimeout;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "StartCaches: creating Custom Action connection caches");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "StartCaches: count={0}, idle timeout={1}", customActionConnectionCount, customActionIdleConnectionTimeout);
		if (ldapConnCache == null)
		{
			ldapConnCache = new CustomActionConnCache<SidAndInstanceType, LdapConnection>(customActionConnectionCount, customActionIdleConnectionTimeout);
			dsBindHandleCache = new CustomActionConnCache<SidAndInstanceType, DsBindHandle>(customActionConnectionCount, customActionIdleConnectionTimeout);
			PerfCounters.CustActLdapPossibleConns.Adjust(customActionConnectionCount);
			PerfCounters.CustActDsRpcPossibleConns.Adjust(customActionConnectionCount);
		}
	}

	internal static void StopCaches()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "StopCaches: disposing Custom Action connection caches");
		if (ldapConnCache != null)
		{
			PerfCounters.CustActLdapPossibleConns.Set(0);
			PerfCounters.CustActDsRpcPossibleConns.Set(0);
			ldapConnCache.Dispose();
			ldapConnCache = null;
			dsBindHandleCache.Dispose();
			dsBindHandleCache = null;
		}
	}

	internal static ICustomActionConnCacheEntry<SidAndInstanceType, LdapConnection> GetLdapConnection(InstanceType instance)
	{
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		return GetConnection(instance, ldapConnCache, DirectoryUtilities.CreateLdapConnection, PerfCounters.CustActLdapCreateRate.Increment, PerfCounters.CustActLdapReuseRate.Increment);
	}

	internal static void ReleaseLdapConnection(ICustomActionConnCacheEntry<SidAndInstanceType, LdapConnection> connectionEntry)
	{
		if (connectionEntry == null)
		{
			throw new ArgumentNullException("connectionEntry");
		}
		ReleaseConnection(connectionEntry, ldapConnCache);
	}

	internal static ICustomActionConnCacheEntry<SidAndInstanceType, DsBindHandle> GetDsRpcBinding(InstanceType instance)
	{
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		return GetConnection(instance, dsBindHandleCache, DirectoryUtilities.CreateDsRpcBindingByInstance, PerfCounters.CustActDsRpcCreateRate.Increment, PerfCounters.CustActDsRpcReuseRate.Increment);
	}

	internal static void ReleaseDsRpcBinding(ICustomActionConnCacheEntry<SidAndInstanceType, DsBindHandle> handleEntry)
	{
		if (handleEntry == null)
		{
			throw new ArgumentNullException("handleEntry");
		}
		ReleaseConnection(handleEntry, dsBindHandleCache);
	}

	private static ICustomActionConnCacheEntry<SidAndInstanceType, C> GetConnection<C>(InstanceType instance, CustomActionConnCache<SidAndInstanceType, C> cache, CreationDelegate<C> creationMethod, CreationPerfCounterDelegate creationPerfCounter, ReusePerfCounterDelegate reusePerfCounter) where C : IDisposable
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "GetConnection: Entering, instance={0}, conn type={1}", instance.InstanceName, typeof(C).ToString());
		using WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
		SidAndInstanceType connKey = new SidAndInstanceType(windowsIdentity.User, instance);
		ICustomActionConnCacheEntry<SidAndInstanceType, C> newlyAddedCacheEntry;
		while (true)
		{
			newlyAddedCacheEntry = cache.Acquire(connKey);
			if (newlyAddedCacheEntry != null)
			{
				break;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "GetConnection: no entry in cache, creating new entry");
			C connObject = creationMethod(instance);
			creationPerfCounter();
			switch (cache.AddIfNotPresent(connKey, connObject, out newlyAddedCacheEntry))
			{
			case CustomActionConnCache<SidAndInstanceType, C>.AddResult.AlreadyPresent:
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "GetConnection: couldn't add new entry, already present, retrying");
				if (Interlocked.Increment(ref creationCollision) % 50 == 0L)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "CustomActionCaches", "GetConnection: **** There have been {0} already-present collisions (in {1}) ****", creationCollision, DateTime.Now - initializationTime);
				}
				break;
			case CustomActionConnCache<SidAndInstanceType, C>.AddResult.NoRoomInCache:
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "GetConnection: couldn't add entry, no room, using fake entry");
				return cache.CreateFakeEntry(connKey, connObject);
			default:
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "GetConnection: added new entry to cache");
				return newlyAddedCacheEntry;
			}
			connObject.Dispose();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "GetConnection: got entry from cache");
		reusePerfCounter();
		return newlyAddedCacheEntry;
	}

	private static void ReleaseConnection<C>(ICustomActionConnCacheEntry<SidAndInstanceType, C> connectionEntry, CustomActionConnCache<SidAndInstanceType, C> cache)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "ReleaseConnection: Entering, key={0}, conn type={1}", connectionEntry.ConnectionKey, typeof(C).ToString());
		if (connectionEntry.IsFakeEntry)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionCaches", "ReleaseConnection: got fake entry, disposing");
			connectionEntry.Dispose();
		}
		else
		{
			cache.Release(connectionEntry);
		}
	}
}

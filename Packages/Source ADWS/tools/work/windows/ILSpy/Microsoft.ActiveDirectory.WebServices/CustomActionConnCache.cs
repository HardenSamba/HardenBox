using System;
using System.Collections.Generic;

namespace Microsoft.ActiveDirectory.WebServices;

internal class CustomActionConnCache<K, C> : IDisposable
{
	internal enum AddResult
	{
		SuccessfullyAdded,
		AlreadyPresent,
		NoRoomInCache
	}

	private bool disposed;

	private Dictionary<K, CustomActionConnCacheEntry<K, C>> cache = new Dictionary<K, CustomActionConnCacheEntry<K, C>>();

	private uint maxCacheSize;

	private TimeSpan maxIdleEntryLifetime;

	private TimeSpan maintenanceInterval;

	private DateTime lastCacheMaintenancePass = DateTime.Now;

	internal CustomActionConnCache(uint maxNumberOfConnectionsToCache, TimeSpan idleEntryLifetime)
	{
		maxCacheSize = maxNumberOfConnectionsToCache;
		maxIdleEntryLifetime = idleEntryLifetime;
		maintenanceInterval = new TimeSpan(idleEntryLifetime.Ticks / 3);
	}

	internal ICustomActionConnCacheEntry<K, C> Acquire(K connKey)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "Acquire: entering, key={0}", (connKey != null) ? connKey.ToString() : "null");
		CheckDisposed();
		DoCacheMaintenance();
		if (connKey == null)
		{
			throw new ArgumentNullException("connKey");
		}
		lock (this)
		{
			if (cache.TryGetValue(connKey, out var value))
			{
				value.IncInUseRefCount();
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "CustomActionConnCache", "Acquire: key={0} not found in cache", connKey.ToString());
			}
			return value;
		}
	}

	internal void Release(ICustomActionConnCacheEntry<K, C> cacheEntry)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "Release: entering, key={0}", (cacheEntry != null && cacheEntry.ConnectionKey != null) ? cacheEntry.ConnectionKey.ToString() : "null");
		CheckDisposed();
		DoCacheMaintenance();
		if (cacheEntry == null)
		{
			throw new ArgumentNullException("cacheEntry");
		}
		CustomActionConnCacheEntry<K, C> customActionConnCacheEntry = cacheEntry as CustomActionConnCacheEntry<K, C>;
		if (customActionConnCacheEntry.IsFakeEntry)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "Release: fake cache entry, ignoring it");
		}
		else
		{
			customActionConnCacheEntry.DecInUseRefCount();
		}
	}

	internal AddResult AddIfNotPresent(K connKey, C connObject, out ICustomActionConnCacheEntry<K, C> newlyAddedCacheEntry)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "AddIfNotPresent: entering, key={0}", (connKey != null) ? connKey.ToString() : "null");
		newlyAddedCacheEntry = null;
		CheckDisposed();
		DoCacheMaintenance();
		if (connKey == null)
		{
			throw new ArgumentNullException("connKey");
		}
		if (connObject == null)
		{
			throw new ArgumentNullException("connObject");
		}
		lock (this)
		{
			if (cache.ContainsKey(connKey))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "CustomActionConnCache", "AddIfNotPresent: key={0} already present in cache", connKey.ToString());
				return AddResult.AlreadyPresent;
			}
			bool flag = true;
			if (cache.Count == maxCacheSize)
			{
				flag = EvictEntry();
			}
			if (flag)
			{
				CustomActionConnCacheEntry<K, C> customActionConnCacheEntry = new CustomActionConnCacheEntry<K, C>(connKey, connObject);
				cache[connKey] = customActionConnCacheEntry;
				customActionConnCacheEntry.IncInUseRefCount();
				newlyAddedCacheEntry = customActionConnCacheEntry;
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "CustomActionConnCache", "AddIfNotPresent: added key={0} to cache", connKey.ToString());
				PerfCounters.CustActCombinedConnCount.Increment();
				return AddResult.SuccessfullyAdded;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "CustomActionConnCache", "AddIfNotPresent: no room for key={0} in cache", connKey.ToString());
			return AddResult.NoRoomInCache;
		}
	}

	internal void Remove(K connKey)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "Remove: entering, key={0}", (connKey != null) ? connKey.ToString() : "null");
		CheckDisposed();
		DoCacheMaintenance();
		if (connKey == null)
		{
			throw new ArgumentNullException("connKey");
		}
		lock (this)
		{
			if (cache.TryGetValue(connKey, out var value))
			{
				if (value.InUseRefCount != 0)
				{
					throw new InvalidOperationException();
				}
				PerfCounters.CustActCombinedConnCount.Decrement();
				cache.Remove(connKey);
				value.Dispose();
			}
		}
	}

	internal ICustomActionConnCacheEntry<K, C> CreateFakeEntry(K connKey, C connObject)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "CreateFakeEntry: entering, key={0}", (connKey != null) ? connKey.ToString() : "null");
		CheckDisposed();
		if (connKey == null)
		{
			throw new ArgumentNullException("connKey");
		}
		if (connObject == null)
		{
			throw new ArgumentNullException("connObject");
		}
		return new CustomActionConnCacheEntry<K, C>(connKey, connObject, isFakeEntry: true);
	}

	public void Dispose()
	{
		lock (this)
		{
			if (disposed)
			{
				return;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "Dispose: disposing...");
			foreach (CustomActionConnCacheEntry<K, C> value in cache.Values)
			{
				value.Dispose();
			}
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "CustomActionConnCache", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	private void DoCacheMaintenance()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "DoCacheMaintenance: entering");
		DateTime now = DateTime.Now;
		if (!(now >= lastCacheMaintenancePass + maintenanceInterval))
		{
			return;
		}
		lock (this)
		{
			if (!(now >= lastCacheMaintenancePass + maintenanceInterval))
			{
				return;
			}
			List<CustomActionConnCacheEntry<K, C>> list = new List<CustomActionConnCacheEntry<K, C>>();
			foreach (CustomActionConnCacheEntry<K, C> value in cache.Values)
			{
				if (value.InUseRefCount == 0 && value.LastUsed < now - maxIdleEntryLifetime)
				{
					list.Add(value);
				}
			}
			lastCacheMaintenancePass = now;
			foreach (CustomActionConnCacheEntry<K, C> item in list)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "DoCacheMaintenance: removing entry for Key={0}", item.ConnectionKey.ToString());
				PerfCounters.CustActCombinedConnCount.Decrement();
				cache.Remove(item.ConnectionKey);
				item.Dispose();
			}
		}
	}

	private bool EvictEntry()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "EvictEntry: entering");
		CustomActionConnCacheEntry<K, C> customActionConnCacheEntry = null;
		lock (this)
		{
			foreach (CustomActionConnCacheEntry<K, C> value in cache.Values)
			{
				if (value.InUseRefCount == 0 && (customActionConnCacheEntry == null || customActionConnCacheEntry.LastUsed > value.LastUsed))
				{
					customActionConnCacheEntry = value;
				}
			}
			if (customActionConnCacheEntry == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "EvictEntry: no entry available for eviction");
				return false;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCache", "EvictEntry: evicting entry for Key={0}", customActionConnCacheEntry.ConnectionKey.ToString());
			PerfCounters.CustActCombinedConnCount.Decrement();
			cache.Remove(customActionConnCacheEntry.ConnectionKey);
			customActionConnCacheEntry.Dispose();
			return true;
		}
	}
}

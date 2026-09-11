using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;

namespace Microsoft.ActiveDirectory.WebServices;

internal class EnumerationContextCache : IDisposable
{
	public enum ResultCode
	{
		Success,
		NoSuchEntry,
		ExpiresInThePast,
		ExpirationExceedsLimit,
		SessionMismatch,
		Busy
	}

	private Dictionary<Guid, EnumerationContextCacheEntry> guidMap;

	private Dictionary<Query, EnumerationContextCacheEntry> queryMap;

	private Mutex cacheLock;

	private bool disposed;

	private QuotaTracker<ActiveDirectoryWebService> wsSessions;

	private int syncPoint;

	private System.Timers.Timer timer;

	private uint enumerationContextCacheCount;

	internal EnumerationContextCache()
	{
		guidMap = new Dictionary<Guid, EnumerationContextCacheEntry>();
		queryMap = new Dictionary<Query, EnumerationContextCacheEntry>();
		cacheLock = new Mutex();
		enumerationContextCacheCount = 0u;
		wsSessions = new QuotaTracker<ActiveDirectoryWebService>();
		TimeSpan enumCacheTimerAlarmInterval = AdwsConstants.EnumCacheTimerAlarmInterval;
		GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "EnumerationContextCache: using timer inverval {0}", enumCacheTimerAlarmInterval);
		timer = new System.Timers.Timer(enumCacheTimerAlarmInterval.TotalMilliseconds);
		timer.Elapsed += OnTimedEvent;
		timer.Enabled = true;
	}

	internal void LockCache()
	{
		CheckDisposed();
		cacheLock.WaitOne();
	}

	internal void UnlockCache()
	{
		CheckDisposed();
		cacheLock.ReleaseMutex();
	}

	internal ResultCode AddEntry(EnumerationContextCacheEntry entry)
	{
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		if (entry == null)
		{
			throw new ArgumentNullException("entry");
		}
		if (entry.Query == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "AddEntry: entry {0} has a null Query", entry.EnumerationContext);
			throw new ArgumentException(AdwsStrings.InvalidEnumerationCacheEntry);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "AddEntry: adding entry w/EnumerationContext={0}", entry.EnumerationContext);
		if (entry.Expires <= utcNow)
		{
			return ResultCode.ExpiresInThePast;
		}
		if (entry.Expires > utcNow + ConfigurationSettings.MaxEnumContextExpiration)
		{
			return ResultCode.ExpirationExceedsLimit;
		}
		LockCache();
		try
		{
			if (guidMap.ContainsKey(entry.EnumerationContext) || queryMap.ContainsKey(entry.Query))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "EnumerationContextCache", "AddEntry: duplicate entry {0}", entry.EnumerationContext);
				throw new ArgumentException(AdwsStrings.DuplicateEnumerationCacheEntry);
			}
			if (enumerationContextCacheCount >= ConfigurationSettings.MaxEnumCtxsTotal)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "AddEntry: MaxEnumCtxsTotal Limit reached");
				throw new EnumerationContextLimitExceededException();
			}
			if (!wsSessions.TryToIncrementCount(entry.Session, ConfigurationSettings.MaxEnumCtxsPerSession))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "AddEntry: MaxEnumCtxsPerSession Limit reached");
				throw new EnumerationContextLimitExceededException();
			}
			guidMap.Add(entry.EnumerationContext, entry);
			try
			{
				queryMap.Add(entry.Query, entry);
				enumerationContextCacheCount++;
				PerfCounters.OpenEnumCtxs.Increment();
			}
			catch (Exception)
			{
				guidMap.Remove(entry.EnumerationContext);
				throw;
			}
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "AddEntry: exit result={0}", ResultCode.Success);
		return ResultCode.Success;
	}

	internal EnumerationContextCacheEntry GetEntry(Guid enumContext)
	{
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "GetEntry(enumContext): enumContext={0}", enumContext);
		EnumerationContextCacheEntry value = null;
		LockCache();
		try
		{
			guidMap.TryGetValue(enumContext, out value);
			if (value == null || value.DeletePendingEntry)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "GetEntry(enumContext): entry not found");
				return null;
			}
			lock (value)
			{
				if (value.Expires <= utcNow)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "GetEntry(enumContext): entry has expired already.");
					return null;
				}
			}
			value.IncInUseRefCount();
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "GetEntry(enumContext): success");
		return value;
	}

	internal EnumerationContextCacheEntry GetEntry(Query query)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "GetEntry(query): entered");
		return GetEntry(query, honorRefCount: true);
	}

	private EnumerationContextCacheEntry GetEntry(Query query, bool honorRefCount)
	{
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		if (query == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "GetEntry(query, honorRefCount): null query");
			throw new ArgumentNullException("query");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "GetEntry(query, honorRefCount): entered");
		EnumerationContextCacheEntry value = null;
		LockCache();
		try
		{
			queryMap.TryGetValue(query, out value);
			if (value == null || value.DeletePendingEntry)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "GetEntry(query, honorRefCount): entry not found");
				return null;
			}
			lock (value)
			{
				if (value.Expires <= utcNow)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "GetEntry(query, honorRefCount): entry has expired already.");
					return null;
				}
			}
			if (honorRefCount)
			{
				value.IncInUseRefCount();
			}
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "GetEntry(query, honorRefCount): success");
		return value;
	}

	internal void ReturnToCache(EnumerationContextCacheEntry entry)
	{
		CheckDisposed();
		if (entry == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "ReturnToCache null entry");
			throw new ArgumentNullException("entry");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "ReturnToCache entered enumContext={0}", entry.EnumerationContext);
		entry.DecInUseRefCount();
	}

	internal ResultCode RemoveEntry(Guid enumContext)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveEntry(enumContext): enumContext={0}", enumContext);
		return RemoveEntry(enumContext, honorRefCount: true);
	}

	internal ResultCode RemoveTimedOutOrExhaustedEntry(Guid enumContext)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveTimedOutOrExhaustedEntry(enumContext): enumContext={0}", enumContext);
		return RemoveEntry(enumContext, honorRefCount: false);
	}

	private ResultCode RemoveEntry(Guid enumContext, bool honorRefCount)
	{
		CheckDisposed();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveEntry(enumContext): enumContext={0}", enumContext);
		EnumerationContextCacheEntry value = null;
		LockCache();
		try
		{
			guidMap.TryGetValue(enumContext, out value);
			if (value == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "RemoveEntry(enumContext): no such entry");
				return ResultCode.NoSuchEntry;
			}
			lock (value)
			{
				if (value.InUseRefCount != 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "EnumerationContextCache", "RemoveEntry(enumContext): InUseRefCount > 0");
					if (!honorRefCount)
					{
						value.DeletePendingEntry = true;
						return ResultCode.Success;
					}
					return ResultCode.Busy;
				}
			}
			guidMap.Remove(enumContext);
			queryMap.Remove(value.Query);
			enumerationContextCacheCount--;
			wsSessions.DecrementCount(value.Session);
			PerfCounters.OpenEnumCtxs.Decrement();
			value.Dispose();
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveEntry(enumContext): exit success");
		return ResultCode.Success;
	}

	private void RemoveExpiredEntries()
	{
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveExpiredEntries called");
		IList<EnumerationContextCacheEntry> list = new List<EnumerationContextCacheEntry>();
		LockCache();
		try
		{
			foreach (EnumerationContextCacheEntry value in guidMap.Values)
			{
				lock (value)
				{
					if (value.InUseRefCount == 0 && (value.Expires <= utcNow || value.DeletePendingEntry))
					{
						list.Add(value);
					}
				}
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveExpiredEntries -- found {0} entries to remove", list.Count);
			foreach (EnumerationContextCacheEntry item in list)
			{
				lock (item)
				{
					if (item.InUseRefCount == 0 && (item.Expires <= utcNow || item.DeletePendingEntry))
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveExpiredEntries -- removing entry w/GUID={0}", item.EnumerationContext);
						guidMap.Remove(item.EnumerationContext);
						queryMap.Remove(item.Query);
						enumerationContextCacheCount--;
						wsSessions.DecrementCount(item.Session);
						PerfCounters.OpenEnumCtxs.Decrement();
						item.Dispose();
					}
				}
			}
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveExpiredEntries done");
	}

	internal ResultCode UpdateEntryExpiration(Guid enumContext, DateTime newExpiration)
	{
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "UpdateEntryExpiration: enumContext={0} newExpiration={1}", enumContext, newExpiration);
		if (newExpiration <= utcNow)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "UpdateEntryExpiration: returning ExpiresInThePast");
			return ResultCode.ExpiresInThePast;
		}
		if (newExpiration > utcNow + ConfigurationSettings.MaxEnumContextExpiration)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "UpdateEntryExpiration: returning ExpierationExceedsLimit");
			return ResultCode.ExpirationExceedsLimit;
		}
		EnumerationContextCacheEntry value = null;
		LockCache();
		try
		{
			guidMap.TryGetValue(enumContext, out value);
			if (value == null || value.DeletePendingEntry)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "UpdateEntryExpiration: entry not found");
				return ResultCode.NoSuchEntry;
			}
			lock (value)
			{
				if (value.Expires <= utcNow)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "UpdateEntryExpiration: entry has expired already.");
					return ResultCode.NoSuchEntry;
				}
				value.Expires = newExpiration;
			}
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "UpdateEntryExpiration: success");
		return ResultCode.Success;
	}

	internal ResultCode GetEntryExpiration(Guid enumContext, out DateTime expirationTime)
	{
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "GetEntryExpiration: enumContext={0}", enumContext);
		EnumerationContextCacheEntry value = null;
		expirationTime = DateTime.UtcNow;
		LockCache();
		try
		{
			guidMap.TryGetValue(enumContext, out value);
			if (value == null || value.DeletePendingEntry)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "GetEntryExpiration: entry not found");
				return ResultCode.NoSuchEntry;
			}
			lock (value)
			{
				if (value.Expires <= utcNow)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "GetEntryExpiration: entry has expired already.");
					return ResultCode.NoSuchEntry;
				}
				expirationTime = value.Expires;
			}
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "GetEntryExpiration: success");
		return ResultCode.Success;
	}

	internal ResultCode IsValidEnumContextForSession(Guid enumContext, ActiveDirectoryWebService session)
	{
		CheckDisposed();
		DateTime utcNow = DateTime.UtcNow;
		ResultCode resultCode = ResultCode.Success;
		EnumerationContextCacheEntry value = null;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache(session)", "IsValidEnumContextForSession: enumContext={0}", enumContext);
		if (session == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "IsValidEnumContextForSession: session is null");
			throw new ArgumentNullException("session");
		}
		LockCache();
		try
		{
			guidMap.TryGetValue(enumContext, out value);
			if (value == null || value.DeletePendingEntry)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "IsValidEnumContextForSession: entry not found");
				return ResultCode.NoSuchEntry;
			}
			lock (value)
			{
				if (value.Expires <= utcNow)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "IsValidEnumContextForSession: entry has expired already.");
					return ResultCode.NoSuchEntry;
				}
				resultCode = ((value.Session != session) ? ResultCode.SessionMismatch : ResultCode.Success);
			}
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "IsValidEnumContextForSession: returning {0}", resultCode);
		return resultCode;
	}

	internal void RemoveAssociatedEntries(ActiveDirectoryWebService session)
	{
		CheckDisposed();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveAssociatedEntries called");
		if (session == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCache", "RemoveAssociatedEntries: session is null");
			throw new ArgumentNullException("session");
		}
		IList<EnumerationContextCacheEntry> list = new List<EnumerationContextCacheEntry>();
		LockCache();
		try
		{
			foreach (EnumerationContextCacheEntry value in guidMap.Values)
			{
				lock (value)
				{
					if (value.Session == session)
					{
						list.Add(value);
					}
				}
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveAssociatedEntries -- found {0} entries to remove", list.Count);
			foreach (EnumerationContextCacheEntry item in list)
			{
				lock (item)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveAssociatedEntries -- removing entry w/GUID={0}", item.EnumerationContext);
					guidMap.Remove(item.EnumerationContext);
					queryMap.Remove(item.Query);
					enumerationContextCacheCount--;
					wsSessions.DecrementCount(session);
					PerfCounters.OpenEnumCtxs.Decrement();
					item.Dispose();
				}
			}
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "RemoveAssociatedEntries done");
	}

	private void OnTimedEvent(object source, ElapsedEventArgs e)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "OnTimedEvent: got an event");
		int num = Interlocked.CompareExchange(ref syncPoint, 1, 0);
		if (num == 0)
		{
			RemoveExpiredEntries();
			syncPoint = 0;
		}
		else
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "OnTimedEvent: skipped ({0})", num);
		}
	}

	internal void TimeoutHandler(Query timedoutQuery, bool wasIdle)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "TimeoutHandler: got called (wasIdle = {0})", wasIdle.ToString());
		CheckDisposed();
		LockCache();
		try
		{
			EnumerationContextCacheEntry entry = GetEntry(timedoutQuery, honorRefCount: false);
			if (entry != null)
			{
				ResultCode resultCode = RemoveTimedOutOrExhaustedEntry(entry.EnumerationContext);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "TimeoutHandler: ResultCode for RemoveTimedoutEntry {0}", resultCode.ToString());
				switch (resultCode)
				{
				case ResultCode.Success:
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "TimeoutHandler: Removed the timedout entry successfully");
					break;
				case ResultCode.NoSuchEntry:
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "TimeoutHandler: There was nothing to remove or dispose because the entry wasn't present in the cache.");
					break;
				}
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "TimeoutHandler: There was nothing to remove or dispose because the entry wasn't present in the cache or it was an expired / dead (deletePending) entry.");
			}
		}
		finally
		{
			UnlockCache();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "TimeoutHandler: exit");
	}

	public void Dispose()
	{
		if (disposed)
		{
			return;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "Dispose: disposing...");
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
		LockCache();
		try
		{
			foreach (EnumerationContextCacheEntry value in guidMap.Values)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCache", "Dispose: disposing entry Guid={0}", value.EnumerationContext);
				value.Dispose();
			}
			guidMap.Clear();
			queryMap.Clear();
			wsSessions.Clear();
		}
		finally
		{
			UnlockCache();
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCacheEntry", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}
}

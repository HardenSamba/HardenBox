using System;

namespace Microsoft.ActiveDirectory.WebServices;

internal class CustomActionConnCacheEntry<K, C> : ICustomActionConnCacheEntry<K, C>, IDisposable
{
	private K connKey;

	private C connObject;

	private bool isFakeEntry;

	private DateTime creationTime = DateTime.Now;

	private DateTime lastUsedTime = DateTime.Now;

	private uint inUseRefCount;

	private bool disposed;

	public K ConnectionKey
	{
		get
		{
			CheckDisposed();
			return connKey;
		}
	}

	public C ConnectionObject
	{
		get
		{
			CheckDisposed();
			return connObject;
		}
	}

	public bool IsFakeEntry
	{
		get
		{
			CheckDisposed();
			return isFakeEntry;
		}
	}

	internal DateTime Created
	{
		get
		{
			CheckDisposed();
			return creationTime;
		}
	}

	internal DateTime LastUsed
	{
		get
		{
			CheckDisposed();
			lock (this)
			{
				return lastUsedTime;
			}
		}
	}

	internal uint InUseRefCount
	{
		get
		{
			CheckDisposed();
			lock (this)
			{
				return inUseRefCount;
			}
		}
	}

	internal CustomActionConnCacheEntry(K connectionKey, C connectionObject, bool isFakeEntry)
	{
		if (connectionKey == null)
		{
			throw new ArgumentNullException("connectionKey");
		}
		if (connectionObject == null)
		{
			throw new ArgumentNullException("connectionObject");
		}
		connKey = connectionKey;
		connObject = connectionObject;
		this.isFakeEntry = isFakeEntry;
	}

	internal CustomActionConnCacheEntry(K connectionKey, C connectionObject)
		: this(connectionKey, connectionObject, false)
	{
	}

	internal void IncInUseRefCount()
	{
		CheckDisposed();
		lock (this)
		{
			lastUsedTime = DateTime.Now;
			inUseRefCount++;
		}
	}

	internal void DecInUseRefCount()
	{
		CheckDisposed();
		lock (this)
		{
			inUseRefCount--;
		}
	}

	public void Dispose()
	{
		lock (this)
		{
			if (!disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "CustomActionConnCacheEntry", "Dispose: disposing...");
				if (connKey is IDisposable)
				{
					((IDisposable)(object)connKey).Dispose();
				}
				if (connObject is IDisposable)
				{
					((IDisposable)(object)connObject).Dispose();
				}
				disposed = true;
			}
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "CustomActionConnCacheEntry", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}
}

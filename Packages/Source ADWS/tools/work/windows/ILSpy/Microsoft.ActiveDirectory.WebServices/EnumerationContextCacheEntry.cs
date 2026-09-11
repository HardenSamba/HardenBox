using System;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSAddressing;

namespace Microsoft.ActiveDirectory.WebServices;

internal class EnumerationContextCacheEntry : IDisposable
{
	private int inUseRefCount;

	private bool deletePendingEntry;

	private Guid enumerationContext;

	private InstanceType instance;

	private Query query;

	private DateTime creationTime;

	private DateTime expires;

	private EndpointReferenceType endTo;

	private ActiveDirectoryWebService session;

	private bool disposed;

	internal int InUseRefCount
	{
		get
		{
			CheckDisposed();
			return inUseRefCount;
		}
	}

	internal bool DeletePendingEntry
	{
		get
		{
			CheckDisposed();
			return deletePendingEntry;
		}
		set
		{
			CheckDisposed();
			deletePendingEntry = value;
		}
	}

	internal Guid EnumerationContext
	{
		get
		{
			CheckDisposed();
			return enumerationContext;
		}
	}

	internal InstanceType Instance
	{
		get
		{
			CheckDisposed();
			return instance;
		}
	}

	internal Query Query
	{
		get
		{
			CheckDisposed();
			return query;
		}
		set
		{
			CheckDisposed();
			query = value;
		}
	}

	internal DateTime CreationTime
	{
		get
		{
			CheckDisposed();
			return creationTime;
		}
	}

	internal DateTime Expires
	{
		get
		{
			CheckDisposed();
			return expires;
		}
		set
		{
			CheckDisposed();
			expires = value;
		}
	}

	internal EndpointReferenceType EndTo
	{
		get
		{
			CheckDisposed();
			return endTo;
		}
		set
		{
			CheckDisposed();
			endTo = value;
		}
	}

	internal ActiveDirectoryWebService Session
	{
		get
		{
			CheckDisposed();
			return session;
		}
	}

	internal EnumerationContextCacheEntry(InstanceType instance, ActiveDirectoryWebService session)
	{
		if (instance == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCacheEntry", "Constructor: instance is null");
			throw new ArgumentNullException("instance");
		}
		if (session == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "EnumerationContextCacheEntry", "Constructor: session is null");
			throw new ArgumentNullException("session");
		}
		this.instance = instance;
		enumerationContext = Guid.NewGuid();
		creationTime = DateTime.UtcNow;
		inUseRefCount = 0;
		this.session = session;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCacheEntry", "Constructor: Guid is {0}", EnumerationContext);
	}

	internal int IncInUseRefCount()
	{
		CheckDisposed();
		lock (this)
		{
			return ++inUseRefCount;
		}
	}

	internal int DecInUseRefCount()
	{
		CheckDisposed();
		lock (this)
		{
			return --inUseRefCount;
		}
	}

	public void Dispose()
	{
		lock (this)
		{
			if (!disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "EnumerationContextCacheEntry", "Dispose: disposing...");
				if (Query != null)
				{
					Query.Dispose();
				}
				disposed = true;
			}
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

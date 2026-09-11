using System;
using System.Security.Principal;

namespace Microsoft.ActiveDirectory.WebServices;

internal class ReservationToken : IDisposable
{
	private ConnectionPoolEntry associatedConnectionPoolEntry;

	private InstanceType instance;

	private DateTime lastUsedTime;

	private DateTime timeReserved;

	private SecurityIdentifier reservingUser;

	private SessionPoolNotifyOfTimeout routineToNotify;

	private bool valid = true;

	private bool disposed;

	internal ConnectionPoolEntry AssociatedConnectionPoolEntry
	{
		get
		{
			CheckDisposed();
			return associatedConnectionPoolEntry;
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

	internal DateTime LastUsedTime
	{
		get
		{
			CheckDisposed();
			return lastUsedTime;
		}
	}

	internal DateTime TimeReserved
	{
		get
		{
			CheckDisposed();
			lock (this)
			{
				return timeReserved;
			}
		}
	}

	internal SecurityIdentifier ReservingUser
	{
		get
		{
			CheckDisposed();
			return reservingUser;
		}
	}

	internal SessionPoolNotifyOfTimeout RoutineToNotify
	{
		get
		{
			CheckDisposed();
			return routineToNotify;
		}
	}

	internal ReservationToken(InstanceType instance, ConnectionPoolEntry associatedEntry, SessionPoolNotifyOfTimeout routineToNotify)
	{
		associatedConnectionPoolEntry = associatedEntry;
		this.instance = instance;
		this.routineToNotify = routineToNotify;
		timeReserved = DateTime.UtcNow;
		lastUsedTime = timeReserved;
		using (WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent())
		{
			reservingUser = windowsIdentity.User;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ReservationToken", "ReservationToken: token created at time {0} by {1}", timeReserved.ToString(), reservingUser.ToString());
	}

	internal void UpdateLastUsedTime()
	{
		CheckDisposed();
		lock (this)
		{
			lastUsedTime = DateTime.UtcNow;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ReservationToken", "UpdateLastUsedTime: new last used time is {0}", lastUsedTime.ToString());
		}
	}

	internal void InvalidateToken()
	{
		CheckDisposed();
		lock (this)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ReservationToken", "InvalidateToken: invalidating the token (validity was {0})", valid.ToString());
			valid = false;
		}
	}

	internal bool IsValid()
	{
		CheckDisposed();
		lock (this)
		{
			return valid;
		}
	}

	public void Dispose()
	{
		lock (this)
		{
			if (!disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ReservationToken", "Dispose: disposing...");
				if (valid)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ReservationToken", "Dispose: releasing the reservation");
					ClassManager.SessionPool.ReleaseReservation(this);
				}
				routineToNotify = null;
				disposed = true;
			}
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ReservationToken", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}
}

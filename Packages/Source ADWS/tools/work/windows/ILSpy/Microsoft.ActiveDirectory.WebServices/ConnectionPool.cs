using System;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;

namespace Microsoft.ActiveDirectory.WebServices;

internal class ConnectionPool : IDisposable
{
	private enum InitialEntryState
	{
		Free,
		InUse,
		Reserved
	}

	private bool disposed;

	private uint maxNumberOfConnections;

	private InstanceType instance;

	private uint connectionsInUse;

	private Semaphore connectionsAvailable;

	private uint connectionsReserved;

	private bool allowConnectionsToBeAcquired = true;

	private List<ConnectionPoolEntry> connections;

	private List<ReservationToken> reservationTokens;

	private bool encounteredErrorOnPreviousBind;

	internal ConnectionPool(InstanceType instance, uint initialConnectionCount, uint maxConnectionCount)
	{
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (maxConnectionCount == 0 || initialConnectionCount > maxConnectionCount)
		{
			throw new ArgumentException(AdwsStrings.MaxUsersIsTooSmall);
		}
		maxNumberOfConnections = maxConnectionCount;
		connections = new List<ConnectionPoolEntry>((int)maxConnectionCount);
		this.instance = instance;
		connectionsAvailable = new Semaphore((int)maxConnectionCount, (int)maxConnectionCount);
		reservationTokens = new List<ReservationToken>((int)maxConnectionCount);
		PerfCounters.PossibleConns.Adjust(maxNumberOfConnections);
		for (uint num = 0u; num < initialConnectionCount; num++)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "ConnectionPool: trying to add connection {0}", num);
			AddConnectionIfPossible(InitialEntryState.Free);
		}
	}

	internal ConnectionPoolEntry GetAvailableConnectionAsCurrentUser(TimeSpan timeToWaitToAcquire)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetAvailableConnectionAsCurrentUser: entering, wait={0}, instance={1}", timeToWaitToAcquire.ToString(), instance.InstanceName);
		CheckDisposed();
		lock (this)
		{
			if (!allowConnectionsToBeAcquired)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetAvailableConnectionAsCurrentUser: pool {0} is shutting down, exiting", instance.InstanceName);
				return null;
			}
		}
		ConnectionPoolEntry freeEntryAsCurrentUser = GetFreeEntryAsCurrentUser(timeToWaitToAcquire, InitialEntryState.InUse);
		if (freeEntryAsCurrentUser == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetAvailableConnectionAsCurrentUser: couldn't get connection (wait={0}, instance={1})", timeToWaitToAcquire.ToString(), instance.InstanceName);
			return null;
		}
		lock (this)
		{
			connectionsInUse++;
			PerfCounters.NonReservedInUseConns.Increment();
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetAvailableConnectionAsCurrentUser: got connection, instance={0}, new entry count={1}, new pool count={2}", instance.InstanceName, freeEntryAsCurrentUser.InUseCount, connectionsInUse);
			return freeEntryAsCurrentUser;
		}
	}

	internal void ReleaseConnection(ConnectionPoolEntry entry)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "ReleaseConnection: entering (instance={0})", instance.InstanceName);
		CheckDisposed();
		if (entry == null)
		{
			throw new ArgumentNullException("entry");
		}
		lock (this)
		{
			lock (entry)
			{
				if (entry.Pool != this)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "ReleaseConnection: wrong pool (this={0}, entry={1})", instance.InstanceName, entry.Pool.instance.InstanceName);
					throw new ArgumentException(AdwsStrings.WrongPool);
				}
				if (entry.InUseCount == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "ReleaseConnection: trying to release not in-use entry (instance={0})", instance.InstanceName);
					throw new ArgumentException(AdwsStrings.EntryNotInuse);
				}
				if (entry.IsReserved)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "ReleaseConnection: trying to release reserved entry (instance={0})", instance.InstanceName);
					throw new ArgumentException(AdwsStrings.EntryReserved);
				}
				entry.InUseCount--;
				connectionsInUse--;
				connectionsAvailable.Release();
				PerfCounters.NonReservedInUseConns.Decrement();
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "ReleaseConnection: released connection (instance={0}, new entry count={1}, new pool count={2})", instance.InstanceName, entry.InUseCount, connectionsInUse);
			}
		}
	}

	internal ConnectionPoolEntry GetReservedConnection(ReservationToken token)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetReservedConnection: entering, instance={0}", instance.InstanceName);
		CheckDisposed();
		if (token == null)
		{
			throw new ArgumentNullException("token");
		}
		lock (this)
		{
			if (!allowConnectionsToBeAcquired)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetReservedConnection: pool {0} is shutting down, exiting", instance.InstanceName);
				return null;
			}
			lock (token)
			{
				if (!token.Instance.Equals(instance))
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetReservedConnection: wrong instance (this={0}, token={1})", instance.InstanceName, token.Instance.InstanceName);
					throw new ArgumentException(AdwsStrings.WrongPool);
				}
				if (!token.IsValid())
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetReservedConnection: invalid token (instance={0})", instance.InstanceName);
					throw new InvalidReservationTokenException(AdwsStrings.InvalidReservationToken);
				}
				ConnectionPoolEntry associatedConnectionPoolEntry = token.AssociatedConnectionPoolEntry;
				lock (associatedConnectionPoolEntry)
				{
					if (associatedConnectionPoolEntry.Pool != this)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetReservedConnection: wrong entry pool (this={0}, entry={1})", instance.InstanceName, associatedConnectionPoolEntry.Pool.instance.InstanceName);
						throw new ArgumentException(AdwsStrings.WrongPool);
					}
					associatedConnectionPoolEntry.InUseCount++;
					if (associatedConnectionPoolEntry.InUseCount == 1)
					{
						connectionsInUse++;
						PerfCounters.ReservedInUseConns.Increment();
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetReservedConnection: incrementing pool use count, instance={0}", instance.InstanceName);
					}
				}
				token.UpdateLastUsedTime();
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetReservedConnection: got reserved connection, instance={0}, new pool count={1}, new entry count={2}", instance.InstanceName, connectionsInUse, associatedConnectionPoolEntry.InUseCount);
				return associatedConnectionPoolEntry;
			}
		}
	}

	internal void ReleaseReservedConnection(ConnectionPoolEntry entry)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "ReleaseReservedConnection: entering, instance={0}", instance.InstanceName);
		CheckDisposed();
		if (entry == null)
		{
			throw new ArgumentNullException("entry");
		}
		lock (this)
		{
			lock (entry)
			{
				if (entry.Pool != this)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "ReleaseReservedConnection: wrong pool (this={0}, entry={1})", instance.InstanceName, entry.Pool.instance.InstanceName);
					throw new ArgumentException(AdwsStrings.WrongPool);
				}
				if (!entry.IsReserved)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "ReleaseReservedConnection: trying to release non-reserved entry (instance={0})", instance.InstanceName);
					throw new ArgumentException(AdwsStrings.EntryNotReserved);
				}
				if (entry.InUseCount == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "ReleaseReservedConnection: trying to release entry not in-use (instance={0})", instance.InstanceName);
					throw new ArgumentException(AdwsStrings.EntryNotInuse);
				}
				entry.InUseCount--;
				if (entry.InUseCount == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "ReleaseReservedConnection: decrementing pool use count, instance={0}", instance.InstanceName);
					connectionsInUse--;
					PerfCounters.ReservedInUseConns.Decrement();
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "ReleaseReservedConnection: released reserved connection, instance={0}, new pool count={1}, new entry count={2}", instance.InstanceName, connectionsInUse, entry.InUseCount);
			}
		}
	}

	internal ReservationToken AllocateReservedConnection(TimeSpan timeToWaitToAcquire, SessionPoolNotifyOfTimeout routineToNotify)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "AllocateReservedConnection: entering, instance={0}", instance.InstanceName);
		CheckDisposed();
		lock (this)
		{
			if (!allowConnectionsToBeAcquired)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "AllocateReservedConnection: pool {0} is shutting down, exiting", instance.InstanceName);
				return null;
			}
			uint num = ConfigurationSettings.MaxPercentageReservedConnections * maxNumberOfConnections / 100;
			if (connectionsReserved >= num)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "AllocateReservedConnection: reached max reservable percentage of pool {0} ({1} reserved connections)", instance.InstanceName, connectionsReserved);
				return null;
			}
			connectionsReserved++;
			PerfCounters.ReservedConns.Increment();
		}
		bool flag = false;
		try
		{
			ConnectionPoolEntry freeEntryAsCurrentUser = GetFreeEntryAsCurrentUser(timeToWaitToAcquire, InitialEntryState.Reserved);
			if (freeEntryAsCurrentUser == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "AllocateReservedConnection: couldn't get connection (wait={0}, instance={1})", timeToWaitToAcquire.ToString(), instance.InstanceName);
				return null;
			}
			lock (this)
			{
				ReservationToken reservationToken = new ReservationToken(instance, freeEntryAsCurrentUser, routineToNotify);
				reservationTokens.Add(reservationToken);
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "AllocateReservedConnection: got token, instance={0}, new pool reserved count={1}", instance.InstanceName, connectionsReserved);
				flag = true;
				return reservationToken;
			}
		}
		finally
		{
			if (!flag)
			{
				lock (this)
				{
					connectionsReserved--;
					PerfCounters.ReservedConns.Decrement();
				}
			}
		}
	}

	internal void FreeReservedConnection(ReservationToken token)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "FreeReservedConnection: entering, instance={0}", instance.InstanceName);
		CheckDisposed();
		ConnectionPoolEntry connectionPoolEntry = null;
		if (token == null)
		{
			throw new ArgumentNullException("token");
		}
		lock (this)
		{
			lock (token)
			{
				if (!token.Instance.Equals(instance))
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "FreeReservedConnection: wrong instance (this={0}, token={1})", instance.InstanceName, token.Instance.InstanceName);
					throw new ArgumentException(AdwsStrings.WrongPool);
				}
				if (!token.IsValid())
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "FreeReservedConnection: invalid token (instance={0})", instance.InstanceName);
					throw new InvalidReservationTokenException(AdwsStrings.InvalidReservationToken);
				}
				connectionPoolEntry = token.AssociatedConnectionPoolEntry;
				lock (connectionPoolEntry)
				{
					if (connectionPoolEntry.Pool != this)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "FreeReservedConnection: wrong entry pool (this={0}, entry={1})", instance.InstanceName, connectionPoolEntry.Pool.instance.InstanceName);
						throw new ArgumentException(AdwsStrings.WrongPool);
					}
					if (connectionPoolEntry.InUseCount != 0)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "FreeReservedConnection: trying to release in-use entry (instance={0}, count={1})", instance.InstanceName, connectionPoolEntry.InUseCount);
						throw new InvalidOperationException(AdwsStrings.TriedToFreeInUseReservedConnection);
					}
					connectionPoolEntry.IsReserved = false;
				}
				token.InvalidateToken();
				reservationTokens.Remove(token);
			}
			connectionsReserved--;
			connectionsAvailable.Release();
			PerfCounters.ReservedConns.Decrement();
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "FreeReservedConnection: released token (instance={0}, new pool reserved count={1})", instance.InstanceName, connectionsReserved);
		}
	}

	internal void ProhibitConnectionAcquisition()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "ProhibitConnectionAcquisition: entering, instance={0}", instance.InstanceName);
		CheckDisposed();
		lock (this)
		{
			allowConnectionsToBeAcquired = false;
		}
	}

	internal void WaitForAllConnectionsToBeFree()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "WaitForAllConnectionsToBeFree: entering, instance={0}", instance.InstanceName);
		CheckDisposed();
		uint num = 0u;
		uint num2 = maxNumberOfConnections;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "WaitForAllConnectionsToBeFree: Need to get {0} connections", num2);
		while (num < num2)
		{
			connectionsAvailable.WaitOne();
			num++;
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "WaitForAllConnectionsToBeFree: Got {0} connections so far", num);
		}
	}

	public IEnumerator<ReservationToken> GetReservationEnumerator()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetReservationEnumerator: entering, instance={0}", instance.InstanceName);
		CheckDisposed();
		return reservationTokens.GetEnumerator();
	}

	public void Dispose()
	{
		lock (this)
		{
			if (disposed)
			{
				return;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "Dispose: disposing (instance={0})...", instance.InstanceName);
			ProhibitConnectionAcquisition();
			PerfCounters.PossibleConns.Adjust(0L - (long)maxNumberOfConnections);
			foreach (ConnectionPoolEntry connection in connections)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "Dispose: disposing a ConnectionPoolEntry");
				connection.Dispose();
				PerfCounters.AllocConns.Decrement();
			}
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	private ConnectionPoolEntry GetFreeEntryAsCurrentUser(TimeSpan timeToWaitToAcquire, InitialEntryState state)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetFreeEntryAsCurrentUser: entering, instance={0}, wait={1}", instance.InstanceName, timeToWaitToAcquire.ToString());
		ConnectionPoolEntry connectionPoolEntry = null;
		ConnectionPoolEntry connectionPoolEntry2 = null;
		WindowsIdentity windowsIdentity = null;
		if (!connectionsAvailable.WaitOne(timeToWaitToAcquire, exitContext: false))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetFreeEntryAsCurrentUser: couldn't get connection (wait={0}, instance={1})", timeToWaitToAcquire.ToString(), instance.InstanceName);
			return null;
		}
		try
		{
			windowsIdentity = WindowsIdentity.GetCurrent();
			WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);
			lock (this)
			{
				if (!allowConnectionsToBeAcquired)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetFreeEntryAsCurrentUser: pool {0} is shutting down, exiting", instance.InstanceName);
					connectionsAvailable.Release();
					return null;
				}
				ConnectionPoolEntry connectionPoolEntry3 = null;
				ConnectionPoolEntry connectionPoolEntry4 = null;
				foreach (ConnectionPoolEntry connection in connections)
				{
					lock (connection)
					{
						if (connection.InUseCount != 0 || connection.IsReserved)
						{
							continue;
						}
						if (connection.Identity != null && connection.Identity.User.Equals(windowsIdentity.User))
						{
							WindowsPrincipal windowsPrincipal2 = new WindowsPrincipal(connection.Identity);
							if (windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator) == windowsPrincipal2.IsInRole(WindowsBuiltInRole.Administrator))
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetFreeEntryAsCurrentUser: got perfect match, instance={0}", instance.InstanceName);
								connectionPoolEntry3 = connection;
								break;
							}
							if (connectionPoolEntry4 == null)
							{
								GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetFreeEntryAsCurrentUser: got possible match, but need to (Re)Authenticate due to elevation privileges, instance={0}", instance.InstanceName);
								connectionPoolEntry4 = connection;
							}
						}
						else if (connectionPoolEntry4 == null)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetFreeEntryAsCurrentUser: got possible match, instance={0}, identity={1}", instance.InstanceName, (connection.Identity != null) ? connection.Identity.User.ToString() : "null");
							connectionPoolEntry4 = connection;
						}
					}
				}
				if (connectionPoolEntry3 != null)
				{
					connections.Remove(connectionPoolEntry3);
					connections.Add(connectionPoolEntry3);
					lock (connectionPoolEntry3)
					{
						switch (state)
						{
						case InitialEntryState.InUse:
							connectionPoolEntry3.InUseCount++;
							break;
						case InitialEntryState.Reserved:
							connectionPoolEntry3.IsReserved = true;
							break;
						}
						if (connectionPoolEntry3.IsBindTooOld() || connectionPoolEntry3.TooManyConsecutiveServerDowns())
						{
							connectionPoolEntry2 = connectionPoolEntry3;
						}
						else
						{
							connectionPoolEntry = connectionPoolEntry3;
						}
					}
				}
				else if (connectionPoolEntry4 != null)
				{
					connections.Remove(connectionPoolEntry4);
					connections.Add(connectionPoolEntry4);
					lock (connectionPoolEntry4)
					{
						switch (state)
						{
						case InitialEntryState.InUse:
							connectionPoolEntry4.InUseCount++;
							break;
						case InitialEntryState.Reserved:
							connectionPoolEntry4.IsReserved = true;
							break;
						}
						connectionPoolEntry2 = connectionPoolEntry4;
					}
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetFreeEntryAsCurrentUser: trying to grow pool (instance={0}, current={1}, max={2})", instance.InstanceName, connections.Count, maxNumberOfConnections);
					connectionPoolEntry2 = AddConnectionIfPossible(state);
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetFreeEntryAsCurrentUser: grew pool (instance={0}, current={1}, max={2})", instance.InstanceName, connections.Count, maxNumberOfConnections);
				}
			}
			if (connectionPoolEntry2 != null)
			{
				try
				{
					connectionPoolEntry2.AuthenticateAsCurrentUser();
				}
				catch (Exception)
				{
					lock (connectionPoolEntry2)
					{
						switch (state)
						{
						case InitialEntryState.InUse:
							connectionPoolEntry2.InUseCount--;
							break;
						case InitialEntryState.Reserved:
							connectionPoolEntry2.IsReserved = false;
							break;
						}
					}
					using (WindowsIdentity windowsIdentity2 = WindowsIdentity.GetCurrent())
					{
						ADWSEventlog.Log(3221226772L, instance.InstanceName, instance.Port, instance.SslPort, windowsIdentity2.User);
					}
					encounteredErrorOnPreviousBind = true;
					throw;
				}
				if (encounteredErrorOnPreviousBind)
				{
					using WindowsIdentity windowsIdentity3 = WindowsIdentity.GetCurrent();
					ADWSEventlog.Log(1073743126L, instance.InstanceName, instance.Port, instance.SslPort, windowsIdentity3.User);
				}
				encounteredErrorOnPreviousBind = false;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "GetFreeEntryAsCurrentUser: reauthenticated entry (instance={0})", instance.InstanceName);
				connectionPoolEntry = connectionPoolEntry2;
			}
			return connectionPoolEntry;
		}
		finally
		{
			if (connectionPoolEntry == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "GetFreeEntryAsCurrentUser: releasing semaphore on failure, instance={0}", instance.InstanceName);
				connectionsAvailable.Release();
			}
			windowsIdentity.Dispose();
		}
	}

	private ConnectionPoolEntry AddConnectionIfPossible(InitialEntryState state)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "AddConnectionIfPossible: entering, instance={0}", instance.InstanceName);
		ConnectionPoolEntry connectionPoolEntry2;
		lock (this)
		{
			ConnectionPoolEntry connectionPoolEntry = null;
			if (connections.Count < maxNumberOfConnections)
			{
				connectionPoolEntry = new ConnectionPoolEntry(this, instance);
				switch (state)
				{
				case InitialEntryState.InUse:
				{
					connectionPoolEntry2 = connectionPoolEntry;
					bool lockTaken = false;
					try
					{
						Monitor.Enter(connectionPoolEntry2, ref lockTaken);
						connectionPoolEntry.InUseCount++;
					}
					finally
					{
						if (lockTaken)
						{
							Monitor.Exit(connectionPoolEntry2);
						}
					}
					break;
				}
				case InitialEntryState.Reserved:
				{
					connectionPoolEntry2 = connectionPoolEntry;
					bool lockTaken = false;
					try
					{
						Monitor.Enter(connectionPoolEntry2, ref lockTaken);
						connectionPoolEntry.IsReserved = true;
					}
					finally
					{
						if (lockTaken)
						{
							Monitor.Exit(connectionPoolEntry2);
						}
					}
					break;
				}
				}
				connections.Add(connectionPoolEntry);
				PerfCounters.AllocConns.Increment();
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPool", "AddConnectionIfPossible: grew pool, instance={0}, new count={1}, max={2}", instance.InstanceName, connections.Count, maxNumberOfConnections);
				connectionPoolEntry2 = connectionPoolEntry;
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPool", "AddConnectionIfPossible: pool at limit, can't grow, instance={0}, max={1}", instance.InstanceName, maxNumberOfConnections);
				connectionPoolEntry2 = null;
			}
		}
		return connectionPoolEntry2;
	}
}

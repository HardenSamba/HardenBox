using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Security.Principal;
using System.Threading;

namespace Microsoft.ActiveDirectory.WebServices;

internal class LdapSessionPoolImplementation : ILdapSessionPool, IDisposable
{
	private bool disposed;

	private Dictionary<InstanceType, ConnectionPool> pools;

	private QuotaTracker<SecurityIdentifier> users;

	private Dictionary<LdapConnection, ConnectionPoolEntry> connectionsGivenOut;

	private Dictionary<InstanceType, UtilityConnectionImplementation> utilityConnections;

	private Thread scavengerThread;

	private ManualResetEvent terminateScavengerEvent;

	internal LdapSessionPoolImplementation()
	{
		users = new QuotaTracker<SecurityIdentifier>();
		pools = new Dictionary<InstanceType, ConnectionPool>();
		connectionsGivenOut = new Dictionary<LdapConnection, ConnectionPoolEntry>();
		utilityConnections = new Dictionary<InstanceType, UtilityConnectionImplementation>();
		terminateScavengerEvent = new ManualResetEvent(initialState: false);
		scavengerThread = new Thread(ScavengerThread);
		scavengerThread.IsBackground = true;
		scavengerThread.Name = "LDAP Session Pool - Scavenger";
		scavengerThread.Start(this);
	}

	public void InitializeInstance(InstanceType instance, uint initialConnectionCount, uint maxConnectionCount)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "InitializeInstance: entering, instance={0}, init={1}, max={2}", (instance != null && instance.InstanceName != null) ? instance.InstanceName : "null", initialConnectionCount, maxConnectionCount);
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (maxConnectionCount == 0 || initialConnectionCount > maxConnectionCount)
		{
			throw new ArgumentException(AdwsStrings.MaxUsersIsTooSmall);
		}
		lock (this)
		{
			if (pools.ContainsKey(instance))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "InitializeInstance: tried to add instance already present {0}", instance.InstanceName);
				throw new InvalidOperationException(AdwsStrings.PoolAlreadyExists);
			}
			LdapConnection ldapConnection = DirectoryUtilities.CreateLdapConnection(instance);
			ldapConnection.Bind();
			UtilityConnectionImplementation value = new UtilityConnectionImplementation(instance, ldapConnection);
			utilityConnections[instance] = value;
			ConnectionPool value2 = new ConnectionPool(instance, initialConnectionCount, maxConnectionCount);
			pools[instance] = value2;
		}
	}

	public bool IsInstanceKnown(InstanceType instance)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "IsInstanceKnown: entering, instance={0}", (instance != null && instance.InstanceName != null) ? instance.InstanceName : "null");
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		lock (this)
		{
			return pools.ContainsKey(instance);
		}
	}

	public void RemoveInstance(InstanceType instance)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "RemoveInstance: entering, instance={0}", (instance != null && instance.InstanceName != null) ? instance.InstanceName : "null");
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		UtilityConnectionImplementation utilityConnectionImplementation = null;
		ConnectionPool connectionPool;
		lock (this)
		{
			if (!pools.ContainsKey(instance))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "RemoveInstance: tried to remove instance not present {0}", instance.InstanceName);
				return;
			}
			connectionPool = pools[instance];
			connectionPool.ProhibitConnectionAcquisition();
			pools.Remove(instance);
			utilityConnectionImplementation = utilityConnections[instance];
			utilityConnections.Remove(instance);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "RemoveInstance: waiting on {0}", instance.InstanceName);
		connectionPool.WaitForAllConnectionsToBeFree();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "RemoveInstance: wait completed");
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "RemoveInstance: waiting on util connection for {0}", instance.InstanceName);
		if (utilityConnectionImplementation == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "LdapSessionPoolImplementation", "RemoveInstance: Unexpected State. utilWrapperToRemove == null");
			throw new InvalidOperationException();
		}
		uint inUseCount;
		lock (utilityConnectionImplementation)
		{
			inUseCount = utilityConnectionImplementation.InUseCount;
		}
		while (inUseCount != 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "RemoveInstance: continuing wait on util connection for {0}, count={1}", instance.InstanceName, utilityConnectionImplementation.InUseCount);
			Thread.Sleep(1000);
			lock (utilityConnectionImplementation)
			{
				inUseCount = utilityConnectionImplementation.InUseCount;
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "RemoveInstance: wait on util connection completed");
		utilityConnectionImplementation.LdapConn.Dispose();
		connectionPool.Dispose();
	}

	public LdapConnection GetConnectionAsCurrentUser(InstanceType instance, TimeSpan timeToWaitToAcquire)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "GetConnectionAsCurrentUser: entering, instance={0}, wait={1}", (instance != null && instance.InstanceName != null) ? instance.InstanceName : "null", timeToWaitToAcquire.ToString());
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		using WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
		SecurityIdentifier user = windowsIdentity.User;
		ConnectionPool connectionPool;
		lock (this)
		{
			if (!pools.ContainsKey(instance))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "GetConnectionAsCurrentUser: unknown instance {0}", instance.InstanceName);
				throw new ArgumentException(AdwsStrings.NoSuchPool);
			}
			connectionPool = pools[instance];
			if (!users.TryToIncrementCount(user, ConfigurationSettings.MaxConnectionsPerUser))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "GetConnectionAsCurrentUser: user reached limit");
				return null;
			}
		}
		ConnectionPoolEntry connectionPoolEntry = null;
		try
		{
			connectionPoolEntry = connectionPool.GetAvailableConnectionAsCurrentUser(timeToWaitToAcquire);
		}
		finally
		{
			if (connectionPoolEntry == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "GetConnectionAsCurrentUser: couldn't get connection, instance={0}, wait={1}", instance.InstanceName, timeToWaitToAcquire.ToString());
				users.DecrementCount(user);
			}
		}
		if (connectionPoolEntry != null)
		{
			lock (this)
			{
				connectionsGivenOut[connectionPoolEntry.Connection] = connectionPoolEntry;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "GetConnectionAsCurrentUser: got connection, instance={0}", instance.InstanceName);
			return connectionPoolEntry.Connection;
		}
		return null;
	}

	public void ReleaseConnection(LdapConnection ldapConn)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ReleaseConnection: entering");
		CheckDisposed();
		if (ldapConn == null)
		{
			throw new ArgumentNullException("ldapConn");
		}
		using WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
		SecurityIdentifier user = windowsIdentity.User;
		lock (this)
		{
			if (!connectionsGivenOut.ContainsKey(ldapConn))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ReleaseConnection: unknown connection");
				throw new ArgumentException(AdwsStrings.InvalidConnection);
			}
			ConnectionPoolEntry connectionPoolEntry = connectionsGivenOut[ldapConn];
			connectionPoolEntry.Pool.ReleaseConnection(connectionPoolEntry);
			users.DecrementCount(user);
			connectionsGivenOut.Remove(ldapConn);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ReleaseConnection: removed connection for instance {0}", connectionPoolEntry.Instance.InstanceName);
		}
	}

	public void NotifyConnectionDown(LdapConnection ldapConn)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "NotifyConnectionDown: entering");
		NotifyConnectionInternal(ldapConn, down: true);
	}

	public void NotifyConnectionUp(LdapConnection ldapConn)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "NotifyConnectionUp: entering");
		NotifyConnectionInternal(ldapConn, down: false);
	}

	private void NotifyConnectionInternal(LdapConnection ldapConn, bool down)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "NotifyConnectionInternal: entering, down={0}", down);
		CheckDisposed();
		if (ldapConn == null)
		{
			throw new ArgumentNullException("ldapConn");
		}
		lock (this)
		{
			if (!connectionsGivenOut.ContainsKey(ldapConn))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "NotifyConnectionInternal: unknown connection");
				throw new ArgumentException(AdwsStrings.InvalidConnection);
			}
			ConnectionPoolEntry connectionPoolEntry = connectionsGivenOut[ldapConn];
			if (down)
			{
				connectionPoolEntry.NotifyServerDown();
			}
			else
			{
				connectionPoolEntry.NotifyServerUp();
			}
		}
	}

	public ReservationToken ReserveConnectionAsCurrentUser(InstanceType instance, SessionPoolNotifyOfTimeout routineToNotify, TimeSpan timeToWaitToAcquire)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ReserveConnectionAsCurrentUser: entering, instance={0}, wait={1}", (instance != null && instance.InstanceName != null) ? instance.InstanceName : "null", timeToWaitToAcquire.ToString());
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		using WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
		SecurityIdentifier user = windowsIdentity.User;
		ConnectionPool connectionPool;
		lock (this)
		{
			if (!pools.ContainsKey(instance))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ReserveConnectionAsCurrentUser: unknown instance {0}", instance.InstanceName);
				throw new ArgumentException(AdwsStrings.NoSuchPool);
			}
			connectionPool = pools[instance];
			if (!users.TryToIncrementCount(user, ConfigurationSettings.MaxConnectionsPerUser))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ReserveConnectionAsCurrentUser: user reached limit");
				return null;
			}
		}
		ReservationToken reservationToken = null;
		try
		{
			reservationToken = connectionPool.AllocateReservedConnection(timeToWaitToAcquire, routineToNotify);
		}
		finally
		{
			if (reservationToken == null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ReserveConnectionAsCurrentUser: couldn't get reservation, instance={0}, wait={1}", instance.InstanceName, timeToWaitToAcquire.ToString());
				users.DecrementCount(user);
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ReserveConnectionAsCurrentUser: got reservation, instance={0}", instance.InstanceName);
		return reservationToken;
	}

	public LdapConnection GetReservedConnection(ReservationToken token)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "GetReservedConnection: entering, instance={0}", (token != null && token.Instance != null && token.Instance.InstanceName != null) ? token.Instance.InstanceName : "null");
		CheckDisposed();
		if (token == null)
		{
			throw new ArgumentNullException("token");
		}
		DateTime utcNow = DateTime.UtcNow;
		if (token.TimeReserved < utcNow && utcNow - token.TimeReserved > ConfigurationSettings.MaxReservedTimeout)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "GetReservedConnection: blocking because open since {0}, instance={1}", token.TimeReserved.ToString(), token.Instance.InstanceName);
			throw new InvalidReservationTokenException(AdwsStrings.InvalidReservationToken);
		}
		ConnectionPool pool = token.AssociatedConnectionPoolEntry.Pool;
		ConnectionPoolEntry connectionPoolEntry = null;
		connectionPoolEntry = pool.GetReservedConnection(token);
		if (connectionPoolEntry != null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "GetReservedConnection: got connection from token, instance={0}", token.Instance.InstanceName);
			return connectionPoolEntry.Connection;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "GetReservedConnection: returning null (pool shutdown?), instance={0}", token.Instance.InstanceName);
		return null;
	}

	public void ReleaseReservedConnection(ReservationToken token)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ReleaseReservedConnection: entering, instance={0}", (token != null && token.Instance != null && token.Instance.InstanceName != null) ? token.Instance.InstanceName : "null");
		CheckDisposed();
		if (token == null)
		{
			throw new ArgumentNullException("token");
		}
		ConnectionPoolEntry associatedConnectionPoolEntry = token.AssociatedConnectionPoolEntry;
		associatedConnectionPoolEntry.Pool.ReleaseReservedConnection(associatedConnectionPoolEntry);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "GetReservedConnection: released connection for token, instance={0}", token.Instance.InstanceName);
	}

	public void NotifyReservedConnectionDown(ReservationToken token)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "NotifyReservedConnectionDown: entering");
		NotifyReservedConnectionInternal(token, down: true);
	}

	public void NotifyReservedConnectionUp(ReservationToken token)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "NotifyReservedConnectionUp: entering");
		NotifyReservedConnectionInternal(token, down: false);
	}

	private void NotifyReservedConnectionInternal(ReservationToken token, bool down)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "NotifyReservedConnectionInternal: entering, instance={0}, down={1}", (token != null && token.Instance != null && token.Instance.InstanceName != null) ? token.Instance.InstanceName : "null", down);
		CheckDisposed();
		if (token == null)
		{
			throw new ArgumentNullException("token");
		}
		ConnectionPoolEntry associatedConnectionPoolEntry = token.AssociatedConnectionPoolEntry;
		if (down)
		{
			associatedConnectionPoolEntry.NotifyServerDown();
		}
		else
		{
			associatedConnectionPoolEntry.NotifyServerUp();
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "NotifyReservedConnectionInternal: notified connection");
	}

	public void ReleaseReservation(ReservationToken token)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ReleaseReservation: entering, instance={0}", (token != null && token.Instance != null && token.Instance.InstanceName != null) ? token.Instance.InstanceName : "null");
		CheckDisposed();
		if (token == null)
		{
			throw new ArgumentNullException("token");
		}
		ConnectionPool pool = token.AssociatedConnectionPoolEntry.Pool;
		SecurityIdentifier reservingUser = token.ReservingUser;
		lock (this)
		{
			pool.FreeReservedConnection(token);
			users.DecrementCount(reservingUser);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ReleaseReservation: released reservation, instance={0}", token.Instance.InstanceName);
		}
	}

	public UtilityConnection GetUtilityConnection(InstanceType instance)
	{
		CheckDisposed();
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		lock (this)
		{
			if (!utilityConnections.ContainsKey(instance))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "GetUtilityConnection: unknown instance {0}", instance.InstanceName);
				throw new ArgumentException(AdwsStrings.NoSuchPool);
			}
			UtilityConnectionImplementation utilityConnectionImplementation = utilityConnections[instance];
			lock (utilityConnectionImplementation)
			{
				utilityConnectionImplementation.InUseCount++;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "GetUtilityConnection: acquired util connection for instance {0}, new count {1}", instance.InstanceName, utilityConnectionImplementation.InUseCount);
				return utilityConnectionImplementation;
			}
		}
	}

	public void ReleaseUtilityConnection(UtilityConnection utilConnection)
	{
		CheckDisposed();
		if (utilConnection == null)
		{
			throw new ArgumentNullException("utilConnection");
		}
		UtilityConnectionImplementation utilityConnectionImplementation = utilConnection as UtilityConnectionImplementation;
		lock (this)
		{
			lock (utilityConnectionImplementation)
			{
				if (utilityConnectionImplementation.InUseCount == 0)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "LdapSessionPoolImplementation", "ReleaseUtilityConnection: trying to release instance {0} below zero", utilityConnectionImplementation.Instance.InstanceName);
					throw new InvalidOperationException(AdwsStrings.TriedToReleaseUtilConnectionTooManyTimes);
				}
				utilityConnectionImplementation.InUseCount--;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ReleaseUtilityConnection: released util connection for instance {0}, new count {1}", utilityConnectionImplementation.Instance.InstanceName, utilityConnectionImplementation.InUseCount);
			}
		}
	}

	public void Dispose()
	{
		lock (this)
		{
			if (disposed)
			{
				return;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "Dispose: disposing...");
			terminateScavengerEvent.Set();
			if (!scavengerThread.Join(new TimeSpan(0, 0, 30)))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "LdapSessionPoolImplementation", "Dispose: couldn't shutdown scavenger");
			}
			foreach (ConnectionPool value in pools.Values)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "Dispose: disposing pool");
				value.Dispose();
			}
			foreach (UtilityConnectionImplementation value2 in utilityConnections.Values)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "Dispose: disposing utility connection {0}", value2.Instance.InstanceName);
				value2.LdapConn.Dispose();
			}
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	public static void ScavengerThread(object o)
	{
		LdapSessionPoolImplementation ldapSessionPoolImplementation = o as LdapSessionPoolImplementation;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ScavengerThread: thread starting");
		List<ReservationToken> list = new List<ReservationToken>();
		List<ReservationToken> list2 = new List<ReservationToken>();
		do
		{
			TimeSpan maxReservedIdleTimeout = ConfigurationSettings.MaxReservedIdleTimeout;
			TimeSpan maxReservedTimeout = ConfigurationSettings.MaxReservedTimeout;
			TimeSpan timeout = new TimeSpan(((maxReservedIdleTimeout < maxReservedTimeout) ? maxReservedIdleTimeout : maxReservedTimeout).Ticks / 3);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "Scavenger: waking up at {0} interval", timeout.ToString());
			bool num = ldapSessionPoolImplementation.terminateScavengerEvent.WaitOne(timeout, exitContext: false);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ScavengerThread: woke up");
			if (num)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ScavengerThread: received termination signal, exiting");
				break;
			}
			if (ldapSessionPoolImplementation.disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ScavengerThread: pool disposed 1, exiting");
				break;
			}
			maxReservedIdleTimeout = ConfigurationSettings.MaxReservedIdleTimeout;
			maxReservedTimeout = ConfigurationSettings.MaxReservedTimeout;
			lock (ldapSessionPoolImplementation)
			{
				if (ldapSessionPoolImplementation.disposed)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ScavengerThread: pool disposed 2, exiting");
					break;
				}
				foreach (KeyValuePair<InstanceType, ConnectionPool> pool in ldapSessionPoolImplementation.pools)
				{
					ConnectionPool value = pool.Value;
					lock (value)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ScavengerThread: processing next pool");
						List<ReservationToken> list3 = new List<ReservationToken>();
						IEnumerator<ReservationToken> reservationEnumerator = value.GetReservationEnumerator();
						while (reservationEnumerator.MoveNext())
						{
							ReservationToken current = reservationEnumerator.Current;
							lock (current)
							{
								ConnectionPoolEntry associatedConnectionPoolEntry = current.AssociatedConnectionPoolEntry;
								lock (associatedConnectionPoolEntry)
								{
									GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionPoolImplementation", "ScavengerThread: processing entry for instance {0}", associatedConnectionPoolEntry.Instance.InstanceName);
									if (!associatedConnectionPoolEntry.IsReserved)
									{
										continue;
									}
									DateTime utcNow = DateTime.UtcNow;
									if (associatedConnectionPoolEntry.InUseCount == 0)
									{
										if (current.LastUsedTime < utcNow && utcNow - current.LastUsedTime > maxReservedIdleTimeout)
										{
											list3.Add(current);
											list.Add(current);
											GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ScavengerThread: case 2a, idle since {0}, instance {1}, releasing it", current.LastUsedTime.ToString(), associatedConnectionPoolEntry.Instance.InstanceName);
										}
										else if (current.TimeReserved < utcNow && utcNow - current.TimeReserved > maxReservedTimeout)
										{
											list3.Add(current);
											list2.Add(current);
											GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ScavengerThread: case 2b, open since {0}, instance {1}, releasing it", current.TimeReserved.ToString(), associatedConnectionPoolEntry.Instance.InstanceName);
										}
									}
									else if (current.TimeReserved < utcNow && utcNow - current.TimeReserved > maxReservedTimeout)
									{
										GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ScavengerThread: case 3, open since {0}, instance {1}", current.TimeReserved.ToString(), associatedConnectionPoolEntry.Instance.InstanceName);
									}
								}
							}
						}
						foreach (ReservationToken item in list3)
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ScavengerThread: going to release token for instance {0}", item.Instance.InstanceName);
							ldapSessionPoolImplementation.ReleaseReservation(item);
						}
						list3.Clear();
					}
				}
			}
			foreach (ReservationToken item2 in list)
			{
				if (item2.RoutineToNotify != null)
				{
					try
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ScavengerThread: notifying idle token for instance {0}", item2.Instance.InstanceName);
						item2.RoutineToNotify(item2, wasIdle: true);
					}
					catch (Exception ex)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Error, "LdapSessionPoolImplementation", "ScavengerThread: caught a idle notification exception {0}, instance {1}", ex.ToString(), item2.Instance.InstanceName);
						throw;
					}
				}
			}
			foreach (ReservationToken item3 in list2)
			{
				if (item3.RoutineToNotify != null)
				{
					try
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSessionPoolImplementation", "ScavengerThread: notifying open token for instance {0}", item3.Instance.InstanceName);
						item3.RoutineToNotify(item3, wasIdle: false);
					}
					catch (Exception ex2)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Error, "LdapSessionPoolImplementation", "ScavengerThread: caught a open notification exception {0}, instance {1}", ex2.ToString(), item3.Instance.InstanceName);
						throw;
					}
				}
			}
			list.Clear();
			list2.Clear();
		}
		while (!ldapSessionPoolImplementation.disposed);
	}
}

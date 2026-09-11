using System;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Security.Principal;

namespace Microsoft.ActiveDirectory.WebServices;

internal class ConnectionPoolEntry : IDisposable
{
	private InstanceType instance;

	private ConnectionPool pool;

	private LdapConnection connection;

	private WindowsIdentity identity;

	private uint inUseCount;

	private bool isReserved;

	private bool disposed;

	private DateTime lastBindTime = DateTime.MinValue;

	private uint consecutiveServerDowns;

	internal InstanceType Instance
	{
		get
		{
			CheckDisposed();
			return instance;
		}
	}

	internal ConnectionPool Pool
	{
		get
		{
			CheckDisposed();
			return pool;
		}
	}

	internal LdapConnection Connection
	{
		get
		{
			CheckDisposed();
			return connection;
		}
	}

	internal WindowsIdentity Identity
	{
		get
		{
			CheckDisposed();
			return identity;
		}
	}

	internal uint InUseCount
	{
		get
		{
			CheckDisposed();
			return inUseCount;
		}
		set
		{
			CheckDisposed();
			inUseCount = value;
		}
	}

	internal bool IsReserved
	{
		get
		{
			CheckDisposed();
			return isReserved;
		}
		set
		{
			CheckDisposed();
			isReserved = value;
		}
	}

	internal ConnectionPoolEntry(ConnectionPool pool, InstanceType instance)
	{
		this.instance = instance;
		this.pool = pool;
		connection = null;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "ConnectionPoolEntry: connection created");
		identity = null;
	}

	private void SetConnectionIdentity()
	{
		lock (this)
		{
			if (identity != null)
			{
				identity.Dispose();
				identity = null;
			}
			identity = WindowsIdentity.GetCurrent();
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "SetConnectionIdentity: new identity is {0}", identity.User.ToString());
		}
	}

	internal bool IsBindTooOld()
	{
		CheckDisposed();
		lock (this)
		{
			TimeSpan timeSpan = DateTime.UtcNow - lastBindTime;
			if (timeSpan > ConfigurationSettings.MaxBindLifetime)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "IsBindTooOld: bind too old ({0} minutes), forcing rebind", timeSpan.TotalMinutes);
				return true;
			}
			return false;
		}
	}

	internal bool TooManyConsecutiveServerDowns()
	{
		CheckDisposed();
		lock (this)
		{
			return consecutiveServerDowns > ConfigurationSettings.MaxServerDownRetry;
		}
	}

	internal void NotifyServerDown()
	{
		CheckDisposed();
		lock (this)
		{
			consecutiveServerDowns++;
		}
	}

	internal void NotifyServerUp()
	{
		CheckDisposed();
		lock (this)
		{
			consecutiveServerDowns = 0u;
		}
	}

	internal void AuthenticateAsCurrentUser()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "AuthenticateAsCurrentUser: entering");
		CheckDisposed();
		lock (this)
		{
			bool flag = true;
			while (true)
			{
				if (TooManyConsecutiveServerDowns())
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "AuthenticateAsCurrentUser: recreating connection because of too many server down errors ({0} occurred, {1} allowed)", consecutiveServerDowns, ConfigurationSettings.MaxServerDownRetry);
					if (connection != null)
					{
						connection.Dispose();
						connection = null;
						identity = null;
					}
					consecutiveServerDowns = 0u;
				}
				if (identity == null && connection == null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "AuthenticateAsCurrentUser: creating connection");
					connection = DirectoryUtilities.CreateLdapConnection(instance);
				}
				else if (connection == null)
				{
					throw new NoConnectionAvailableException();
				}
				try
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "AuthenticateAsCurrentUser: Binding");
					connection.Bind();
				}
				catch (Exception ex)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "AuthenticateAsCurrentUser: bind failed, nulling out identity (was {0})", (identity != null) ? identity.User.ToString() : "NULL");
					identity = null;
					if (flag && ex is DirectoryException)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "AuthenticateAsCurrentUser: retrying bind (error was {0})", ex.ToString());
						flag = false;
						connection.Dispose();
						connection = null;
						continue;
					}
					throw;
				}
				break;
			}
			if (IsBoundAnonymously())
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "AuthenticateAsCurrentUser: connection was bound as anonymous, reject");
				identity = null;
				using (WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent())
				{
					ADWSEventlog.Log(2147484949L, instance.InstanceName, instance.Port, instance.SslPort, windowsIdentity.User);
				}
				throw new LdapException(48, AdwsStrings.AnonymousNotAllowed);
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "AuthenticateAsCurrentUser: connection (re)bound successfully");
			SetConnectionIdentity();
			lastBindTime = DateTime.UtcNow;
		}
	}

	public void Dispose()
	{
		lock (this)
		{
			if (!disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "Dispose: disposing...");
				if (connection != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "Dispose: disposing the connection");
					connection.Dispose();
				}
				if (identity != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "Dispose: disposing the identity");
					identity.Dispose();
				}
				disposed = true;
			}
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	private bool IsBoundAnonymously()
	{
		bool flag = false;
		SearchRequest request = new SearchRequest(null, "(objectClass=*)", SearchScope.Base, "msds-principalName");
		try
		{
			SearchResponse searchResponse = (SearchResponse)connection.SendRequest(request, ConfigurationSettings.OperationTimeout);
			if (searchResponse.ResultCode == ResultCode.Success)
			{
				if (searchResponse.Entries.Count == 1)
				{
					string singleValuedAttrVal = DirectoryUtilities.GetSingleValuedAttrVal<string>(searchResponse.Entries[0], "msds-principalName");
					if (singleValuedAttrVal != null && singleValuedAttrVal.Length > 0)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "IsBoundAnonymously: bound as {0}", singleValuedAttrVal);
						if (string.Compare(singleValuedAttrVal, "NT AUTHORITY\\ANONYMOUS LOGON", ignoreCase: true, CultureInfo.InvariantCulture) == 0 || string.Compare(singleValuedAttrVal, "S-1-5-7", ignoreCase: true, CultureInfo.InvariantCulture) == 0)
						{
							flag = true;
						}
					}
					else
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "IsBoundAnonymously: didn't get value for attribute");
					}
				}
				else
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "IsBoundAnonymously: didn't get rootDse");
				}
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "IsBoundAnonymously: search returned error {0}", searchResponse.ResultCode);
			}
		}
		catch (DirectoryException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConnectionPoolEntry", "IsBoundAnonymously: got error {0}", ex.ToString());
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConnectionPoolEntry", "IsBoundAnonymously: returning {0}", flag);
		return flag;
	}
}

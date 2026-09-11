using System;

namespace Microsoft.ActiveDirectory.WebServices;

internal class LdapSession : IDisposable
{
	private InstanceType instance;

	private ReservationToken token;

	private bool valid = true;

	private bool disposed;

	internal InstanceType Instance
	{
		get
		{
			CheckDisposed();
			return instance;
		}
	}

	internal ReservationToken Token
	{
		get
		{
			CheckDisposed();
			return token;
		}
	}

	internal LdapSession(InstanceType instance, ReservationToken token)
	{
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		if (token == null)
		{
			throw new ArgumentNullException("token");
		}
		this.instance = instance;
		this.token = token;
	}

	internal void InvalidateSession()
	{
		CheckDisposed();
		lock (this)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSession", "InvalidateSession: invalidating the session (validity was {0})", valid.ToString());
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
		if (!disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSession", "Dispose: disposing...");
			if (valid)
			{
				ClassManager.DirectoryDataAccess.ReleaseLdapSession(this);
			}
			token.Dispose();
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapSession", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}
}

using System;
using System.DirectoryServices.Protocols;

namespace Microsoft.ActiveDirectory.WebServices;

internal class LdapQuery : Query, IDisposable
{
	private QueryFilter queryFilter;

	private LdapSession session;

	private LdapQueryMode mode;

	private PageResultResponseControl pagedResponse;

	private DirectoryActionNotifyOfConnectionTimeout routineToNotify;

	public override QueryFilter QueryFilter
	{
		get
		{
			CheckDisposed();
			return queryFilter;
		}
		set
		{
			CheckDisposed();
			queryFilter = value as LdapQueryFilter;
			if (queryFilter == null)
			{
				throw new ArgumentException(AdwsStrings.NotCorrectFilterType);
			}
		}
	}

	internal LdapSession Session
	{
		get
		{
			CheckDisposed();
			return session;
		}
		set
		{
			CheckDisposed();
			session = value;
		}
	}

	internal LdapQueryMode Mode
	{
		get
		{
			CheckDisposed();
			return mode;
		}
		set
		{
			CheckDisposed();
			mode = value;
		}
	}

	internal PageResultResponseControl PagedResponse
	{
		get
		{
			CheckDisposed();
			return pagedResponse;
		}
		set
		{
			CheckDisposed();
			pagedResponse = value;
		}
	}

	internal DirectoryActionNotifyOfConnectionTimeout RoutineToNotify
	{
		get
		{
			return routineToNotify;
		}
		set
		{
			routineToNotify = value;
		}
	}

	internal LdapQuery()
	{
		queryFilter = null;
		session = null;
		mode = LdapQueryMode.Unprepared;
		pagedResponse = null;
	}

	public override bool NoMoreResults()
	{
		CheckDisposed();
		bool result = false;
		if (pagedResponse != null && pagedResponse.Cookie.Length == 0)
		{
			result = true;
		}
		return result;
	}

	public override void Dispose()
	{
		lock (this)
		{
			if (!disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapQuery", "Dispose: disposing...");
				base.Dispose();
				if (session != null)
				{
					session.Dispose();
				}
			}
		}
	}

	internal void DataAccessTimedOutCallback(LdapSession ldapSession, bool wasIdle)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapQuery", "DataAccessTimedOutCallback: got called (wasIdle = {0})", wasIdle.ToString());
		if (routineToNotify != null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapQuery", "DataAccessTimedOutCallback: calling handler");
			routineToNotify(this, wasIdle);
		}
	}
}

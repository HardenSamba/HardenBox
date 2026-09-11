using System;
using System.DirectoryServices.Protocols;
using System.Globalization;

namespace Microsoft.ActiveDirectory.WebServices;

internal class LdapQueryFilter : QueryFilter
{
	private string ldapFilter;

	private SearchScope scope;

	private Guid queryBaseObjGuid;

	private string queryBaseObjDn;

	internal string LdapFilter
	{
		get
		{
			return ldapFilter;
		}
		private set
		{
			ldapFilter = value;
		}
	}

	internal SearchScope Scope
	{
		get
		{
			return scope;
		}
		private set
		{
			scope = value;
		}
	}

	internal Guid QueryBaseObjGuid
	{
		get
		{
			return queryBaseObjGuid;
		}
		private set
		{
			queryBaseObjGuid = value;
		}
	}

	internal string QueryBaseObjDn
	{
		get
		{
			return queryBaseObjDn;
		}
		set
		{
			queryBaseObjDn = value;
		}
	}

	internal LdapQueryFilter(string filter, SearchScope scope, Guid baseObjectGuid)
		: this(filter, scope)
	{
		queryBaseObjGuid = baseObjectGuid;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapQueryFilter", "Constructor: filter={0} scope={1} baseObjGuid={2}", filter, scope, baseObjectGuid);
	}

	internal LdapQueryFilter(string filter, SearchScope scope, string baseObjectDn)
		: this(filter, scope)
	{
		if (baseObjectDn == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapQueryFilter", "null base DN");
			throw new ArgumentNullException("baseObjectDn");
		}
		queryBaseObjDn = baseObjectDn;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapQueryFilter", "Constructor: filter={0} scope={1} baseObjDn={2}", filter, scope, baseObjectDn);
	}

	private LdapQueryFilter(string filter, SearchScope scope)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapQueryFilter", "constructor entered");
		if (filter == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapQueryFilter", "null filter");
			throw new ArgumentNullException("filter");
		}
		ldapFilter = filter;
		this.scope = scope;
		queryBaseObjGuid = Guid.Empty;
		queryBaseObjDn = null;
	}

	public override string ToString()
	{
		return string.Format(CultureInfo.InvariantCulture, "({0},{1},{2},{3})", scope, (ldapFilter != null) ? ldapFilter : "NULL", (queryBaseObjDn != null) ? queryBaseObjDn : "NULL", (queryBaseObjGuid != Guid.Empty) ? queryBaseObjGuid.ToString() : "-");
	}
}

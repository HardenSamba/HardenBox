using System;
using System.DirectoryServices.Protocols;

namespace Microsoft.ActiveDirectory.WebServices;

internal class LdapAdaptor
{
	private LdapQueryFilter ldapQueryFilter;

	internal LdapAdaptor(QueryFilter filter)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapAdaptor", "Constructor entered");
		if (filter == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapAdaptor", "null filter");
			throw new ArgumentNullException("filter");
		}
		ldapQueryFilter = filter as LdapQueryFilter;
		if (filter == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapAdaptor", "incorrect filter type");
			throw new ArgumentException(AdwsStrings.NotCorrectFilterType);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Warn, "LdapAdaptor", "Constructor complete");
	}

	internal string GetLdapFilter()
	{
		return ldapQueryFilter.LdapFilter;
	}

	internal SearchScope GetScope()
	{
		return ldapQueryFilter.Scope;
	}

	internal string GetDnForRootOfSearch()
	{
		if (!ldapQueryFilter.QueryBaseObjGuid.Equals(Guid.Empty) || ldapQueryFilter.QueryBaseObjDn == null)
		{
			return DirectoryUtilities.MakeGuidDn(ldapQueryFilter.QueryBaseObjGuid);
		}
		return ldapQueryFilter.QueryBaseObjDn;
	}
}

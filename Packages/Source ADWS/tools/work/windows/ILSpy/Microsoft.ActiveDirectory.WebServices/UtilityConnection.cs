using System.DirectoryServices.Protocols;

namespace Microsoft.ActiveDirectory.WebServices;

internal abstract class UtilityConnection
{
	protected LdapConnection ldapConn;

	internal LdapConnection LdapConn => ldapConn;
}

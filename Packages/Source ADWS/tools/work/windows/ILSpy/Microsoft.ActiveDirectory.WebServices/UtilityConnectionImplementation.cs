using System.DirectoryServices.Protocols;

namespace Microsoft.ActiveDirectory.WebServices;

internal class UtilityConnectionImplementation : UtilityConnection
{
	private uint inUseCount;

	private InstanceType instance;

	internal uint InUseCount
	{
		get
		{
			return inUseCount;
		}
		set
		{
			inUseCount = value;
		}
	}

	internal InstanceType Instance => instance;

	internal UtilityConnectionImplementation(InstanceType instance, LdapConnection ldapConn)
	{
		this.instance = instance;
		base.ldapConn = ldapConn;
	}
}

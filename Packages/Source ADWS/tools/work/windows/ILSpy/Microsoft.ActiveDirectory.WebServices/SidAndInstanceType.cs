using System.Security.Principal;

namespace Microsoft.ActiveDirectory.WebServices;

internal class SidAndInstanceType
{
	private SecurityIdentifier userSid;

	private InstanceType instanceType;

	internal SidAndInstanceType(SecurityIdentifier userSid, InstanceType instanceType)
	{
		this.userSid = userSid;
		this.instanceType = instanceType;
	}

	public override bool Equals(object o)
	{
		if (!(o is SidAndInstanceType sidAndInstanceType))
		{
			return false;
		}
		if (instanceType.Equals(sidAndInstanceType.instanceType) && userSid.Equals(sidAndInstanceType.userSid))
		{
			return true;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return instanceType.GetHashCode() ^ userSid.GetHashCode();
	}

	public override string ToString()
	{
		return "<InstanceType>" + instanceType.ToString() + "</InstanceType><UserSid>" + userSid.ToString() + "</UserSid>";
	}
}

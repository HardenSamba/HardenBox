using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADPrincipalGroupMembershipResponse
{
	[MessageBodyMember]
	public ActiveDirectoryGroup[] MemberOf;
}

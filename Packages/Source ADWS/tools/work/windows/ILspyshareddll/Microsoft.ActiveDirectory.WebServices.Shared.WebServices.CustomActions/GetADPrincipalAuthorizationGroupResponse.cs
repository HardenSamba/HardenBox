using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADPrincipalAuthorizationGroupResponse
{
	[MessageBodyMember]
	public ActiveDirectoryGroup[] MemberOf;
}

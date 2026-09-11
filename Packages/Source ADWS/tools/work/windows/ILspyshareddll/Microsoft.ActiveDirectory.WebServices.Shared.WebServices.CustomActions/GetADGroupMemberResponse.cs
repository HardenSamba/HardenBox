using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADGroupMemberResponse
{
	[MessageBodyMember]
	public ActiveDirectoryPrincipal[] Members;
}

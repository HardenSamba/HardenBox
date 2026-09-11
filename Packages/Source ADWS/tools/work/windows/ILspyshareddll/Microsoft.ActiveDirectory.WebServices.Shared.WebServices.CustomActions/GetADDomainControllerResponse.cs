using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADDomainControllerResponse
{
	[MessageBodyMember]
	public ActiveDirectoryDomainController[] DomainControllers;
}

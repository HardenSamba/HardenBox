using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADDomainResponse
{
	[MessageBodyMember]
	public ActiveDirectoryDomain Domain;
}

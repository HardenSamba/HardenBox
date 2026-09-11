using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADDomainRequest
{
	[MessageHeader(MustUnderstand = true)]
	public string Server;
}

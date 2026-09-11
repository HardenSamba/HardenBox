using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADForestRequest
{
	[MessageHeader(MustUnderstand = true)]
	public string Server;
}

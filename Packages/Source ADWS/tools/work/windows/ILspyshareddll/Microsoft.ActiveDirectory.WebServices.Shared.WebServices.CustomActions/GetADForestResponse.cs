using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADForestResponse
{
	[MessageBodyMember]
	public ActiveDirectoryForest Forest;
}

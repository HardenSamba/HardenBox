using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetVersionResponse
{
	[MessageBodyMember]
	public int VersionMajor;

	[MessageBodyMember]
	public int VersionMinor;

	[MessageBodyMember]
	public string VersionString;
}

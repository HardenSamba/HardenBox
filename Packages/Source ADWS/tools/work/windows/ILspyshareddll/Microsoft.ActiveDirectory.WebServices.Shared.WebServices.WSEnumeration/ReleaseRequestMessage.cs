using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class ReleaseRequestMessage
{
	private Release releaseField;

	[MessageBodyMember(Name = "Release", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public Release Release
	{
		get
		{
			return releaseField;
		}
		set
		{
			releaseField = value;
		}
	}

	public ReleaseRequestMessage()
	{
		releaseField = new Release();
	}
}

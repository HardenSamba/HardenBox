using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class PullResponseMessage
{
	private PullResponse pullResponseField;

	[MessageBodyMember(Name = "PullResponse", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public PullResponse PullResponse
	{
		get
		{
			return pullResponseField;
		}
		set
		{
			pullResponseField = value;
		}
	}

	public PullResponseMessage()
	{
		pullResponseField = new PullResponse();
	}
}

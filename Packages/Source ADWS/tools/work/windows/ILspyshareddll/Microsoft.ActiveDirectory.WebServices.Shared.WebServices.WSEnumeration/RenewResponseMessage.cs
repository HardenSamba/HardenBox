using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class RenewResponseMessage
{
	private RenewResponse renewResponseField;

	[MessageBodyMember(Name = "RenewResponse", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public RenewResponse RenewResponse
	{
		get
		{
			return renewResponseField;
		}
		set
		{
			renewResponseField = value;
		}
	}

	public RenewResponseMessage()
	{
		renewResponseField = new RenewResponse();
	}
}

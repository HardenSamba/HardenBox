using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class RenewRequestMessage
{
	private Renew renewField;

	[MessageBodyMember(Name = "Renew", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public Renew Renew
	{
		get
		{
			return renewField;
		}
		set
		{
			renewField = value;
		}
	}

	public RenewRequestMessage()
	{
		renewField = new Renew();
	}
}

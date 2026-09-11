using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class EnumerateResponseMessage
{
	private EnumerateResponse enumerateResponseField;

	[MessageBodyMember(Name = "EnumerateResponse", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public EnumerateResponse EnumerateResponse
	{
		get
		{
			return enumerateResponseField;
		}
		set
		{
			enumerateResponseField = value;
		}
	}

	public EnumerateResponseMessage()
	{
		enumerateResponseField = new EnumerateResponse();
	}
}

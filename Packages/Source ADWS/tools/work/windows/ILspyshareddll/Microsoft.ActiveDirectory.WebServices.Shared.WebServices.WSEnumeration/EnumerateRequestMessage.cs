using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class EnumerateRequestMessage
{
	private Enumerate enumerateField;

	[MessageBodyMember(Name = "Enumerate", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public Enumerate Enumerate
	{
		get
		{
			return enumerateField;
		}
		set
		{
			enumerateField = value;
		}
	}

	public EnumerateRequestMessage()
	{
		enumerateField = new Enumerate();
	}
}

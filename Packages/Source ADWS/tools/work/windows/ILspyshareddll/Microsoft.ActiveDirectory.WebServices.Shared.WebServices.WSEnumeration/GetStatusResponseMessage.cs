using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class GetStatusResponseMessage
{
	private GetStatusResponse getStatusResponseField;

	[MessageBodyMember(Name = "GetStatusResponse", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public GetStatusResponse GetStatusResponse
	{
		get
		{
			return getStatusResponseField;
		}
		set
		{
			getStatusResponseField = value;
		}
	}

	public GetStatusResponseMessage()
	{
		getStatusResponseField = new GetStatusResponse();
	}
}

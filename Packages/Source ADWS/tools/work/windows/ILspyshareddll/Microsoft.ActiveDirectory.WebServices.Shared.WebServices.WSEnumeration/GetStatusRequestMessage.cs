using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class GetStatusRequestMessage
{
	private GetStatus getStatusField;

	[MessageBodyMember(Name = "GetStatus", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public GetStatus GetStatus
	{
		get
		{
			return getStatusField;
		}
		set
		{
			getStatusField = value;
		}
	}

	public GetStatusRequestMessage()
	{
		getStatusField = new GetStatus();
	}
}

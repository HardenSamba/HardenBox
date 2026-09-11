using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[MessageContract(IsWrapped = false)]
public class PullRequestMessage
{
	private Pull pullField;

	[MessageBodyMember(Name = "Pull", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public Pull Pull
	{
		get
		{
			return pullField;
		}
		set
		{
			pullField = value;
		}
	}

	public PullRequestMessage()
	{
		pullField = new Pull();
	}
}

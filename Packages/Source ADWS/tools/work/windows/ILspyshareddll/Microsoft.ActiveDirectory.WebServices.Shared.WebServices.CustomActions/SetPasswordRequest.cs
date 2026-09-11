using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class SetPasswordRequest
{
	[MessageHeader(MustUnderstand = true)]
	public string Server;

	[MessageBodyMember]
	public string PartitionDN;

	[MessageBodyMember]
	public string AccountDN;

	[MessageBodyMember]
	public string NewPassword;
}

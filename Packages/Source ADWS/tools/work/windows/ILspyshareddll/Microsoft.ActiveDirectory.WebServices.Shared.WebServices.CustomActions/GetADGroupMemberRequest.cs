using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADGroupMemberRequest
{
	[MessageHeader(MustUnderstand = true)]
	public string Server;

	[MessageBodyMember]
	public string PartitionDN;

	[MessageBodyMember]
	public string GroupDN;

	[MessageBodyMember]
	public bool Recursive;
}

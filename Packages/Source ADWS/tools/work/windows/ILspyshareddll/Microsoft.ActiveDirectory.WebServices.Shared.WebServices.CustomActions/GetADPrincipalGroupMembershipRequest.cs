using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class GetADPrincipalGroupMembershipRequest
{
	[MessageHeader(MustUnderstand = true)]
	public string Server;

	[MessageBodyMember]
	public string PartitionDN;

	[MessageBodyMember]
	public string PrincipalDN;

	[MessageBodyMember]
	public string ResourceContextServer;

	[MessageBodyMember]
	public string ResourceContextPartition;
}

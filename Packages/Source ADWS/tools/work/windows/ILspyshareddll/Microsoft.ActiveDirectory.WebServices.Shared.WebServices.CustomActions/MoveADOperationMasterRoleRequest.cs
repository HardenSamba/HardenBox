using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class MoveADOperationMasterRoleRequest
{
	[MessageHeader(MustUnderstand = true)]
	public string Server;

	[MessageBodyMember]
	public ActiveDirectoryOperationMasterRole OperationMasterRole;

	[MessageBodyMember]
	public bool Seize;
}

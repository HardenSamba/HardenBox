using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class MoveADOperationMasterRoleResponse
{
	[MessageBodyMember]
	public bool WasSeized;
}

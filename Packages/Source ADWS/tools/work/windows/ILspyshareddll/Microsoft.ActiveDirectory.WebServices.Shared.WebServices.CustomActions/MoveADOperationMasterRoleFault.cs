using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class MoveADOperationMasterRoleFault : CustomActionFault
{
	[IgnoreDataMember]
	public override string FaultName => "MoveADOperationMasterRoleFault";

	public MoveADOperationMasterRoleFault()
	{
	}

	public MoveADOperationMasterRoleFault(ArgumentErrorDetailCA argErr, string strErr, DirectoryErrorDetailCA dirErr)
		: base(argErr, strErr, dirErr)
	{
	}
}

using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class SetPasswordFault : CustomActionFault
{
	[IgnoreDataMember]
	public override string FaultName => "SetPasswordFault";

	public SetPasswordFault()
	{
	}

	public SetPasswordFault(ArgumentErrorDetailCA argErr, string strErr, DirectoryErrorDetailCA dirErr)
		: base(argErr, strErr, dirErr)
	{
	}
}

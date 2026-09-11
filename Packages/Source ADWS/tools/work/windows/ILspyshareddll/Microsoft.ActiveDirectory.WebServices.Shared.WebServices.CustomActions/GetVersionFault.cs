using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class GetVersionFault : CustomActionFault
{
	[IgnoreDataMember]
	public override string FaultName => "GetVersionFault";

	public GetVersionFault()
	{
	}

	public GetVersionFault(ArgumentErrorDetailCA argErr, string strErr, DirectoryErrorDetailCA dirErr)
		: base(argErr, strErr, dirErr)
	{
	}
}

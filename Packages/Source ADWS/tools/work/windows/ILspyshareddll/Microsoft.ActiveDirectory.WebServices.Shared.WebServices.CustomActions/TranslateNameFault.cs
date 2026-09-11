using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class TranslateNameFault : CustomActionFault
{
	[IgnoreDataMember]
	public override string FaultName => "TranslateNameFault";

	public TranslateNameFault()
	{
	}

	public TranslateNameFault(ArgumentErrorDetailCA argErr, string strErr, DirectoryErrorDetailCA dirErr)
		: base(argErr, strErr, dirErr)
	{
	}
}

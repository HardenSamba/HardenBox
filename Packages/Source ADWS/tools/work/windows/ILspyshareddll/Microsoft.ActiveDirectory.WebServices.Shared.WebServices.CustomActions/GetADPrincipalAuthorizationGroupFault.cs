using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class GetADPrincipalAuthorizationGroupFault : CustomActionFault
{
	[IgnoreDataMember]
	public override string FaultName => "GetADPrincipalAuthorizationGroupFault";

	public GetADPrincipalAuthorizationGroupFault()
	{
	}

	public GetADPrincipalAuthorizationGroupFault(ArgumentErrorDetailCA argErr, string strErr, DirectoryErrorDetailCA dirErr)
		: base(argErr, strErr, dirErr)
	{
	}
}

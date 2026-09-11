using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class GetADPrincipalGroupMembershipFault : CustomActionFault
{
	[IgnoreDataMember]
	public override string FaultName => "GetADPrincipalGroupMembershipFault";

	public GetADPrincipalGroupMembershipFault()
	{
	}

	public GetADPrincipalGroupMembershipFault(ArgumentErrorDetailCA argErr, string strErr, DirectoryErrorDetailCA dirErr)
		: base(argErr, strErr, dirErr)
	{
	}
}

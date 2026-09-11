using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class ActiveDirectoryGroup : ActiveDirectoryPrincipal
{
	[DataMember(IsRequired = true)]
	public ActiveDirectoryGroupType GroupType;

	[DataMember(IsRequired = true)]
	public ActiveDirectoryGroupScope GroupScope;
}

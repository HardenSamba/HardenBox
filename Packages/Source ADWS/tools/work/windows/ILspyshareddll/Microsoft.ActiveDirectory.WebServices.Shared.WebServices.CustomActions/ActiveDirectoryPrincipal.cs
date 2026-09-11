using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
[KnownType(typeof(ActiveDirectoryGroup))]
public class ActiveDirectoryPrincipal : ActiveDirectoryObject
{
	[DataMember(IsRequired = true)]
	public byte[] SID;

	[DataMember(IsRequired = true)]
	public string SamAccountName;
}

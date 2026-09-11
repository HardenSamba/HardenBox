using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public enum ActiveDirectoryNameFormat
{
	[EnumMember]
	DistinguishedName = 1,
	[EnumMember]
	CanonicalName
}

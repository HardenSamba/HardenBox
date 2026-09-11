using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public enum ActiveDirectoryOperationMasterRole
{
	[EnumMember]
	PDCEmulator,
	[EnumMember]
	RIDMaster,
	[EnumMember]
	InfrastructureMaster,
	[EnumMember]
	SchemaMaster,
	[EnumMember]
	DomainNamingMaster
}

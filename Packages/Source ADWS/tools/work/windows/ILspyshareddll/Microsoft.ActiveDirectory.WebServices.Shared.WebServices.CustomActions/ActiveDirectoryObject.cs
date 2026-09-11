using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
[KnownType(typeof(ActiveDirectoryPrincipal))]
[KnownType(typeof(ActiveDirectoryGroup))]
[KnownType(typeof(ActiveDirectoryPartition))]
[KnownType(typeof(ActiveDirectoryDomain))]
public class ActiveDirectoryObject
{
	[DataMember(IsRequired = true)]
	public string Name;

	[DataMember(IsRequired = true)]
	public Guid ObjectGuid;

	[DataMember(IsRequired = true)]
	public string DistinguishedName;

	[DataMember(IsRequired = true)]
	public string ObjectClass;

	[DataMember(IsRequired = true)]
	public string[] ObjectTypes;

	[DataMember(IsRequired = true)]
	public string ReferenceServer;
}

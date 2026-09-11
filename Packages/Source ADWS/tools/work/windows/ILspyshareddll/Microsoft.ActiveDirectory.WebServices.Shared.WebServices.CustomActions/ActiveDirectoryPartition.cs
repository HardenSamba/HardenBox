using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
[KnownType(typeof(ActiveDirectoryDomain))]
public class ActiveDirectoryPartition : ActiveDirectoryObject
{
	[DataMember(IsRequired = true)]
	public string DNSRoot;

	[DataMember(IsRequired = true)]
	public string LostAndFoundContainer;

	[DataMember(IsRequired = true)]
	public string DeletedObjectsContainer;

	[DataMember(IsRequired = true)]
	public string QuotasContainer;

	[DataMember(IsRequired = true)]
	public string[] SubordinateReferences;

	[DataMember(IsRequired = true)]
	public string[] ReplicaDirectoryServer;

	[DataMember(IsRequired = true)]
	public string[] ReadOnlyReplicaDirectoryServer;
}

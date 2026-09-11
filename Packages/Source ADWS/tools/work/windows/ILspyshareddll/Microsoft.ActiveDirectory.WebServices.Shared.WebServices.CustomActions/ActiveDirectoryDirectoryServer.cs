using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
[KnownType(typeof(ActiveDirectoryDomainController))]
public class ActiveDirectoryDirectoryServer
{
	[DataMember(IsRequired = true)]
	public string Name;

	[DataMember(IsRequired = true)]
	public string Site;

	[DataMember(IsRequired = true)]
	public string[] Partitions;

	[DataMember(IsRequired = true)]
	public string DefaultPartition;

	[DataMember(IsRequired = true)]
	public string HostName;

	[DataMember(IsRequired = true)]
	public int LdapPort;

	[DataMember(IsRequired = true)]
	public int SslPort;

	[DataMember(IsRequired = true)]
	public ActiveDirectoryOperationMasterRole[] OperationMasterRole;

	[DataMember(IsRequired = true)]
	public string NTDSSettingsObjectDN;

	[DataMember(IsRequired = true)]
	public string ServerObjectDN;

	[DataMember(IsRequired = true)]
	public Guid ServerObjectGuid;

	[DataMember(IsRequired = true)]
	public Guid InvocationId;
}

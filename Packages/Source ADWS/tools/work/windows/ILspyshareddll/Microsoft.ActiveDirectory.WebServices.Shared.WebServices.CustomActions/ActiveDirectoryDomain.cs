using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class ActiveDirectoryDomain : ActiveDirectoryPartition
{
	[DataMember(IsRequired = true)]
	public string ManagedBy;

	[DataMember(IsRequired = true)]
	public int DomainMode;

	[DataMember(IsRequired = true)]
	public string InfrastructureMaster;

	[DataMember(IsRequired = true)]
	public string RIDMaster;

	[DataMember(IsRequired = true)]
	public string PDCEmulator;

	[DataMember(IsRequired = true)]
	public string ParentDomain;

	[DataMember(IsRequired = true)]
	public string[] ChildDomains;

	[DataMember(IsRequired = true)]
	public string NetBIOSName;

	[DataMember(IsRequired = true)]
	public string[] AllowedDNSSuffixes;

	[DataMember(IsRequired = true)]
	public byte[] DomainSID;

	[DataMember(IsRequired = true)]
	public string[] AppliedGroupPolicies;

	[DataMember(IsRequired = true)]
	public TimeSpan? LastLogonReplicationInterval;

	[DataMember(IsRequired = true)]
	public string Forest;

	[DataMember(IsRequired = true)]
	public string UsersContainer;

	[DataMember(IsRequired = true)]
	public string ComputersContainer;

	[DataMember(IsRequired = true)]
	public string DomainControllersContainer;

	[DataMember(IsRequired = true)]
	public string ForeignSecurityPrincipalsContainer;

	[DataMember(IsRequired = true)]
	public string SystemsContainer;
}

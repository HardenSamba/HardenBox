using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class ActiveDirectoryForest
{
	[DataMember(IsRequired = true)]
	public string Name;

	[DataMember(IsRequired = true)]
	public int ForestMode;

	[DataMember(IsRequired = true)]
	public string[] UPNSuffixes;

	[DataMember(IsRequired = true)]
	public string[] SPNSuffixes;

	[DataMember(IsRequired = true)]
	public string[] CrossForestReferences;

	[DataMember(IsRequired = true)]
	public string SchemaMaster;

	[DataMember(IsRequired = true)]
	public string DomainNamingMaster;

	[DataMember(IsRequired = true)]
	public string[] ApplicationPartitions;

	[DataMember(IsRequired = true)]
	public string RootDomain;

	[DataMember(IsRequired = true)]
	public string[] Domains;

	[DataMember(IsRequired = true)]
	public string[] GlobalCatalogs;

	[DataMember(IsRequired = true)]
	public string[] Sites;
}

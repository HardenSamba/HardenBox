using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class ActiveDirectoryDomainController : ActiveDirectoryDirectoryServer
{
	[DataMember(IsRequired = true)]
	public bool IsReadOnly;

	[DataMember(IsRequired = true)]
	public bool IsGlobalCatalog;

	[DataMember(IsRequired = true)]
	public string OSVersion;

	[DataMember(IsRequired = true)]
	public string OSName;

	[DataMember(IsRequired = true)]
	public string OSServicepack;

	[DataMember(IsRequired = true)]
	public string OSHotFix;

	[DataMember(IsRequired = true)]
	public string ComputerDN;

	[DataMember(IsRequired = true)]
	public string Domain;

	[DataMember(IsRequired = true)]
	public string Forest;

	[DataMember(IsRequired = true)]
	public bool Enabled;
}

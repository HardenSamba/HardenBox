using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class ActiveDirectoryNameTranslateResult
{
	[DataMember(IsRequired = true)]
	public uint Result;

	[DataMember(IsRequired = true)]
	public string Name;
}

using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[XmlType(TypeName = "ModeType", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
public enum ModeType
{
	Modify,
	Insert,
	Remove
}

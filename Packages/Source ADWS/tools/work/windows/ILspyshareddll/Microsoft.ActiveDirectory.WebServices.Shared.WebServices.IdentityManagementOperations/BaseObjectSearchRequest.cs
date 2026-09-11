using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[XmlType(TypeName = "BaseObjectSearchRequest", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IncludeInSchema = true)]
[XmlRoot(ElementName = "BaseObjectSearchRequest", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
public class BaseObjectSearchRequest
{
	private string dialectField;

	private AttributeType[] attributeTypeField;

	private DirControl[] controlField;

	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	[XmlAttribute(AttributeName = "Dialect", DataType = "anyURI")]
	public string Dialect
	{
		get
		{
			return dialectField;
		}
		set
		{
			dialectField = value;
		}
	}

	[XmlElement(ElementName = "AttributeType", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	public AttributeType[] AttributeTypes
	{
		get
		{
			return attributeTypeField;
		}
		set
		{
			attributeTypeField = value;
		}
	}

	[XmlArray(ElementName = "controls", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	[XmlArrayItem(ElementName = "control", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory", Type = typeof(DirControl), IsNullable = false)]
	public DirControl[] Controls
	{
		get
		{
			return controlField;
		}
		set
		{
			controlField = value;
		}
	}
}

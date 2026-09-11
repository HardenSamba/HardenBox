using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "AddRequest", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
public class AddRequest
{
	private string dialectField;

	private AttributeTypeAndValueType[] attributeTypeAndValueField;

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

	[XmlElement(ElementName = "AttributeTypeAndValue", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	public AttributeTypeAndValueType[] AttributeTypeAndValues
	{
		get
		{
			return attributeTypeAndValueField;
		}
		set
		{
			attributeTypeAndValueField = value;
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

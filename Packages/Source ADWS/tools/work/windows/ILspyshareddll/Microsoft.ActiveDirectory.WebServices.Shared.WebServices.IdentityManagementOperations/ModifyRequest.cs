using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "ModifyRequest", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
public class ModifyRequest
{
	private string dialectField;

	private ChangeType[] changeField;

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

	[XmlElement("Change", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	public ChangeType[] Changes
	{
		get
		{
			return changeField;
		}
		set
		{
			changeField = value;
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

using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlRoot(ElementName = "AttributeTypeNotValidForEntry", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
public class AttributeTypeNotValidForEntry
{
	private string attributeTypeField;

	[XmlElement(ElementName = "AttributeType", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
	public string AttributeTypeField
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

	public AttributeTypeNotValidForEntry()
	{
		attributeTypeField = null;
	}

	public AttributeTypeNotValidForEntry(InvalidExpressionException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		attributeTypeField = e.Expression;
	}
}

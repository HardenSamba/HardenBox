using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlRoot(ElementName = "AttributeTypeNotValidForDialect", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
public class AttributeTypeNotValidForDialect
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

	public AttributeTypeNotValidForDialect()
	{
		attributeTypeField = null;
	}

	public AttributeTypeNotValidForDialect(InvalidExpressionException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		attributeTypeField = e.Expression;
	}
}

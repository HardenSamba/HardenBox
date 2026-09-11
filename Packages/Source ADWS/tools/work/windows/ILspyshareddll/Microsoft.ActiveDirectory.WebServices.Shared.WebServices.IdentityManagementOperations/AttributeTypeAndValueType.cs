using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlInclude(typeof(ChangeType))]
[XmlType(TypeName = "AttributeTypeAndValue", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
[XmlRoot(ElementName = "AttributeTypeAndValue", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
public class AttributeTypeAndValueType
{
	private AttributeType attrTypeField;

	private AttributeValue attrValueField;

	[XmlElement(ElementName = "AttributeType", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	public AttributeType AttributeType
	{
		get
		{
			return attrTypeField;
		}
		set
		{
			attrTypeField = value;
		}
	}

	[XmlElement(ElementName = "AttributeValue", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	public AttributeValue AttributeValue
	{
		get
		{
			return attrValueField;
		}
		set
		{
			attrValueField = value;
		}
	}

	public AttributeTypeAndValueType()
	{
	}

	public AttributeTypeAndValueType(string type, IList<SingleValue> values)
	{
		attrTypeField = new AttributeType(type);
		attrValueField = new AttributeValue(values);
	}
}

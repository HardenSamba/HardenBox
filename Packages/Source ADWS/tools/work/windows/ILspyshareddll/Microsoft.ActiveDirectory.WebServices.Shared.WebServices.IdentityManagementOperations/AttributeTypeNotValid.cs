using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlRoot(ElementName = "AttributeTypeNotValid", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
public class AttributeTypeNotValid
{
	private AttributeTypeNotValidForDialect diaErr;

	private AttributeTypeNotValidForEntry entErr;

	[XmlElement("AttributeTypeNotValidForDialect")]
	public AttributeTypeNotValidForDialect AttributeTypeNotValidForDialectField
	{
		get
		{
			return diaErr;
		}
		set
		{
			diaErr = value;
		}
	}

	[XmlElement("AttributeTypeNotValidForEntry")]
	public AttributeTypeNotValidForEntry AttributeTypeNotValidForEntryField
	{
		get
		{
			return entErr;
		}
		set
		{
			entErr = value;
		}
	}

	public AttributeTypeNotValid()
	{
		diaErr = null;
		entErr = null;
	}

	public AttributeTypeNotValid(AttributeTypeNotValidForDialect De, AttributeTypeNotValidForEntry Ee)
	{
		diaErr = De;
		entErr = Ee;
	}
}

using System;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

public class AttributeType : ExpressionBase, IXmlSerializable
{
	private string rangeLow;

	private string rangeHigh;

	public string RangeLow => rangeLow;

	public string RangeHigh => rangeHigh;

	public AttributeType()
	{
		rangeLow = null;
		rangeHigh = null;
	}

	public AttributeType(string xpath)
		: base(xpath)
	{
		rangeLow = null;
		rangeHigh = null;
	}

	public AttributeType(string xpath, string rangeLow, string rangeHigh)
		: base(xpath)
	{
		this.rangeLow = rangeLow;
		this.rangeHigh = rangeHigh;
	}

	public void ReadXml(XmlReader reader)
	{
		if (reader == null)
		{
			throw new ArgumentNullException("reader");
		}
		rangeLow = reader.GetAttribute("RangeLow");
		rangeHigh = reader.GetAttribute("RangeHigh");
		ReadXml(reader, "AttributeType", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess");
	}
}

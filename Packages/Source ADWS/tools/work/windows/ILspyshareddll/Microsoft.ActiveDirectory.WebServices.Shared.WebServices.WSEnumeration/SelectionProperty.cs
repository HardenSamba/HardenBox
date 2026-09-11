using System;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

public class SelectionProperty : ExpressionBase, IXmlSerializable
{
	private string rangeLow;

	private string rangeHigh;

	public string RangeLow => rangeLow;

	public string RangeHigh => rangeHigh;

	public SelectionProperty()
	{
		rangeLow = null;
		rangeHigh = null;
	}

	public SelectionProperty(string xpath)
		: base(xpath)
	{
		rangeLow = null;
		rangeHigh = null;
	}

	public SelectionProperty(string xpath, string rangeLow, string rangeHigh)
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
		ReadXml(reader, "SelectionProperty", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
	}
}

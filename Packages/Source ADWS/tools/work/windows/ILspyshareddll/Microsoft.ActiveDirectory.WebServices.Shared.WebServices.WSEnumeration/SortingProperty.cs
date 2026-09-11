using System;
using System.Globalization;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

public class SortingProperty : ExpressionBase, IXmlSerializable
{
	private bool ascending = true;

	public bool Ascending => ascending;

	public SortingProperty()
	{
		ascending = true;
	}

	public SortingProperty(string xpath)
		: this(xpath, ascending: true)
	{
	}

	public SortingProperty(string xpath, bool ascending)
		: base(xpath)
	{
		this.ascending = ascending;
	}

	public void ReadXml(XmlReader reader)
	{
		if (reader == null)
		{
			throw new ArgumentNullException("reader");
		}
		string attribute = reader.GetAttribute("Ascending");
		if (attribute != null && attribute.Equals("false", StringComparison.InvariantCulture))
		{
			ascending = false;
		}
		else
		{
			ascending = true;
		}
		ReadXml(reader, "SortingProperty", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
	}

	public override void WriteXml(XmlWriter writer)
	{
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		writer.WriteAttributeString("Ascending", ascending.ToString().ToLower(CultureInfo.InvariantCulture));
		base.WriteXml(writer);
	}
}

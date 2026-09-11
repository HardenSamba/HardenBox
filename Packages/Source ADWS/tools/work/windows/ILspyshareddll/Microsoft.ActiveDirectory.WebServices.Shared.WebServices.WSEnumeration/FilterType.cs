using System;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
[XmlRoot(ElementName = "Filter", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IsNullable = false)]
public class FilterType
{
	private string dialectField;

	private XmlNode queryField;

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

	[XmlAnyElement]
	public XmlNode QueryField
	{
		get
		{
			return queryField;
		}
		set
		{
			queryField = value;
		}
	}
}

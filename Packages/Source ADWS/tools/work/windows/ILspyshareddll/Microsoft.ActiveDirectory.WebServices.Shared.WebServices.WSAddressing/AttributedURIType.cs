using System;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSAddressing;

[Serializable]
[XmlType(TypeName = "AttributedURI", Namespace = "http://www.w3.org/2005/08/addressing")]
[XmlRoot(ElementName = "MessageID", Namespace = "http://www.w3.org/2005/08/addressing", IsNullable = false)]
public class AttributedURIType
{
	[NonSerialized]
	private XmlAttribute[] anyAttrField;

	private string valueField;

	[XmlAnyAttribute]
	public XmlAttribute[] AnyAttr
	{
		get
		{
			return anyAttrField;
		}
		set
		{
			anyAttrField = value;
		}
	}

	[XmlText(DataType = "anyURI")]
	public string Value
	{
		get
		{
			return valueField;
		}
		set
		{
			valueField = value;
		}
	}
}

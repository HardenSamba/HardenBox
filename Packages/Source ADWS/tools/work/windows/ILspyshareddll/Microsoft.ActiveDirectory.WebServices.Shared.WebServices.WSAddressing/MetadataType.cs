using System;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSAddressing;

[Serializable]
[XmlType(TypeName = "MetadataType", Namespace = "http://www.w3.org/2005/08/addressing")]
[XmlRoot(ElementName = "Metadata", Namespace = "http://www.w3.org/2005/08/addressing", IsNullable = false)]
public class MetadataType
{
	[NonSerialized]
	private XmlElement[] anyField;

	[NonSerialized]
	private XmlAttribute[] anyAttrField;

	[XmlAnyElement]
	public XmlElement[] Any
	{
		get
		{
			return anyField;
		}
		set
		{
			anyField = value;
		}
	}

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
}

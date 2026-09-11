using System;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSAddressing;

[Serializable]
[XmlType(TypeName = "EndpointReference", Namespace = "http://www.w3.org/2005/08/addressing")]
[XmlRoot(ElementName = "EndpointReference", Namespace = "http://www.w3.org/2005/08/addressing", IsNullable = false)]
public class EndpointReferenceType
{
	private AttributedURIType addressField;

	private ReferenceParametersType referenceParametersField;

	private MetadataType metadataField;

	[NonSerialized]
	private XmlElement[] anyField;

	[NonSerialized]
	private XmlAttribute[] anyAttrField;

	[XmlElement(ElementName = "Address")]
	public AttributedURIType Address
	{
		get
		{
			return addressField;
		}
		set
		{
			addressField = value;
		}
	}

	[XmlElement(ElementName = "ReferenceParameters")]
	public ReferenceParametersType ReferenceParameters
	{
		get
		{
			return referenceParametersField;
		}
		set
		{
			referenceParametersField = value;
		}
	}

	[XmlElement(ElementName = "Metadata")]
	public MetadataType Metadata
	{
		get
		{
			return metadataField;
		}
		set
		{
			metadataField = value;
		}
	}

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

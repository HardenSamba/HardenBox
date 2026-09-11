using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "PullResponse", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IsNullable = false)]
public class PullResponse
{
	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	private XmlNode enumerationContextField;

	private Items itemsField;

	private PullControls controlField;

	private XmlNode endOfSequenceField;

	[XmlAnyElement(Name = "EnumerationContext", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public XmlNode EnumerationContext
	{
		get
		{
			return enumerationContextField;
		}
		set
		{
			enumerationContextField = value;
		}
	}

	[XmlAnyElement(Name = "Items", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public Items Items
	{
		get
		{
			return itemsField;
		}
		set
		{
			itemsField = value;
		}
	}

	[XmlAnyElement(Name = "controls", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public PullControls Controls
	{
		get
		{
			return controlField;
		}
		set
		{
			controlField = value;
		}
	}

	[XmlAnyElement(Name = "EndOfSequence", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public XmlNode EndOfSequence
	{
		get
		{
			return endOfSequenceField;
		}
		set
		{
			endOfSequenceField = value;
		}
	}

	private void FillNameSpaces()
	{
		xmlns = new XmlSerializerNamespaces();
		xmlns.Add("wsen", "http://schemas.xmlsoap.org/ws/2004/09/enumeration");
		xmlns.Add("ad", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
		xmlns.Add("addata", "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data");
		xmlns.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
		xmlns.Add("xsd", "http://www.w3.org/2001/XMLSchema");
	}

	public PullResponse()
	{
	}

	public PullResponse(IList<DirectoryObjectFragment> dirObjFrags, IList<DirectoryControl> controlList)
	{
		FillNameSpaces();
		Items = new Items(dirObjFrags);
		Controls = new PullControls(controlList);
	}
}

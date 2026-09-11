using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "Enumerate", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IsNullable = false)]
public class Enumerate
{
	private object anyField;

	private string expiresField;

	private FilterType filterField;

	private Sorting sortingField;

	private Selection selectionField;

	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	[XmlAnyElement]
	public object AnyField
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

	[XmlElement(ElementName = "Expires", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public string Expires
	{
		get
		{
			return expiresField;
		}
		set
		{
			expiresField = value;
		}
	}

	[XmlElement(ElementName = "Filter", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public FilterType Filter
	{
		get
		{
			return filterField;
		}
		set
		{
			filterField = value;
		}
	}

	[XmlElement(ElementName = "Selection", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public Selection Selection
	{
		get
		{
			return selectionField;
		}
		set
		{
			selectionField = value;
		}
	}

	[XmlElement(ElementName = "Sorting", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public Sorting Sorting
	{
		get
		{
			return sortingField;
		}
		set
		{
			sortingField = value;
		}
	}
}

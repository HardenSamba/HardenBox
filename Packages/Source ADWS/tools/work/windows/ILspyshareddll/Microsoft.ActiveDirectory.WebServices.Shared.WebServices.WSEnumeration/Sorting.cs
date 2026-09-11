using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
[XmlRoot(ElementName = "Sorting", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory", IsNullable = true)]
public class Sorting
{
	private string dialectField;

	private SortingProperty[] sortingPropertiesField;

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

	[XmlElement(ElementName = "SortingProperty", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public SortingProperty[] SortingProperties
	{
		get
		{
			return sortingPropertiesField;
		}
		set
		{
			sortingPropertiesField = value;
		}
	}
}

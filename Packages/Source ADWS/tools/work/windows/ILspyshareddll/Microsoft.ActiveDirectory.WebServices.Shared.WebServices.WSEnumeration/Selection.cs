using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
[XmlRoot(ElementName = "Selection", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory", IsNullable = true)]
public class Selection
{
	private string dialectField;

	private SelectionProperty[] selectionPropertiesField;

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

	[XmlElement(ElementName = "SelectionProperty", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public SelectionProperty[] SelectionProperties
	{
		get
		{
			return selectionPropertiesField;
		}
		set
		{
			selectionPropertiesField = value;
		}
	}
}

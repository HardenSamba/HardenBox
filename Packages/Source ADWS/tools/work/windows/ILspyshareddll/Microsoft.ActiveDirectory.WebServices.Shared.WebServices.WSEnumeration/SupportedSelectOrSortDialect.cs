using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlRoot(ElementName = "SupportedSelectOrSortDialect", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
public class SupportedSelectOrSortDialect
{
	private string textField;

	[XmlText(DataType = "anyURI")]
	public string Text
	{
		get
		{
			return textField;
		}
		set
		{
			textField = value;
		}
	}

	public SupportedSelectOrSortDialect()
		: this(string.Empty)
	{
	}

	public SupportedSelectOrSortDialect(string dialect)
	{
		if (dialect == null)
		{
			throw new ArgumentNullException("dialect");
		}
		textField = dialect;
	}
}

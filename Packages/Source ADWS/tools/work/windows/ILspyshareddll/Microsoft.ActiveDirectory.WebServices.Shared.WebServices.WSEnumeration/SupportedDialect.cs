using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlRoot(ElementName = "SupportedDialect", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
public class SupportedDialect
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

	public SupportedDialect()
		: this(string.Empty)
	{
	}

	public SupportedDialect(string dialect)
	{
		if (dialect == null)
		{
			throw new ArgumentNullException("dialect");
		}
		textField = dialect;
	}
}

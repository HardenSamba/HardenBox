using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlRoot(ElementName = "FragmentDialect", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
public class FragmentDialect
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

	public FragmentDialect()
		: this(string.Empty)
	{
	}

	public FragmentDialect(string dialect)
	{
		if (dialect == null)
		{
			throw new ArgumentNullException("dialect");
		}
		textField = dialect;
	}
}

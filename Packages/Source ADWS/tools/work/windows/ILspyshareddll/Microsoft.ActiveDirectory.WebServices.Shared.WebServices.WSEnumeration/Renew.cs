using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "Renew", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IsNullable = false)]
public class Renew
{
	private Guid enumerationContextField = Guid.Empty;

	private string expiresField;

	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	[XmlElement(ElementName = "EnumerationContext", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public Guid EnumerationContext
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
}

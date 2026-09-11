using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "RenewResponse", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IsNullable = false)]
public class RenewResponse
{
	private string expiresField;

	private Guid enumerationContextField;

	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

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

	public RenewResponse()
	{
	}

	public RenewResponse(Guid enumerationContext, string Expires)
	{
		xmlns = new XmlSerializerNamespaces();
		xmlns.Add("wsen", "http://schemas.xmlsoap.org/ws/2004/09/enumeration");
		if (Expires == null)
		{
			throw new ArgumentNullException("Expires");
		}
		enumerationContextField = enumerationContext;
		expiresField = Expires;
	}
}

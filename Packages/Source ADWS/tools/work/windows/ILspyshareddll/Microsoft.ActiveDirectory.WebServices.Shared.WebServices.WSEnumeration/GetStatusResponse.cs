using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "GetStatusResponse", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IsNullable = false)]
public class GetStatusResponse
{
	private string expiresField;

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

	public GetStatusResponse()
	{
	}

	public GetStatusResponse(string Expires)
	{
		xmlns = new XmlSerializerNamespaces();
		xmlns.Add("wsen", "http://schemas.xmlsoap.org/ws/2004/09/enumeration");
		if (Expires == null)
		{
			throw new ArgumentNullException("Expires");
		}
		expiresField = Expires;
	}
}

using System;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSAddressing;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlType(TypeName = "ResourceCreated", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/transfer")]
[XmlRoot(ElementName = "ResourceCreated", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/transfer", IsNullable = false)]
public class AddResponse
{
	private string addressField;

	private ReferenceParametersType referenceParametersField;

	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	[XmlElement(ElementName = "Address", Namespace = "http://www.w3.org/2005/08/addressing")]
	public string Address
	{
		get
		{
			return addressField;
		}
		set
		{
			addressField = value;
		}
	}

	[XmlElement(ElementName = "ReferenceParameters", Namespace = "http://www.w3.org/2005/08/addressing")]
	public ReferenceParametersType ReferenceParameters
	{
		get
		{
			return referenceParametersField;
		}
		set
		{
			referenceParametersField = value;
		}
	}

	public AddResponse()
	{
	}

	public AddResponse(string address, ReferenceParametersType refParamtype)
	{
		if (address == null)
		{
			throw new ArgumentNullException("address");
		}
		if (refParamtype == null)
		{
			throw new ArgumentNullException("refParamType");
		}
		xmlns = new XmlSerializerNamespaces();
		xmlns.Add("wst", "http://schemas.xmlsoap.org/ws/2004/09/transfer");
		xmlns.Add("ad", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
		xmlns.Add("wsa", "http://www.w3.org/2005/08/addressing");
		addressField = address;
		referenceParametersField = refParamtype;
	}
}

using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlType(TypeName = "FaultDetail", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
[XmlRoot(ElementName = "FaultDetail", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
public class FaultDetail
{
	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	private string uri;

	private int encodingLimitSize;

	[XmlAttribute(AttributeName = "SizeLimit", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	public int EncodingLimitSize
	{
		get
		{
			return encodingLimitSize;
		}
		set
		{
			encodingLimitSize = value;
		}
	}

	[XmlText]
	public string Uri
	{
		get
		{
			return uri;
		}
		set
		{
			uri = value;
		}
	}

	public FaultDetail()
	{
		uri = null;
	}

	public FaultDetail(EncodingLimitExceededDetailType part, string uri)
	{
		if (uri == null)
		{
			throw new ArgumentNullException("uri");
		}
		xmlns = new XmlSerializerNamespaces();
		xmlns.Add("wsman", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd");
		xmlns.Add("da", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess");
		this.uri = uri;
		if (part == EncodingLimitExceededDetailType.AttributeTypeAndValues)
		{
			encodingLimitSize = 100;
		}
		else
		{
			encodingLimitSize = 100;
		}
	}
}

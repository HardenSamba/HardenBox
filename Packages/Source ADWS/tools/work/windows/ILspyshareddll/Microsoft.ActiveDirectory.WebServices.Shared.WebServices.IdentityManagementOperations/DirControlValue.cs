using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[XmlType(TypeName = "base64Binary", Namespace = "http://www.w3.org/2001/XMLSchema")]
public class DirControlValue
{
	private string type;

	private byte[] value;

	[XmlAttribute(AttributeName = "type", Namespace = "http://www.w3.org/2001/XMLSchema-instance")]
	public string Type
	{
		get
		{
			return type;
		}
		set
		{
			type = value;
		}
	}

	[XmlText]
	public byte[] Value
	{
		get
		{
			return value;
		}
		set
		{
			this.value = value;
		}
	}
}

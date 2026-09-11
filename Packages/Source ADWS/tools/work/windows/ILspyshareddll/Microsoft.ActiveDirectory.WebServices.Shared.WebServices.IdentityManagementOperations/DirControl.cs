using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

public class DirControl
{
	private string oid;

	private DirControlValue controlValue;

	private bool criticality;

	[XmlAttribute(AttributeName = "type")]
	public string OID
	{
		get
		{
			return oid;
		}
		set
		{
			oid = value;
		}
	}

	[XmlElement(ElementName = "controlValue", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public DirControlValue ControlValue
	{
		get
		{
			return controlValue;
		}
		set
		{
			controlValue = value;
		}
	}

	[XmlAttribute(AttributeName = "criticality")]
	public bool Criticality
	{
		get
		{
			return criticality;
		}
		set
		{
			criticality = value;
		}
	}
}

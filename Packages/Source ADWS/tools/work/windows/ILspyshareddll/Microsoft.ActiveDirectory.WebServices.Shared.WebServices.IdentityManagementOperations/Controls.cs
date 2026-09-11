using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[XmlRoot(ElementName = "controls", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
public class Controls
{
	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	private DirControl[] controlField;

	[XmlElement(ElementName = "control", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory", Type = typeof(DirControl), IsNullable = false)]
	public DirControl[] Ctrls
	{
		get
		{
			return controlField;
		}
		set
		{
			controlField = value;
		}
	}
}

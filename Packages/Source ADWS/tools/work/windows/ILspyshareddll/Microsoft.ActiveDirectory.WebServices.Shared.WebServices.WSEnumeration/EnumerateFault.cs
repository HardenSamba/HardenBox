using System;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlRoot(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
public class EnumerateFault : Fault
{
	private string invalidProperty;

	public string InvalidProperty
	{
		get
		{
			return invalidProperty;
		}
		set
		{
			invalidProperty = value;
		}
	}

	public EnumerateFault()
	{
		invalidProperty = null;
	}

	public EnumerateFault(ArgumentErrorDetail Ae, string Se, DirectoryErrorDetail De, string property)
		: base(Ae, Se, De)
	{
		invalidProperty = property;
	}
}

using System;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlRoot(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
public class PullFault : Fault
{
	public PullFault()
	{
	}

	public PullFault(ArgumentErrorDetail Ae, string Se, DirectoryErrorDetail De)
		: base(Ae, Se, De)
	{
	}
}

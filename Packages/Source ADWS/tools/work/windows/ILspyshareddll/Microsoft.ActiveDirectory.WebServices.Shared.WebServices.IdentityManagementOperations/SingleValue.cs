using System;
using System.Collections.Generic;
using System.Xml;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

public class SingleValue
{
	private XmlNode value;

	private Dictionary<string, string> nsPairs;

	public XmlNode Value => value;

	public Dictionary<string, string> NsPairs => nsPairs;

	public SingleValue()
	{
		nsPairs = new Dictionary<string, string>();
	}

	public SingleValue(XmlNode adValue, Dictionary<string, string> namespacePairs)
	{
		if (namespacePairs == null)
		{
			throw new ArgumentNullException("namespacePairs");
		}
		nsPairs = namespacePairs;
		value = adValue;
	}
}

using System;
using System.Collections.Generic;
using System.Xml;

namespace Microsoft.ActiveDirectory.Shared.Utilities;

internal class SharedUtils
{
	internal static void GetNSPairsOutOfTheString(string str, XmlReader xmlReader, Dictionary<string, string> namespacePairs)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		if (xmlReader == null)
		{
			throw new ArgumentNullException("xmlReader");
		}
		if (namespacePairs == null)
		{
			throw new ArgumentNullException("namespacePairs");
		}
		if (!str.Contains(":") || str.StartsWith(":", StringComparison.Ordinal))
		{
			return;
		}
		string[] array = str.Split(':');
		if (array == null || array.Length == 0)
		{
			return;
		}
		string text = array[0];
		if (!namespacePairs.ContainsKey(text))
		{
			string text2 = xmlReader.LookupNamespace(text);
			if (text2 != null && text2.Length > 0)
			{
				namespacePairs.Add(text, text2);
			}
		}
	}
}

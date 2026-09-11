using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using Microsoft.ActiveDirectory.Shared.Utilities;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

public class ExpressionBase
{
	private string xpathExpression;

	private Dictionary<string, string> nsPairs;

	public string XPathExpression => xpathExpression;

	public Dictionary<string, string> NsPairs => nsPairs;

	public ExpressionBase()
	{
		nsPairs = new Dictionary<string, string>();
		xpathExpression = string.Empty;
	}

	public ExpressionBase(string xpath)
	{
		if (xpath == null)
		{
			throw new ArgumentNullException("xpath");
		}
		nsPairs = new Dictionary<string, string>();
		xpathExpression = xpath;
	}

	public void ReadXml(XmlReader reader, string elementName, string elementNamespace)
	{
		if (reader == null)
		{
			throw new ArgumentNullException("reader");
		}
		if (elementName == null)
		{
			throw new ArgumentNullException("elementName");
		}
		if (elementNamespace == null)
		{
			throw new ArgumentNullException("elementNamespace");
		}
		int depth = reader.Depth;
		xpathExpression = reader.ReadString();
		string attributeName = string.Empty;
		string objectName = string.Empty;
		string predicate = string.Empty;
		if (xpathExpression != null && xpathExpression != string.Empty)
		{
			XPathLevel1.ParseXPathLevel1String(xpathExpression, out objectName, out attributeName, out predicate);
			string[] array = new string[3] { objectName, attributeName, predicate };
			for (int i = 0; i < array.Length; i++)
			{
				SharedUtils.GetNSPairsOutOfTheString(array[i], reader, nsPairs);
			}
			nsPairs.Add(string.Empty, reader.LookupNamespace(string.Empty));
		}
		do
		{
			if (reader.LocalName != null && reader.NamespaceURI != null && reader.LocalName.Equals(elementName, StringComparison.Ordinal) && reader.NamespaceURI.Equals(elementNamespace, StringComparison.OrdinalIgnoreCase) && reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
			{
				reader.Read();
				break;
			}
		}
		while (reader.Read());
	}

	public virtual void WriteXml(XmlWriter writer)
	{
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		writer.WriteString(xpathExpression);
	}

	public XmlSchema GetSchema()
	{
		return null;
	}
}

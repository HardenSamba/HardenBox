using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.Shared.Utilities;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

public class AttributeValue : IXmlSerializable
{
	private IList<SingleValue> values;

	public IList<SingleValue> Values => values;

	public AttributeValue()
	{
		values = new List<SingleValue>();
	}

	public AttributeValue(IList<SingleValue> adValues)
	{
		if (adValues == null)
		{
			throw new ArgumentNullException("adValues");
		}
		values = adValues;
	}

	public void ReadXml(XmlReader reader)
	{
		if (reader == null)
		{
			throw new ArgumentNullException("reader");
		}
		int depth = reader.Depth;
		string text = null;
		while (true)
		{
			if (XmlNodeType.Element != reader.NodeType && XmlNodeType.EndElement != reader.NodeType && reader.Read())
			{
				continue;
			}
			if (reader.LocalName != null && reader.NamespaceURI != null && reader.LocalName.Equals("AttributeValue", StringComparison.Ordinal) && reader.NamespaceURI.Equals("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", StringComparison.OrdinalIgnoreCase) && reader.NodeType == XmlNodeType.EndElement && reader.Depth == depth)
			{
				reader.Read();
				break;
			}
			if (reader.LocalName != null && reader.NamespaceURI != null && reader.LocalName.Equals("value", StringComparison.Ordinal) && reader.NamespaceURI.Equals("http://schemas.microsoft.com/2008/1/ActiveDirectory", StringComparison.OrdinalIgnoreCase) && reader.NodeType == XmlNodeType.Element && reader.Depth == depth + 1)
			{
				Dictionary<string, string> namespacePairs = new Dictionary<string, string>();
				text = reader.GetAttribute("type", "http://www.w3.org/2001/XMLSchema-instance");
				if (text != null)
				{
					SharedUtils.GetNSPairsOutOfTheString(text, reader, namespacePairs);
				}
				XmlDocument xmlDocument = new XmlDocument();
				xmlDocument.PreserveWhitespace = true;
				xmlDocument.LoadXml(reader.ReadOuterXml());
				SingleValue singleValue = new SingleValue(xmlDocument.DocumentElement, namespacePairs);
				singleValue.NsPairs.Add(string.Empty, reader.LookupNamespace(string.Empty));
				values.Add(singleValue);
			}
			else if (!reader.Read())
			{
				break;
			}
		}
	}

	public void WriteXml(XmlWriter writer)
	{
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		foreach (SingleValue value in values)
		{
			if (value.Value != null)
			{
				XmlReader xmlReader = XmlReader.Create(new StringReader(value.Value.OuterXml));
				writer.WriteNode(xmlReader, defattr: false);
				xmlReader.Close();
			}
		}
	}

	public XmlSchema GetSchema()
	{
		return null;
	}
}

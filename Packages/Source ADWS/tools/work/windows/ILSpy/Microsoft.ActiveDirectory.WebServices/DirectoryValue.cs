using System;
using System.Collections.Generic;
using System.Xml;

namespace Microsoft.ActiveDirectory.WebServices;

internal class DirectoryValue
{
	private object objValue;

	internal object Value
	{
		get
		{
			return objValue;
		}
		set
		{
			objValue = value;
		}
	}

	internal DirectoryValue()
	{
		objValue = null;
	}

	internal DirectoryValue(string s)
	{
		objValue = s;
	}

	internal DirectoryValue(byte[] a)
	{
		objValue = a;
	}

	internal DirectoryValue(Guid g)
	{
		objValue = g;
	}

	internal void GetXmlValue(XmlWriter writer, DirectorySyntaxType syntax)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "GetXmlValue entered");
		if (writer == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "writer null");
			throw new ArgumentNullException("writer");
		}
		string text = writer.LookupPrefix(AdwsConstants.AdNs);
		string text2 = writer.LookupPrefix(AdwsConstants.XsiNs);
		string text3 = writer.LookupPrefix(AdwsConstants.XsdNs);
		if (text == null || text2 == null || text3 == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "missing ad, xsi or xsd namespace to prefix mapping");
			throw new ArgumentException(AdwsStrings.MissingXMLNamespace);
		}
		writer.WriteStartElement("value", AdwsConstants.AdNs);
		if (objValue is string)
		{
			writer.WriteAttributeString("type", AdwsConstants.XsiNs, text3 + ":string");
			writer.WriteString((string)objValue);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "GetXmlValue: serialized {0}", (string)objValue);
		}
		else if (objValue is Guid)
		{
			writer.WriteAttributeString("type", AdwsConstants.XsiNs, text3 + ":string");
			Guid guid = (Guid)objValue;
			writer.WriteString(guid.ToString());
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "GetXmlValue: serialized {0}", guid.ToString());
		}
		else
		{
			if (!(objValue is byte[]))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "GetXmlValue: objValue not string, byte[], nor Guid");
				throw new ArgumentException(AdwsStrings.AttributeValueNotByteOrStringOrGuid);
			}
			writer.WriteAttributeString("type", AdwsConstants.XsiNs, text3 + ":base64Binary");
			byte[] array = (byte[])objValue;
			writer.WriteBase64(array, 0, array.Length);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "GetXmlValue: serialized base64Binary");
		}
		writer.WriteEndElement();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "GetXmlValue exit");
	}

	internal void SetXmlValue(XmlNode xmlValueNode, Dictionary<string, string> nsPairs, DirectorySyntaxType syntax)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "SetXmlValue entered");
		if (xmlValueNode == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: xmlValueNode null");
			throw new ArgumentNullException("xmlValueNode");
		}
		string text = null;
		string text2 = null;
		try
		{
			text = NamespaceToPrefix(AdwsConstants.XsiNs, xmlValueNode, nsPairs);
			text2 = NamespaceToPrefix(AdwsConstants.XsdNs, xmlValueNode, nsPairs);
		}
		catch (ArgumentException innerException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: missing xsi, xsd or ad namespace to prefix mapping");
			throw new ArgumentException(AdwsStrings.MissingXMLNamespace, innerException);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "SetXmlValue: XsdPrefix:[{0}], XsiPrefix:[{1}]", (text2 == null) ? "NULL" : text2, (text == null) ? "NULL" : text);
		if (xmlValueNode.LocalName == null || xmlValueNode.LocalName != "value")
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: not a value node");
			throw new ArgumentException(AdwsStrings.UnknownXmlNode);
		}
		if (!xmlValueNode.NamespaceURI.Equals(AdwsConstants.AdNs, StringComparison.InvariantCultureIgnoreCase))
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: value not in ad: namespace");
			throw new ArgumentException(AdwsStrings.ObjectInWrongNamespace);
		}
		if (xmlValueNode.Attributes == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: missing xsi:type attribute");
			throw new ArgumentException(AdwsStrings.MissingTypeAttribute);
		}
		if (xmlValueNode.FirstChild == null || xmlValueNode.FirstChild.Value == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: empty value");
			throw new ArgumentException(AdwsStrings.EmptyAttribute);
		}
		string name = ((!(text != string.Empty)) ? "type" : (text + ":type"));
		if (xmlValueNode.Attributes[name] == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: missing xsi:type attribute");
			throw new ArgumentException(AdwsStrings.MissingTypeAttribute);
		}
		string text3 = null;
		string[] array = xmlValueNode.Attributes[name].Value.Split(new char[1] { ':' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 1 && text2 == string.Empty)
		{
			text3 = array[0];
		}
		else
		{
			if (array.Length != 2 || !(array[0] == text2))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: missing xsi:type attribute value -- not of form xsd:<type>");
				throw new ArgumentException(AdwsStrings.UnknownAttributeType);
			}
			text3 = array[1];
		}
		if (text3 == "base64Binary")
		{
			byte[] array2;
			try
			{
				array2 = Convert.FromBase64String(xmlValueNode.FirstChild.Value.Trim());
			}
			catch (FormatException innerException2)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: Base 64 string len is not 4, or a multiple of 4");
				throw new ArgumentException(AdwsStrings.InvalidBase64Binary, innerException2);
			}
			objValue = array2;
		}
		else
		{
			if (!(text3 == "string"))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DirectoryValue", "SetXmlValue: Unknown attribute type {0}", array[1]);
				throw new ArgumentException(AdwsStrings.UnknownAttributeType);
			}
			objValue = xmlValueNode.FirstChild.Value;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "SetXmlValue exit Value=\"{0}\"", objValue);
	}

	public override string ToString()
	{
		if (objValue != null)
		{
			if (objValue is string)
			{
				return "<Value>" + objValue?.ToString() + "</Value>\n";
			}
			if (objValue is byte[])
			{
				byte[] b = (byte[])objValue;
				return "<Value>" + new Guid(b).ToString() + "</Value>\n";
			}
		}
		return "<Value></Value>\n";
	}

	private string NamespaceToPrefix(string nameSpace, XmlNode xmlNode, Dictionary<string, string> nsPairs)
	{
		if (nameSpace == null)
		{
			throw new ArgumentNullException("nameSpace");
		}
		if (xmlNode == null)
		{
			throw new ArgumentNullException("xmlNode");
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "NamespaceToPrefix: nameSpace:{0}", nameSpace);
		string text = xmlNode.GetPrefixOfNamespace(nameSpace);
		if (text.Length == 0)
		{
			string namespaceOfPrefix = xmlNode.GetNamespaceOfPrefix(text);
			if (namespaceOfPrefix == null || !namespaceOfPrefix.Equals(nameSpace, StringComparison.InvariantCultureIgnoreCase))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "NamespaceToPrefix: nameSpace {0} not found in XmlNode namespace", nameSpace);
				text = null;
			}
		}
		if (text == null && nsPairs != null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "NamespaceToPrefix: trying nsPairs");
			foreach (KeyValuePair<string, string> nsPair in nsPairs)
			{
				if (nsPair.Value != null && nsPair.Value.Equals(nameSpace, StringComparison.InvariantCultureIgnoreCase))
				{
					text = nsPair.Key;
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "NamespaceToPrefix: found prefix {0} in nsPairs", text);
					break;
				}
			}
		}
		if (text == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "NamespaceToPrefix: nameSpace{0} not found in nsPairs or XmlNode", nameSpace);
			throw new ArgumentException(AdwsStrings.MissingXMLNamespace);
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryValue", "NamespaceToPrefix: returning prefix {0}", text);
		return text;
	}
}

using System;
using System.Runtime.Serialization;
using System.Xml;

namespace Microsoft.ActiveDirectory.WebServices;

public class ObjectSerializer : XmlObjectSerializer
{
	public ObjectSerializer()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "ObjectSerializer Constructor Entered.");
	}

	public bool ArePrefixesOfNameSpacesDefined(XmlDictionaryWriter writer)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "ArePrefixesOfNameSpacesDefined Entered.");
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		string text = writer.LookupPrefix(AdwsConstants.AdNs);
		string text2 = writer.LookupPrefix(AdwsConstants.AddataNs);
		string text3 = writer.LookupPrefix(AdwsConstants.XsiNs);
		string text4 = writer.LookupPrefix(AdwsConstants.XsdNs);
		if (text == null || text2 == null || text3 == null || text4 == null)
		{
			return false;
		}
		return true;
	}

	public override bool IsStartObject(XmlDictionaryReader reader)
	{
		throw new NotImplementedException();
	}

	public override object ReadObject(XmlDictionaryReader reader, bool verifyObjectName)
	{
		throw new NotImplementedException();
	}

	public override void WriteStartObject(XmlDictionaryWriter writer, object graph)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteStartObject Entered.");
	}

	public override void WriteObjectContent(XmlDictionaryWriter writer, object graph)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteObjectContent Entered.");
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		DirectoryObject obj = graph as DirectoryObject;
		DirectoryObjectFragment directoryObjectFragment = graph as DirectoryObjectFragment;
		if (obj == null)
		{
		}
		if (!(graph is IDirectoryObject directoryObject))
		{
			return;
		}
		if (ArePrefixesOfNameSpacesDefined(writer))
		{
			writer.WriteStartElement(directoryObject.ObjectClass, AdwsConstants.AddataNs);
		}
		else
		{
			writer.WriteStartElement(AdwsConstants.AddataPrefix, directoryObject.ObjectClass, AdwsConstants.AddataNs);
			writer.WriteAttributeString("xmlns", AdwsConstants.AdPrefix, null, AdwsConstants.AdNs);
			writer.WriteAttributeString("xmlns", AdwsConstants.AddataPrefix, null, AdwsConstants.AddataNs);
			writer.WriteAttributeString("xmlns", AdwsConstants.XsiInstancePrefix, null, AdwsConstants.XsiNs);
			writer.WriteAttributeString("xmlns", AdwsConstants.XsdInstancePrefix, null, AdwsConstants.XsdNs);
		}
		if (directoryObject.ObjectIdentity != null && !directoryObject.ObjectIdentity.ObjectReferenceProperty.Equals(Guid.Empty))
		{
			DirectoryValue directoryValue = new DirectoryValue(directoryObject.ObjectIdentity.ObjectReferenceProperty);
			writer.WriteStartElement(AdwsConstants.objectReferenceProperty, AdwsConstants.AdNs);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteObjectContent: serializing Object Identity");
			directoryValue.GetXmlValue(writer, DirectorySyntaxType.UnicodeString);
			writer.WriteEndElement();
		}
		foreach (DirectoryAttribute attribute in directoryObject.Attributes)
		{
			if (attribute == null || attribute.Name == null || attribute.AttrValues.Count == 0)
			{
				continue;
			}
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteObjectContent: serializing {0}", attribute.Name);
			string[] array = attribute.Name.Split(';');
			if (array[0] != null)
			{
				writer.WriteStartElement(array[0], attribute.Xmlns);
			}
			if (attribute.Name.ToLowerInvariant().Contains(";range=") && array[1] != null)
			{
				string[] array2 = array[1].Split('=', '-');
				if (array2.Length == 3 && array2[1] != null && array2[2] != null)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteObjectContent: serializing rangeLow attribute with value {0}", array2[1]);
					writer.WriteAttributeString(AdwsConstants.rangeLow, array2[1]);
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteObjectContent: serializing rangeHigh attribute with value {0}", array2[2]);
					writer.WriteAttributeString(AdwsConstants.rangeHigh, array2[2]);
				}
			}
			writer.WriteAttributeString(AdwsConstants.LdapSyntax, attribute.Syntax.ToString());
			foreach (DirectoryValue attrValue in attribute.AttrValues)
			{
				if (attrValue != null && attrValue.Value != null)
				{
					attrValue.GetXmlValue(writer, attribute.Syntax);
				}
			}
			writer.WriteEndElement();
		}
		if (directoryObject.ParentIdentity != null)
		{
			DirectoryValue directoryValue2 = new DirectoryValue(directoryObject.ParentIdentity.ObjectReferenceProperty);
			writer.WriteStartElement(AdwsConstants.containerHierarchyParent, AdwsConstants.AdNs);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteObjectContent: serializing ParentIdentity");
			directoryValue2.GetXmlValue(writer, DirectorySyntaxType.UnicodeString);
			writer.WriteEndElement();
		}
		if (directoryObject.RDN != null)
		{
			DirectoryValue directoryValue3 = new DirectoryValue(directoryObject.RDN);
			writer.WriteStartElement(AdwsConstants.relativeDistinguishedName, AdwsConstants.AdNs);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteObjectContent: serializing Relative Distinguished Name.");
			directoryValue3.GetXmlValue(writer, DirectorySyntaxType.UnicodeString);
			writer.WriteEndElement();
		}
		if (directoryObject.DistinguishedName != null)
		{
			DirectoryValue directoryValue4 = new DirectoryValue(directoryObject.DistinguishedName);
			writer.WriteStartElement(AdwsConstants.distinguishedName, AdwsConstants.AdNs);
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteObjectContent: serializing Distinguished Name.");
			directoryValue4.GetXmlValue(writer, DirectorySyntaxType.UnicodeString);
			writer.WriteEndElement();
		}
	}

	public override void WriteEndObject(XmlDictionaryWriter writer)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ObjectSerializer", "WriteEndObject Entered.");
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		writer.WriteEndElement();
	}
}

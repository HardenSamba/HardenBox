using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

public class Items : IXmlSerializable
{
	private IList<DirectoryObjectFragment> dirObjFragsList;

	public Items()
	{
	}

	public Items(IList<DirectoryObjectFragment> dirfragments)
	{
		dirObjFragsList = dirfragments;
	}

	public void ReadXml(XmlReader reader)
	{
		throw new NotImplementedException();
	}

	public void WriteXml(XmlWriter writer)
	{
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		if (dirObjFragsList == null || dirObjFragsList.Count == 0)
		{
			return;
		}
		writer.WriteStartElement("wsen", "Items", "http://schemas.xmlsoap.org/ws/2004/09/enumeration");
		foreach (DirectoryObjectFragment dirObjFrags in dirObjFragsList)
		{
			new DirectoryObjectFragmentSerializer(dirObjFrags).WriteObject(writer, dirObjFrags);
		}
		writer.WriteEndElement();
	}

	public XmlSchema GetSchema()
	{
		return null;
	}
}

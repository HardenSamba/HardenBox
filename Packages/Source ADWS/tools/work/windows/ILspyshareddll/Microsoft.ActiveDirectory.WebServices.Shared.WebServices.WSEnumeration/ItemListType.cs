using System;
using System.Collections;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(TypeName = "ItemList", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
public class ItemListType
{
	[NonSerialized]
	private XmlNode[] anyField;

	[XmlAnyElement]
	public XmlNode[] Any
	{
		get
		{
			return anyField;
		}
		set
		{
			anyField = value;
		}
	}

	public ItemListType()
	{
	}

	public ItemListType(ICollection items)
	{
		if (items == null)
		{
			return;
		}
		XmlNode[] array = new XmlElement[items.Count];
		anyField = array;
		StringBuilder stringBuilder = new StringBuilder();
		using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, new XmlWriterSettings
		{
			OmitXmlDeclaration = true
		}))
		{
			xmlWriter.WriteStartElement("wsen", "Items", "http://schemas.xmlsoap.org/ws/2004/09/enumeration");
			foreach (object item in items)
			{
				_ = item;
			}
			xmlWriter.WriteEndElement();
		}
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.LoadXml(stringBuilder.ToString());
		for (int i = 0; i < xmlDocument.DocumentElement.ChildNodes.Count; i++)
		{
			anyField[i] = xmlDocument.DocumentElement.ChildNodes[i].CloneNode(deep: true);
		}
	}
}

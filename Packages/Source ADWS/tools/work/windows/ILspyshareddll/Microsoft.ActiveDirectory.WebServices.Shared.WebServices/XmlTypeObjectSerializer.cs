using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.XPath;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

public class XmlTypeObjectSerializer : XmlObjectSerializer
{
	private Type rootType;

	private XmlSerializer internalSerializer;

	public XmlTypeObjectSerializer(Type type)
	{
		rootType = type;
		internalSerializer = new XmlSerializer(rootType);
	}

	public override bool IsStartObject(XmlDictionaryReader reader)
	{
		return internalSerializer.CanDeserialize(reader);
	}

	internal static T ReadObject<T>(string serializedObject)
	{
		if (string.IsNullOrEmpty(serializedObject))
		{
			throw new ArgumentException("serializedObject");
		}
		XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
		using TextReader textReader = new StringReader(serializedObject);
		try
		{
			return (T)xmlSerializer.Deserialize(textReader);
		}
		catch (XmlException innerException)
		{
			throw new InvalidOperationException("Invalid XML format of filter: " + serializedObject, innerException);
		}
	}

	public override object ReadObject(XmlDictionaryReader reader, bool verifyObjectName)
	{
		object result = null;
		if (!verifyObjectName || IsStartObject(reader))
		{
			result = internalSerializer.Deserialize(reader);
		}
		return result;
	}

	public object ReadObjectFromXmlNode(IXPathNavigable xml)
	{
		if (xml == null)
		{
			return null;
		}
		using XmlReader reader = xml.CreateNavigator().ReadSubtree();
		return ReadObject(reader);
	}

	public static string WriteObject<T>(T obj, params Type[] extraTypes)
	{
		using StringWriter stringWriter = new StringWriter(CultureInfo.InvariantCulture);
		new XmlSerializer(typeof(T), extraTypes).Serialize(stringWriter, obj);
		return stringWriter.ToString();
	}

	public override void WriteEndObject(XmlDictionaryWriter writer)
	{
	}

	public override void WriteObjectContent(XmlDictionaryWriter writer, object graph)
	{
		if (graph == null)
		{
			return;
		}
		Type type = graph.GetType();
		if (rootType == type)
		{
			internalSerializer.Serialize(writer, graph);
		}
		else
		{
			if (!typeof(IList).IsAssignableFrom(type))
			{
				return;
			}
			foreach (object item in (IList)graph)
			{
				if (rootType == item.GetType())
				{
					internalSerializer.Serialize(writer, item);
				}
			}
		}
	}

	public XmlElement WriteObjectToXmlElement(object graph)
	{
		if (graph == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder))
		{
			WriteObject(xmlWriter, graph);
			xmlWriter.Flush();
		}
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.LoadXml(stringBuilder.ToString());
		return xmlDocument.DocumentElement;
	}

	public override void WriteStartObject(XmlDictionaryWriter writer, object graph)
	{
	}
}

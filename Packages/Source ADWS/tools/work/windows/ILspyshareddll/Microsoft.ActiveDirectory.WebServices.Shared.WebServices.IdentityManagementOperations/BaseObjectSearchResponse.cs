using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.Shared.Utilities;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "BaseObjectSearchResponse", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", IsNullable = false)]
public class BaseObjectSearchResponse
{
	private object[] partialAttributeField;

	private object[] controlField;

	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	[XmlAnyElement(Name = "PartialAttribute", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	public object[] PartialAttributes
	{
		get
		{
			return partialAttributeField;
		}
		set
		{
			partialAttributeField = value;
		}
	}

	[XmlAnyElement(Name = "controls", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public object[] Controls
	{
		get
		{
			return controlField;
		}
		set
		{
			controlField = value;
		}
	}

	public BaseObjectSearchResponse()
	{
	}

	private void FillNameSpaces()
	{
		xmlns = new XmlSerializerNamespaces();
		xmlns.Add("da", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess");
		xmlns.Add("ad", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
		xmlns.Add("addata", "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data");
		xmlns.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");
		xmlns.Add("xsd", "http://www.w3.org/2001/XMLSchema");
	}

	private void GetControlElementsFromXmlDoc(XmlDocument xmlDoc)
	{
		if (xmlDoc == null)
		{
			throw new ArgumentNullException("xmlDoc");
		}
		XmlNodeList elementsByTagName = xmlDoc.GetElementsByTagName("controls", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
		if (elementsByTagName[0] != null)
		{
			controlField[0] = elementsByTagName[0];
		}
	}

	public BaseObjectSearchResponse(DirectoryObjectFragment objFrag, IList<DirectoryAttribute> orderOfResults, IList<DirectoryControl> controlList)
	{
		if (objFrag == null)
		{
			throw new ArgumentNullException("objFrag");
		}
		if (orderOfResults == null)
		{
			throw new ArgumentNullException("orderOfResults");
		}
		FillNameSpaces();
		StringBuilder stringBuilder = new StringBuilder();
		using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, new XmlWriterSettings
		{
			ConformanceLevel = ConformanceLevel.Fragment,
			Encoding = Encoding.UTF8,
			CheckCharacters = false
		}))
		{
			if (orderOfResults.Count != 0)
			{
				object[] array = new XmlNode[orderOfResults.Count];
				partialAttributeField = array;
			}
			xmlWriter.WriteStartElement("dummyroot");
			new ControlSerializer(controlList, new DirectoryObjectFragmentSerializer(objFrag)).WriteObject(xmlWriter, objFrag);
			xmlWriter.WriteEndElement();
		}
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.PreserveWhitespace = true;
		xmlDocument.LoadXml(stringBuilder.ToString());
		XmlDocument xmlDocument2 = new XmlDocument();
		xmlDocument2.LoadXml(string.Format(CultureInfo.InvariantCulture, "<{0}:{1} {2}:{0}='{3}'></{0}:{1}>", "da", "PartialAttribute", "xmlns", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess"));
		for (int i = 0; i < orderOfResults.Count; i++)
		{
			XmlNode xmlNode = xmlDocument2.DocumentElement.Clone();
			if (orderOfResults[i] != null && orderOfResults[i].Name != null && orderOfResults[i].Xmlns != null)
			{
				CustomXPathContext customXPathContext = new CustomXPathContext((NameTable)xmlDocument.NameTable);
				customXPathContext.AddNamespace("adxp", "http://schemas.microsoft.com/2008/1/ActiveDirectory/XPathInternalExtensions");
				string text = "adxp:nocaseequals";
				string xpath = "/dummyroot/*/node()[" + text + "(local-name(),'" + orderOfResults[i].Name.Split(';')[0] + "') and " + text + "(namespace-uri(),'" + orderOfResults[i].Xmlns + "')]";
				XmlNodeList xmlNodeList = xmlDocument.SelectNodes(xpath, customXPathContext);
				if (xmlNodeList[0] != null)
				{
					XmlNode newChild = xmlDocument2.ImportNode(xmlNodeList[0], deep: true);
					xmlNode.AppendChild(newChild);
				}
				partialAttributeField[i] = xmlNode;
			}
		}
		if (controlList != null && controlList.Count != 0)
		{
			object[] array = new XmlNode[1];
			controlField = array;
			GetControlElementsFromXmlDoc(xmlDocument);
		}
	}

	public BaseObjectSearchResponse(DirectoryObject dirObj, IList<DirectoryControl> controlList)
	{
		if (dirObj == null)
		{
			throw new ArgumentNullException("dirObj");
		}
		StringBuilder stringBuilder = new StringBuilder();
		XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
		FillNameSpaces();
		object[] array = new XmlNode[1];
		partialAttributeField = array;
		xmlWriterSettings.ConformanceLevel = ConformanceLevel.Fragment;
		xmlWriterSettings.Encoding = Encoding.UTF8;
		xmlWriterSettings.CheckCharacters = false;
		using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder, xmlWriterSettings))
		{
			xmlWriter.WriteStartElement("dummyroot");
			xmlWriter.WriteStartElement("da", "PartialAttribute", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess");
			new ControlSerializer(null, new DirectoryObjectSerializer(dirObj)).WriteObject(xmlWriter, dirObj);
			xmlWriter.WriteEndElement();
			new ControlSerializer(controlList, null).WriteObject(xmlWriter, null);
			xmlWriter.WriteEndElement();
		}
		XmlDocument xmlDocument = new XmlDocument();
		xmlDocument.PreserveWhitespace = true;
		xmlDocument.LoadXml(stringBuilder.ToString());
		XmlNodeList elementsByTagName = xmlDocument.GetElementsByTagName("PartialAttribute", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess");
		if (elementsByTagName[0] != null)
		{
			partialAttributeField[0] = elementsByTagName[0];
		}
		if (controlList != null && controlList.Count != 0)
		{
			array = new XmlNode[1];
			controlField = array;
			GetControlElementsFromXmlDoc(xmlDocument);
		}
	}
}

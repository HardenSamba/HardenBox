using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Runtime.Serialization;
using System.Xml;

namespace Microsoft.ActiveDirectory.WebServices;

public class ControlSerializer : XmlObjectSerializer
{
	private IList<DirectoryControl> ctrlList;

	private XmlObjectSerializer objSerializer;

	public ControlSerializer(IList<DirectoryControl> controlList, XmlObjectSerializer serializer)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ControlSerializer", "ControlSerializer constructor Entered.");
		ctrlList = controlList;
		objSerializer = serializer;
	}

	public bool ArePrefixesOfNameSpacesDefined(XmlDictionaryWriter writer)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ControlSerializer", "ArePrefixesOfNameSpacesDefined Entered.");
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		string text = writer.LookupPrefix(AdwsConstants.AdNs);
		string text2 = writer.LookupPrefix(AdwsConstants.XsiNs);
		string text3 = writer.LookupPrefix(AdwsConstants.XsdNs);
		if (text == null || text2 == null || text3 == null)
		{
			return false;
		}
		return true;
	}

	private void SerializeControl(XmlDictionaryWriter writer)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ControlSerializer", "SerializeControl Entered.");
		bool flag = ArePrefixesOfNameSpacesDefined(writer);
		if (ctrlList == null || ctrlList.Count == 0)
		{
			return;
		}
		if (flag)
		{
			writer.WriteStartElement(AdwsConstants.Controls, AdwsConstants.AdNs);
		}
		else
		{
			writer.WriteStartElement(AdwsConstants.AdPrefix, AdwsConstants.Controls, AdwsConstants.AdNs);
			writer.WriteAttributeString(AdwsConstants.XmlNs, AdwsConstants.AdPrefix, null, AdwsConstants.AdNs);
			writer.WriteAttributeString(AdwsConstants.XmlNs, AdwsConstants.XsiInstancePrefix, null, AdwsConstants.XsiNs);
			writer.WriteAttributeString(AdwsConstants.XmlNs, AdwsConstants.XsdInstancePrefix, null, AdwsConstants.XsdNs);
		}
		foreach (DirectoryControl ctrl in ctrlList)
		{
			writer.WriteStartElement(AdwsConstants.Control, AdwsConstants.AdNs);
			writer.WriteAttributeString(AdwsConstants.CtrlType, ctrl.Type.ToString().ToLower());
			writer.WriteAttributeString(AdwsConstants.CtrlCriticality, ctrl.IsCritical.ToString().ToLower());
			if (ctrl.GetValue().Length != 0)
			{
				writer.WriteStartElement(AdwsConstants.CtrlValue, AdwsConstants.AdNs);
				writer.WriteAttributeString(AdwsConstants.CtrlType, AdwsConstants.XsiNs, AdwsConstants.XsdInstancePrefix + ":base64Binary");
				writer.WriteBase64(ctrl.GetValue(), 0, ctrl.GetValue().Length);
				writer.WriteEndElement();
			}
			writer.WriteEndElement();
		}
		writer.WriteEndElement();
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
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ControlSerializer", "WriteStartObject Entered.");
	}

	public override void WriteObjectContent(XmlDictionaryWriter writer, object graph)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ControlSerializer", "WriteObjectContent Entered.");
		if (graph == null)
		{
			SerializeControl(writer);
		}
		else if (objSerializer != null)
		{
			objSerializer.WriteObject(writer, graph);
			SerializeControl(writer);
		}
	}

	public override void WriteEndObject(XmlDictionaryWriter writer)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ControlSerializer", "WriteEndObject Entered.");
	}
}

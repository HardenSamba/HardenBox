using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

public class PullControls : IXmlSerializable
{
	private IList<DirectoryControl> pullControlList;

	public PullControls()
	{
	}

	public PullControls(IList<DirectoryControl> controls)
	{
		pullControlList = controls;
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
		new ControlSerializer(pullControlList, null).WriteObject(writer, null);
	}

	public XmlSchema GetSchema()
	{
		return null;
	}
}

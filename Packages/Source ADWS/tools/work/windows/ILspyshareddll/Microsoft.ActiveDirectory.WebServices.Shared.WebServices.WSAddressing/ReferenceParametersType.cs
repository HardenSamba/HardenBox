using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSAddressing;

[Serializable]
[XmlType(TypeName = "ReferenceParametersType", Namespace = "http://www.w3.org/2005/08/addressing")]
[XmlRoot(ElementName = "ReferenceParameters", Namespace = "http://www.w3.org/2005/08/addressing", IsNullable = false)]
public class ReferenceParametersType
{
	private Guid objectRefPropertyField;

	private string instanceField;

	[XmlElement(ElementName = "objectReferenceProperty", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public Guid ObjectReferenceProperty
	{
		get
		{
			return objectRefPropertyField;
		}
		set
		{
			objectRefPropertyField = value;
		}
	}

	[XmlElement(ElementName = "instance", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	public string Instance
	{
		get
		{
			return instanceField;
		}
		set
		{
			instanceField = value;
		}
	}

	public ReferenceParametersType()
	{
		objectRefPropertyField = Guid.Empty;
		instanceField = null;
	}

	public ReferenceParametersType(Guid objectId, string instance)
	{
		if (instance == null)
		{
			throw new ArgumentNullException("instance");
		}
		objectRefPropertyField = objectId;
		instanceField = instance;
	}
}

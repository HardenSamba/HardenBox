using System;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "Pull", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration", IsNullable = false)]
public class Pull
{
	private Guid enumerationContextField = Guid.Empty;

	private string maxTimeField;

	private string maxElementsField;

	private string maxCharactersField;

	private DirControl[] controlField;

	[XmlNamespaceDeclarations]
	public XmlSerializerNamespaces xmlns;

	[XmlElement(ElementName = "EnumerationContext", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public Guid EnumerationContext
	{
		get
		{
			return enumerationContextField;
		}
		set
		{
			enumerationContextField = value;
		}
	}

	[XmlElement(ElementName = "MaxTime", DataType = "duration", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public string MaxTime
	{
		get
		{
			return maxTimeField;
		}
		set
		{
			maxTimeField = value;
		}
	}

	[XmlElement(ElementName = "MaxElements", DataType = "positiveInteger", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public string MaxElements
	{
		get
		{
			return maxElementsField;
		}
		set
		{
			maxElementsField = value;
		}
	}

	[XmlElement(ElementName = "MaxCharacters", DataType = "positiveInteger", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	public string MaxCharacters
	{
		get
		{
			return maxCharactersField;
		}
		set
		{
			maxCharactersField = value;
		}
	}

	[XmlArray(ElementName = "controls", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	[XmlArrayItem(ElementName = "control", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory", Type = typeof(DirControl), IsNullable = false)]
	public DirControl[] Controls
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
}

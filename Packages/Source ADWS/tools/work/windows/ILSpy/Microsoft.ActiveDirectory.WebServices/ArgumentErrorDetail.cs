using System;
using System.Xml.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
[XmlRoot(ElementName = "ArgumentError", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory", IsNullable = true)]
public class ArgumentErrorDetail
{
	private string message;

	private string shortMessage;

	private string parameterName;

	[XmlElement(ElementName = "Message")]
	public string Message
	{
		get
		{
			return message;
		}
		set
		{
			message = value;
		}
	}

	[XmlElement(ElementName = "ParameterName")]
	public string ParameterName
	{
		get
		{
			return parameterName;
		}
		set
		{
			parameterName = value;
		}
	}

	[XmlElement(ElementName = "ShortMessage")]
	public string ShortMessage
	{
		get
		{
			return shortMessage;
		}
		set
		{
			shortMessage = value;
		}
	}

	public ArgumentErrorDetail()
	{
		message = null;
		shortMessage = null;
		parameterName = null;
	}

	public ArgumentErrorDetail(ArgumentException e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		message = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			shortMessage = value;
		}
		else
		{
			shortMessage = WebServiceConstants.ArgumentException;
		}
		parameterName = e.ParamName;
	}
}

using System;
using System.Runtime.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

namespace Microsoft.ActiveDirectory.WebServices;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public class ArgumentErrorDetailCA
{
	private string message;

	private string shortMessage;

	private string parameterName;

	[DataMember(IsRequired = false)]
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

	[DataMember(IsRequired = false)]
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

	[DataMember(IsRequired = false)]
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

	public ArgumentErrorDetailCA()
	{
		message = null;
		shortMessage = null;
		parameterName = null;
	}

	public ArgumentErrorDetailCA(ArgumentException e)
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

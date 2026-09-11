using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class NoConnectionAvailableException : ApplicationException
{
	public NoConnectionAvailableException()
		: this(AdwsStrings.NoConnectionAvailable)
	{
	}

	public NoConnectionAvailableException(string exceptionText)
		: base(exceptionText)
	{
	}

	public NoConnectionAvailableException(string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
	}

	protected NoConnectionAvailableException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

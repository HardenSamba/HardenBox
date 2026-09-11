using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class ObjectCreatedButIdentityUnknownException : ApplicationException
{
	public ObjectCreatedButIdentityUnknownException()
		: this(AdwsStrings.ObjectCreatedButIdentityUnknown)
	{
	}

	public ObjectCreatedButIdentityUnknownException(string exceptionText)
		: base(exceptionText)
	{
	}

	public ObjectCreatedButIdentityUnknownException(Exception innerException)
		: base(AdwsStrings.ObjectCreatedButIdentityUnknown, innerException)
	{
	}

	public ObjectCreatedButIdentityUnknownException(string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
	}

	protected ObjectCreatedButIdentityUnknownException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

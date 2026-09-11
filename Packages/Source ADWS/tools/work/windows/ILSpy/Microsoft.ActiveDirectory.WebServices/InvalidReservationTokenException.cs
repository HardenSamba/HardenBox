using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class InvalidReservationTokenException : ApplicationException
{
	public InvalidReservationTokenException()
		: this(AdwsStrings.InvalidReservationToken)
	{
	}

	public InvalidReservationTokenException(string exceptionText)
		: base(exceptionText)
	{
	}

	public InvalidReservationTokenException(string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
	}

	protected InvalidReservationTokenException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

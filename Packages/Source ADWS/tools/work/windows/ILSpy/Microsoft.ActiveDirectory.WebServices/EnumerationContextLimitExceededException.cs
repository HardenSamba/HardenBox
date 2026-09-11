using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class EnumerationContextLimitExceededException : ApplicationException
{
	public EnumerationContextLimitExceededException()
		: this(AdwsStrings.MaxEnumCtxsTotalReached)
	{
	}

	public EnumerationContextLimitExceededException(string exceptionText)
		: base(exceptionText)
	{
	}

	public EnumerationContextLimitExceededException(string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
	}

	protected EnumerationContextLimitExceededException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class UnknownAttributeException : ApplicationException
{
	private string unknownAttribute;

	public string UnknownAttribute => unknownAttribute;

	public UnknownAttributeException(string attribute)
		: this(attribute, AdwsStrings.UnknownAttribute)
	{
	}

	public UnknownAttributeException(string attribute, string exceptionText)
		: base(exceptionText)
	{
		unknownAttribute = attribute;
	}

	public UnknownAttributeException(string attribute, string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
		unknownAttribute = attribute;
	}

	protected UnknownAttributeException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

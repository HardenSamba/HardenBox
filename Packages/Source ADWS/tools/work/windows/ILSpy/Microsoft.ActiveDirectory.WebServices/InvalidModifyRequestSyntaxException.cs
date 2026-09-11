using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class InvalidModifyRequestSyntaxException : ApplicationException
{
	private string expression;

	public string Expression => expression;

	public InvalidModifyRequestSyntaxException()
		: this(AdwsStrings.InvalidPutSyntax, string.Empty)
	{
	}

	public InvalidModifyRequestSyntaxException(string exceptionText)
		: this(exceptionText, string.Empty)
	{
	}

	public InvalidModifyRequestSyntaxException(string exceptionText, string expression)
		: base(exceptionText)
	{
		this.expression = expression;
	}

	public InvalidModifyRequestSyntaxException(string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
		expression = string.Empty;
	}

	public InvalidModifyRequestSyntaxException(string exceptionText, string expression, Exception innerException)
		: base(exceptionText, innerException)
	{
		this.expression = expression;
	}

	protected InvalidModifyRequestSyntaxException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

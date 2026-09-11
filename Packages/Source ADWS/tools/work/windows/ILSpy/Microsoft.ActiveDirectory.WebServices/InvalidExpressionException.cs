using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
public class InvalidExpressionException : ApplicationException
{
	private string expression;

	private InvalidExpressionDetailType reason;

	public string Expression => expression;

	public InvalidExpressionDetailType Reason => reason;

	public InvalidExpressionException(InvalidExpressionDetailType reason, string expression)
		: this(AdwsStrings.InvalidExpression, reason, expression)
	{
	}

	public InvalidExpressionException(string exceptionText, InvalidExpressionDetailType reason, string expression)
		: base(exceptionText)
	{
		this.reason = reason;
		this.expression = expression;
	}

	public InvalidExpressionException(InvalidExpressionDetailType reason, string expression, Exception innerException)
		: this(AdwsStrings.InvalidExpression, reason, expression, innerException)
	{
	}

	public InvalidExpressionException(string exceptionText, InvalidExpressionDetailType reason, string expression, Exception innerException)
		: base(exceptionText, innerException)
	{
		this.reason = reason;
		this.expression = expression;
	}

	protected InvalidExpressionException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

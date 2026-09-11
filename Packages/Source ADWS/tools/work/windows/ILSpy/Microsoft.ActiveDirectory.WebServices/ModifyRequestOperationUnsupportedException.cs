using System;
using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class ModifyRequestOperationUnsupportedException : ApplicationException
{
	private string operation;

	public string Operation => operation;

	public ModifyRequestOperationUnsupportedException()
		: this(AdwsStrings.PutOperationUnsupported, string.Empty)
	{
	}

	public ModifyRequestOperationUnsupportedException(string exceptionText)
		: this(exceptionText, string.Empty)
	{
	}

	public ModifyRequestOperationUnsupportedException(string exceptionText, string operation)
		: base(exceptionText)
	{
		this.operation = operation;
	}

	public ModifyRequestOperationUnsupportedException(string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
		operation = string.Empty;
	}

	public ModifyRequestOperationUnsupportedException(string exceptionText, string operation, Exception innerException)
		: base(exceptionText, innerException)
	{
		this.operation = operation;
	}

	protected ModifyRequestOperationUnsupportedException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

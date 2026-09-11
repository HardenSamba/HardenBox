using System;
using System.Runtime.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class PutException : ApplicationException
{
	private ChangeType putChange;

	public ChangeType PutChange => putChange;

	public PutException()
		: this(AdwsStrings.BadPutOrCreateValue)
	{
	}

	public PutException(string exceptionText)
		: base(exceptionText)
	{
		putChange = null;
	}

	public PutException(string exceptionText, ChangeType putChange)
		: base(exceptionText)
	{
		this.putChange = putChange;
	}

	public PutException(string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
		putChange = null;
	}

	public PutException(string exceptionText, ChangeType putChange, Exception innerException)
		: base(exceptionText, innerException)
	{
		this.putChange = putChange;
	}

	protected PutException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

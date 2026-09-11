using System;
using System.Runtime.Serialization;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

namespace Microsoft.ActiveDirectory.WebServices;

[Serializable]
internal class CreateException : ApplicationException
{
	private AttributeTypeAndValueType attributeTypeAndValue;

	public AttributeTypeAndValueType AttributeTypeAndValue => attributeTypeAndValue;

	public CreateException()
		: this(AdwsStrings.CreateMissingValues)
	{
	}

	public CreateException(string exceptionText)
		: base(exceptionText)
	{
		attributeTypeAndValue = null;
	}

	public CreateException(string exceptionText, AttributeTypeAndValueType attributeTypeAndValue)
		: base(exceptionText)
	{
		this.attributeTypeAndValue = attributeTypeAndValue;
	}

	public CreateException(string exceptionText, Exception innerException)
		: base(exceptionText, innerException)
	{
		attributeTypeAndValue = null;
	}

	public CreateException(string exceptionText, AttributeTypeAndValueType attributeTypeAndValue, Exception innerException)
		: base(exceptionText, innerException)
	{
		this.attributeTypeAndValue = attributeTypeAndValue;
	}

	protected CreateException(SerializationInfo info, StreamingContext context)
		: base(info, context)
	{
	}
}

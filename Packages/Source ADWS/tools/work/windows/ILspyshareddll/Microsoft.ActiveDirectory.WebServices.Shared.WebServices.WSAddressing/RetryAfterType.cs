using System;
using System.Xml.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSAddressing;

[Serializable]
[XmlType(AnonymousType = true)]
[XmlRoot(ElementName = "RetryAfter", Namespace = "http://www.w3.org/2005/08/addressing", IsNullable = true)]
public class RetryAfterType
{
	private string suggestedDurationField;

	[XmlText(DataType = "positiveInteger")]
	public string SuggestedDuration
	{
		get
		{
			return suggestedDurationField;
		}
		set
		{
			suggestedDurationField = value;
		}
	}
}

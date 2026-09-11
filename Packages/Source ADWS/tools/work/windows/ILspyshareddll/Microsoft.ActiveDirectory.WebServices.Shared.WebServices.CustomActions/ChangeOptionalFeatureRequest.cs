using System;
using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class ChangeOptionalFeatureRequest
{
	[MessageHeader(MustUnderstand = true)]
	public string Server;

	[MessageBodyMember]
	public Guid FeatureId;

	[MessageBodyMember]
	public string DistinguishedName;

	[MessageBodyMember]
	public bool Enable;
}

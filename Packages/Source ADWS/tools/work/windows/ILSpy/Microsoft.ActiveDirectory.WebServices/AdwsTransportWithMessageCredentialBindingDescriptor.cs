using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices;

internal class AdwsTransportWithMessageCredentialBindingDescriptor : AdwsBindingDescriptor
{
	private MessageCredentialType messageCredentialType;

	public MessageCredentialType MessageCredentialType => messageCredentialType;

	public AdwsTransportWithMessageCredentialBindingDescriptor(string endpointPrefix, MessageCredentialType messageCredentialType)
		: base(endpointPrefix)
	{
		this.messageCredentialType = messageCredentialType;
	}
}

using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices;

internal class AdwsTransportBindingDescriptor : AdwsBindingDescriptor
{
	private TcpClientCredentialType tcpClientCredentialType;

	public TcpClientCredentialType TcpClientCredentialType => tcpClientCredentialType;

	public AdwsTransportBindingDescriptor(string endpointPrefix, TcpClientCredentialType tcpClientCredentialType)
		: base(endpointPrefix)
	{
		this.tcpClientCredentialType = tcpClientCredentialType;
	}
}

namespace Microsoft.ActiveDirectory.WebServices;

internal abstract class AdwsBindingDescriptor
{
	private string endpointPrefix;

	public string EndpointPrefix => endpointPrefix;

	public AdwsBindingDescriptor(string endpointPrefix)
	{
		this.endpointPrefix = endpointPrefix;
	}
}

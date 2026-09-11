using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

public class IdentityManagementMessageInspector : IDispatchMessageInspector
{
	public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
	{
		if (request == null)
		{
			throw new ArgumentNullException("request");
		}
		if (request.Headers != null && request.Headers.UnderstoodHeaders != null)
		{
			if (string.Equals(request.Headers.Action, "http://schemas.xmlsoap.org/ws/2004/09/transfer/Delete", StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}
			int num = request.Headers.FindHeader("IdentityManagementOperation", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess");
			if (-1 != num && !request.Headers.UnderstoodHeaders.Contains(request.Headers[num]))
			{
				request.Headers.UnderstoodHeaders.Add(request.Headers[num]);
			}
		}
		return null;
	}

	public void BeforeSendReply(ref Message reply, object correlationState)
	{
	}
}

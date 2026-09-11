using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

public class ObjectIdAndInstanceInformationInspector : IDispatchMessageInspector
{
	public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
	{
		if (request == null)
		{
			throw new ArgumentNullException("request");
		}
		if (request.Headers != null && request.Headers.UnderstoodHeaders != null)
		{
			int num = request.Headers.FindHeader("objectReferenceProperty", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
			if (-1 != num && !string.Equals(request.Headers.Action, "http://schemas.xmlsoap.org/ws/2004/09/transfer/Create", StringComparison.OrdinalIgnoreCase) && !string.Equals(request.Headers.Action, "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Enumerate", StringComparison.OrdinalIgnoreCase) && !request.Headers.UnderstoodHeaders.Contains(request.Headers[num]))
			{
				request.Headers.UnderstoodHeaders.Add(request.Headers[num]);
			}
			num = request.Headers.FindHeader("instance", "http://schemas.microsoft.com/2008/1/ActiveDirectory");
			if (-1 != num && !request.Headers.UnderstoodHeaders.Contains(request.Headers[num]))
			{
				request.Headers.UnderstoodHeaders.Add(request.Headers[num]);
			}
		}
		return null;
	}

	public void BeforeSendReply(ref Message reply, object correlationState)
	{
		if (reply == null)
		{
			throw new ArgumentNullException("reply");
		}
	}
}

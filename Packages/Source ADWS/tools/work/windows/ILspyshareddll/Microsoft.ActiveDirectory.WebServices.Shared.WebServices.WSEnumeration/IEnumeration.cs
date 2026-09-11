using System.Net.Security;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;

[ServiceContract(SessionMode = SessionMode.Required, ProtectionLevel = ProtectionLevel.EncryptAndSign, Name = "Search", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
public interface IEnumeration
{
	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "Enumerate", Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Enumerate", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/EnumerateResponse")]
	[FaultContract(typeof(EnumerateFault), Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", Name = "CannotProcessFilter", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	[FaultContract(typeof(SupportedDialect), Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", Name = "FilterDialectRequestedUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	[FaultContract(typeof(EnumerateFault), Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", Name = "InvalidExpirationTime", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "DestinationUnreachable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(EnumerateFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "InvalidSortKey", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	[FaultContract(typeof(EnumerateFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "EnumerationContextLimitExceeded", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	[FaultContract(typeof(SupportedSelectOrSortDialect), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "UnsupportedSelectOrSortDialectFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	[FaultContract(typeof(EnumerateFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "InvalidPropertyFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	Message Enumerate(Message request);

	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "Pull", Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Pull", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/PullResponse")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "DestinationUnreachable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", Name = "InvalidEnumerationContext", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	[FaultContract(typeof(PullFault), Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", Name = "TimedOut", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	[FaultContract(typeof(PullFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "MaxCharsNotSupported", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	[FaultContract(typeof(PullFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "MaxTimeExceedsLimit", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory")]
	Message Pull(Message request);

	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "Renew", Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Renew", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/RenewResponse")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", Name = "InvalidEnumerationContext", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	[FaultContract(typeof(RenewFault), Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", Name = "UnableToRenew", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	Message Renew(Message request);

	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "GetStatus", Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/GetStatus", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/GetStatusResponse")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", Name = "InvalidEnumerationContext", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/enumeration")]
	Message GetStatus(Message request);

	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "Release", Action = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Release", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/ReleaseResponse")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	Message Release(Message request);
}

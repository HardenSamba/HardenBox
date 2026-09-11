using System.Net.Security;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

[ServiceContract(SessionMode = SessionMode.Required, ProtectionLevel = ProtectionLevel.EncryptAndSign, Name = "ResourceFactory", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/transfer")]
public interface IResourceFactory
{
	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "Create", Action = "http://schemas.xmlsoap.org/ws/2004/09/transfer/Create", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/transfer/CreateResponse")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "DestinationUnreachable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(FaultDetail), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "EncodingLimit", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(FragmentDialect), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "FragmentDialectNotSupported", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/09/transfer/fault", Name = "InvalidRepresentation", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/transfer")]
	[FaultContract(typeof(AttributeTypeNotValid), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "CannotProcessFilter", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "ActionNotSupportedFault", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "SchemaValidationError", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "AlreadyExists", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "AccessDenied", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", Name = "UnwillingToPerform", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	Message Create(Message request);
}

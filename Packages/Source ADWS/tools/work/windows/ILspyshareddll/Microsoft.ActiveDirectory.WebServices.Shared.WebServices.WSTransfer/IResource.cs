using System.Net.Security;
using System.ServiceModel;
using System.ServiceModel.Channels;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

[ServiceContract(SessionMode = SessionMode.Required, ProtectionLevel = ProtectionLevel.EncryptAndSign, Name = "Resource", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/transfer")]
public interface IResource
{
	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "Get", Action = "http://schemas.xmlsoap.org/ws/2004/09/transfer/Get", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/transfer/GetResponse")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "DestinationUnreachable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(FaultDetail), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "EncodingLimit", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(FragmentDialect), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "FragmentDialectNotSupported", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(AttributeTypeNotValid), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "CannotProcessFilter", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "SchemaValidationError", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "AccessDenied", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", Name = "UnwillingToPerform", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	Message Get(Message request);

	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "Put", Action = "http://schemas.xmlsoap.org/ws/2004/09/transfer/Put", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/transfer/PutResponse")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "DestinationUnreachable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(FaultDetail), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "EncodingLimit", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(FragmentDialect), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "FragmentDialectNotSupported", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(AttributeTypeNotValid), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "CannotProcessFilter", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/09/transfer/fault", Name = "InvalidRepresentation", Namespace = "http://schemas.xmlsoap.org/ws/2004/09/transfer")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "SchemaValidationError", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "ActionNotSupportedFault", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "AccessDenied", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", Name = "UnwillingToPerform", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	Message Put(Message request);

	[XmlSerializerFormat(SupportFaults = true)]
	[OperationContract(Name = "Delete", Action = "http://schemas.xmlsoap.org/ws/2004/09/transfer/Delete", ReplyAction = "http://schemas.xmlsoap.org/ws/2004/09/transfer/DeleteResponse")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "DestinationUnreachable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "AccessDenied", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", Name = "SchemaValidationError", Namespace = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd")]
	[FaultContract(typeof(Fault), Action = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", Name = "EndpointUnavailable", Namespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing")]
	[FaultContract(typeof(Fault), Action = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", Name = "UnwillingToPerform", Namespace = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess")]
	Message Delete(Message request);
}

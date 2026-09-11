using System;
using System.ComponentModel;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.IdentityManagementOperations;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

namespace Microsoft.ActiveDirectory.WebServices;

internal static class FaultGenerator
{
	private static FaultException GenerateFault(string faultAction, string faultName, string faultNamespace, string faultReason, bool receiverFault)
	{
		return new FaultException(CreateMessageFault(faultName, faultNamespace, faultReason, receiverFault), faultAction);
	}

	private static FaultException GenerateFault<T>(string faultAction, string faultName, string faultNamespace, string faultReason, bool receiverFault, T faultDetail)
	{
		return new FaultException(CreateMessageFault(faultName, faultNamespace, faultReason, receiverFault, faultDetail), faultAction);
	}

	private static FaultException GenerateFault<T>(string faultAction, string faultName, string faultNamespace, string faultReason, bool receiverFault, T[] faultDetail)
	{
		return new FaultException(CreateMessageFault(faultName, faultNamespace, faultReason, receiverFault, faultDetail), faultAction);
	}

	private static MessageFault CreateMessageFault(string faultName, string faultNamespace, string faultReason, bool receiverFault)
	{
		return MessageFault.CreateFault(receiverFault ? FaultCode.CreateReceiverFaultCode(faultName, faultNamespace) : FaultCode.CreateSenderFaultCode(faultName, faultNamespace), new FaultReason(new FaultReasonText(faultReason, CultureInfo.CurrentUICulture)));
	}

	private static MessageFault CreateMessageFault<T>(string faultName, string faultNamespace, string faultReason, bool receiverFault, T faultDetail)
	{
		return MessageFault.CreateFault(receiverFault ? FaultCode.CreateReceiverFaultCode(faultName, faultNamespace) : FaultCode.CreateSenderFaultCode(faultName, faultNamespace), new FaultReason(new FaultReasonText(faultReason, CultureInfo.CurrentUICulture)), faultDetail, new XmlTypeObjectSerializer(typeof(T)));
	}

	private static MessageFault CreateMessageFault<T>(string faultName, string faultNamespace, string faultReason, bool receiverFault, T[] faultDetail)
	{
		return MessageFault.CreateFault(receiverFault ? FaultCode.CreateReceiverFaultCode(faultName, faultNamespace) : FaultCode.CreateSenderFaultCode(faultName, faultNamespace), new FaultReason(new FaultReasonText(faultReason, CultureInfo.CurrentUICulture)), faultDetail, new XmlTypeObjectSerializer(typeof(T)));
	}

	private static FaultException GenerateCustomActionFaultMessage<T>(string faultAction, string faultName, string faultNamespace, string faultReason, bool receiverFault, T faultDetail)
	{
		return new FaultException(MessageFault.CreateFault(receiverFault ? FaultCode.CreateReceiverFaultCode(faultName, faultNamespace) : FaultCode.CreateSenderFaultCode(faultName, faultNamespace), new FaultReason(new FaultReasonText(faultReason, CultureInfo.CurrentUICulture)), faultDetail), faultAction);
	}

	internal static FaultException GenerateActionNotSupportedFault(string faultDetail)
	{
		Fault fault = new Fault();
		fault.Error = faultDetail;
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "ActionNotSupportedFault", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.ActionNotSupportedReason, receiverFault: false, fault);
	}

	internal static FaultException GenerateFragmentDialectNotSupportedFault(OperationType requestType)
	{
		string[] supportedDialects = DialectParser.GetSupportedDialects(requestType);
		FragmentDialect[] array = new FragmentDialect[supportedDialects.Length];
		for (int i = 0; i < supportedDialects.Length; i++)
		{
			array[i] = new FragmentDialect(supportedDialects[i]);
		}
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "FragmentDialectNotSupported", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.DialectNotSupported, receiverFault: false, array);
	}

	internal static FaultException GenerateDestinationUnreachableFault(DirectoryOperationException e)
	{
		Fault fault = new Fault();
		fault.DirectoryError = new DirectoryErrorDetail(e);
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "DestinationUnreachable", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.DestinationUnreachable, receiverFault: true, fault);
	}

	internal static FaultException GenerateEndpointUnavailableFault(LdapException e)
	{
		Fault fault = new Fault();
		fault.DirectoryError = new DirectoryErrorDetail(e);
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "EndpointUnavailable", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.LdapError, receiverFault: true, fault);
	}

	internal static FaultException GenerateEndpointUnavailableFault(DirectoryOperationException e)
	{
		Fault fault = new Fault();
		fault.DirectoryError = new DirectoryErrorDetail(e);
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "EndpointUnavailable", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.DirectoryError, receiverFault: true, fault);
	}

	internal static FaultException GenerateEndpointUnavailableFault(ArgumentException e)
	{
		Fault fault = new Fault();
		fault.ArgumentError = new ArgumentErrorDetail(e);
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "EndpointUnavailable", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.ArgumentError, receiverFault: false, fault);
	}

	internal static FaultException GenerateEndpointUnavailableFault(NoConnectionAvailableException e)
	{
		Fault fault = new Fault();
		fault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			fault.ShortError = WebServiceConstants.NoConnectionAvailableException;
		}
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "EndpointUnavailable", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.EndpointUnableToProcessAtThisTime, receiverFault: true, fault);
	}

	internal static FaultException GenerateSchemaValidationErrorForSerialization(SystemException e)
	{
		Fault fault = new Fault();
		fault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			if (e is InvalidOperationException)
			{
				fault.ShortError = WebServiceConstants.InvalidOperationException;
			}
			if (e is XmlException)
			{
				fault.ShortError = WebServiceConstants.XmlException;
			}
			if (e is SerializationException)
			{
				fault.ShortError = WebServiceConstants.SerializationException;
			}
		}
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "SchemaValidationError", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.SerializationError, receiverFault: false, fault);
	}

	internal static FaultException GenerateEndpointUnavailableForSerialization(SystemException e)
	{
		Fault fault = new Fault();
		fault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			if (e is InvalidOperationException)
			{
				fault.ShortError = WebServiceConstants.InvalidOperationException;
			}
			if (e is XmlException)
			{
				fault.ShortError = WebServiceConstants.XmlException;
			}
			if (e is SerializationException)
			{
				fault.ShortError = WebServiceConstants.SerializationException;
			}
		}
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "EndpointUnavailable", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.SerializationError, receiverFault: false, fault);
	}

	internal static FaultException GenerateCannotProcessFilterFault(InvalidExpressionException e)
	{
		if (e.Reason == InvalidExpressionDetailType.InvalidExpressionSyntax)
		{
			return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "CannotProcessFilter", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.InvalidExpression, receiverFault: false, new AttributeTypeNotValid(new AttributeTypeNotValidForDialect(e), null));
		}
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "CannotProcessFilter", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.InvalidExpression, receiverFault: false, new AttributeTypeNotValid(null, new AttributeTypeNotValidForEntry(e)));
	}

	internal static FaultException GenerateEncodingLimitFault(EncodingLimitExceededDetailType part)
	{
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "EncodingLimit", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.EncodingLimitExceeded, receiverFault: false, new FaultDetail(part, "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/RequestSizeLimitExceeded"));
	}

	internal static FaultException GenerateUnwillingToPerformFault(LdapException e)
	{
		Fault fault = new Fault();
		fault.DirectoryError = new DirectoryErrorDetail(e);
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.LdapError, receiverFault: false, fault);
	}

	internal static FaultException GenerateUnwillingToPerformFault(DirectoryOperationException e)
	{
		Fault fault = new Fault();
		fault.DirectoryError = new DirectoryErrorDetail(e);
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.DirectoryError, receiverFault: false, fault);
	}

	internal static FaultException GenerateUnwillingToPerformFault(ArgumentException e)
	{
		Fault fault = new Fault();
		fault.ArgumentError = new ArgumentErrorDetail(e);
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.ArgumentError, receiverFault: false, fault);
	}

	internal static FaultException GenerateUnwillingToPerformFaultForReadObject(InvalidOperationException e)
	{
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.ReadObjectError, receiverFault: false, new Fault(null, e.Message, null));
	}

	internal static FaultException GenerateCannotProcessFilterFaultForReadObject(UnknownAttributeException e)
	{
		Fault fault = new Fault();
		fault.Error = string.Format(CultureInfo.CurrentUICulture, e.Message, new object[1] { e.UnknownAttribute });
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			fault.ShortError = WebServiceConstants.UnknownAttributeException;
		}
		fault.UnknownAttribute = e.UnknownAttribute;
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "CannotProcessFilter", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.UnknownAttributeError, receiverFault: false, fault);
	}

	internal static FaultException GenerateUnwillingToPerformFault(PutException e)
	{
		Fault fault = new Fault(null, e.PutChange, null);
		fault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			fault.ShortError = WebServiceConstants.PutException;
		}
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.BadPutOrCreateValueReason, receiverFault: false, fault);
	}

	internal static FaultException GenerateUnwillingToPerformForUpdateObject(InvalidOperationException e)
	{
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.RequestedToModifyReservedAttributes, receiverFault: false, new Fault(null, e.Message, null));
	}

	internal static FaultException GenerateSchemaValidationErrorFault(InvalidModifyRequestSyntaxException e)
	{
		Fault fault = new Fault(e.Expression, null, null);
		fault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			fault.ShortError = WebServiceConstants.InvalidModifyRequestSyntaxException;
		}
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "SchemaValidationError", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.InvalidPutSyntaxReason, receiverFault: false, fault);
	}

	internal static FaultException GenerateSchemaValidationErrorFault(ModifyRequestOperationUnsupportedException e)
	{
		Fault fault = new Fault(null, null, e.Operation);
		fault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			fault.ShortError = WebServiceConstants.ModifyRequestOperationUnsupportedException;
		}
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "SchemaValidationError", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.PutOperationUnsupportedReason, receiverFault: false, fault);
	}

	internal static FaultException GenerateInvalidRepresentationFaultConstraintViolation(DirectoryOperationException e)
	{
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/transfer/fault", "InvalidRepresentation", "http://schemas.xmlsoap.org/ws/2004/09/transfer", AdwsStrings.ConstraintViolation, receiverFault: false, new Fault(null, null, new DirectoryErrorDetail(e)));
	}

	internal static FaultException GenerateInvalidRepresentationFaultAttrExists(DirectoryOperationException e)
	{
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/transfer/fault", "InvalidRepresentation", "http://schemas.xmlsoap.org/ws/2004/09/transfer", AdwsStrings.SuppliedAttributeAlreadyExists, receiverFault: false, new Fault(null, null, new DirectoryErrorDetail(e)));
	}

	internal static FaultException GenerateInvalidRepresentationFault(DirectoryOperationException e)
	{
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/transfer/fault", "InvalidRepresentation", "http://schemas.xmlsoap.org/ws/2004/09/transfer", AdwsStrings.SuppliedRepresentationIsInvalid, receiverFault: false, new Fault(null, null, new DirectoryErrorDetail(e)));
	}

	internal static FaultException GenerateEntryAlreadyExistsFault(DirectoryOperationException e)
	{
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "AlreadyExists", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.SuppliedEntryAlreadyExists, receiverFault: false, new Fault(null, null, new DirectoryErrorDetail(e)));
	}

	internal static FaultException GenerateAccessDeniedFault(DirectoryOperationException e)
	{
		return GenerateFault("http://schemas.dmtf.org/wbem/wsman/1/wsman/fault", "AccessDenied", "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd", AdwsStrings.AccessDenied, receiverFault: false, new Fault(null, null, new DirectoryErrorDetail(e)));
	}

	internal static FaultException GenerateUnwillingToPerformFault(CreateException e)
	{
		Fault fault = new Fault(e.AttributeTypeAndValue);
		fault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			fault.ShortError = WebServiceConstants.CreateException;
		}
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.BadPutOrCreateValueReason, receiverFault: false, fault);
	}

	internal static FaultException GenerateUnwillingToPerformFaultForIdentityUnknownException(ObjectCreatedButIdentityUnknownException e)
	{
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.ObjectCreatedButIdentityUnknownReason, receiverFault: true, new Fault(null, e.Message, null));
	}

	internal static FaultException GenerateUnwillingToPerformFaultForCreateObject(InvalidOperationException e)
	{
		return GenerateFault("http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault", "UnwillingToPerform", "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess", AdwsStrings.RequestedToCreateReservedAttributes, receiverFault: false, new Fault(null, e.Message, null));
	}

	internal static FaultException GenerateUnsupportedDialectFaultForEnumSelectionOrSort(OperationType requestType)
	{
		string[] supportedDialects = DialectParser.GetSupportedDialects(requestType);
		SupportedSelectOrSortDialect[] array = new SupportedSelectOrSortDialect[supportedDialects.Length];
		for (int i = 0; i < supportedDialects.Length; i++)
		{
			array[i] = new SupportedSelectOrSortDialect(supportedDialects[i]);
		}
		if (requestType == OperationType.EnumerateSelection)
		{
			return GenerateFault("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", "UnsupportedSelectOrSortDialectFault", "http://schemas.microsoft.com/2008/1/ActiveDirectory", AdwsStrings.DialectNotSupportedForSelection, receiverFault: false, array);
		}
		return GenerateFault("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", "UnsupportedSelectOrSortDialectFault", "http://schemas.microsoft.com/2008/1/ActiveDirectory", AdwsStrings.DialectNotSupportedForSorting, receiverFault: false, array);
	}

	internal static FaultException GenerateCannotProcessFilterFault(ArgumentException e)
	{
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", "CannotProcessFilter", "http://schemas.xmlsoap.org/ws/2004/09/enumeration", AdwsStrings.CannotProcessFilterReason, receiverFault: false, new EnumerateFault(new ArgumentErrorDetail(e), null, null, null));
	}

	internal static FaultException GenerateCannotProcessFilterFault(InvalidOperationException e)
	{
		EnumerateFault enumerateFault = new EnumerateFault();
		enumerateFault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			enumerateFault.ShortError = value;
		}
		else
		{
			enumerateFault.ShortError = WebServiceConstants.InvalidOperationException;
		}
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", "CannotProcessFilter", "http://schemas.xmlsoap.org/ws/2004/09/enumeration", AdwsStrings.CannotProcessFilterReason, receiverFault: false, enumerateFault);
	}

	internal static FaultException GenerateFilterDialectRequestedUnavailableFault()
	{
		string[] supportedDialects = DialectParser.GetSupportedDialects(OperationType.EnumerateFilter);
		SupportedDialect[] array = new SupportedDialect[supportedDialects.Length];
		for (int i = 0; i < supportedDialects.Length; i++)
		{
			array[i] = new SupportedDialect(supportedDialects[i]);
		}
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", "FilterDialectRequestedUnavailable", "http://schemas.xmlsoap.org/ws/2004/09/enumeration", AdwsStrings.FilterDialectNotSupported, receiverFault: false, array);
	}

	internal static FaultException GenerateInvalidExpirationTimeFault()
	{
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", "InvalidExpirationTime", "http://schemas.xmlsoap.org/ws/2004/09/enumeration", AdwsStrings.InvalidExpirationTimeReason, receiverFault: false, new EnumerateFault(null, AdwsStrings.InvalidExpirationTimeDetail, null, null));
	}

	internal static FaultException GenerateInvalidSortKeyFault(string faultDetail)
	{
		return GenerateFault("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", "InvalidSortKey", "http://schemas.microsoft.com/2008/1/ActiveDirectory", AdwsStrings.InvalidSortKeyReason, receiverFault: false, new EnumerateFault(null, faultDetail, null, null));
	}

	internal static FaultException GenerateInvalidPropertyFault(InvalidExpressionException e)
	{
		EnumerateFault enumerateFault = new EnumerateFault(null, null, null, e.Expression);
		if (e.Reason == InvalidExpressionDetailType.InvalidExpressionSyntax)
		{
			enumerateFault.Error = AdwsStrings.InvalidPropertySyntaxDetail;
		}
		else
		{
			enumerateFault.Error = AdwsStrings.InvalidPropertyValueDetail;
		}
		if (WebServiceStrings.LongToShortMap.TryGetValue(enumerateFault.Error, out var value))
		{
			enumerateFault.ShortError = value;
		}
		else
		{
			enumerateFault.ShortError = WebServiceConstants.InvalidExpressionException;
		}
		return GenerateFault("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", "InvalidPropertyFault", "http://schemas.microsoft.com/2008/1/ActiveDirectory", AdwsStrings.InvalidPropertyReason, receiverFault: false, enumerateFault);
	}

	internal static FaultException GenerateEnumerationContextLimitExceededFault(EnumerationContextLimitExceededException e)
	{
		return GenerateFault("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", "EnumerationContextLimitExceeded", "http://schemas.microsoft.com/2008/1/ActiveDirectory", AdwsStrings.EnumCtxLimitExceededReason, receiverFault: false, new EnumerateFault(null, e.Message, null, null));
	}

	internal static FaultException GenerateInvalidEnumerationContextFault(string faultDetail)
	{
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", "InvalidEnumerationContext", "http://schemas.xmlsoap.org/ws/2004/09/enumeration", AdwsStrings.InvalidEnumerationContext, receiverFault: false, new Fault(null, faultDetail, null));
	}

	internal static FaultException GenerateMaxCharsNotSupportedFault()
	{
		return GenerateFault("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", "MaxCharsNotSupported", "http://schemas.microsoft.com/2008/1/ActiveDirectory", AdwsStrings.MaxCharsNotSupportedReason, receiverFault: false, new PullFault(null, AdwsStrings.MaxCharsNotSupportedDetail, null));
	}

	internal static FaultException GenerateMaxTimeExceedsLimitFault()
	{
		return GenerateFault("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", "MaxTimeExceedsLimit", "http://schemas.microsoft.com/2008/1/ActiveDirectory", AdwsStrings.MaxTimeExceedsLimitReason, receiverFault: false, new PullFault(null, AdwsStrings.MaxTimeExceedsLimitDetail, null));
	}

	internal static FaultException GenerateEndpointUnavailableFault(UnknownAttributeException e)
	{
		Fault fault = new Fault();
		fault.Error = string.Format(CultureInfo.CurrentUICulture, e.Message, new object[1] { e.UnknownAttribute });
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			fault.ShortError = WebServiceConstants.UnknownAttributeException;
		}
		fault.UnknownAttribute = e.UnknownAttribute;
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "EndpointUnavailable", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.UnknownAttributeError, receiverFault: true, fault);
	}

	internal static FaultException GenerateEndpointUnavailableFault(InvalidOperationException e)
	{
		Fault fault = new Fault();
		fault.Error = e.Message;
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			fault.ShortError = value;
		}
		else
		{
			fault.ShortError = WebServiceConstants.InvalidOperationException;
		}
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/08/addressing/fault", "EndpointUnavailable", "http://schemas.xmlsoap.org/ws/2004/08/addressing", AdwsStrings.InvalidOperationDuringPullReason, receiverFault: true, fault);
	}

	internal static FaultException GenerateTimedOutFault(DirectoryOperationException e)
	{
		PullFault pullFault = new PullFault();
		pullFault.DirectoryError = new DirectoryErrorDetail(e);
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", "TimedOut", "http://schemas.xmlsoap.org/ws/2004/09/enumeration", AdwsStrings.TimedOutReason, receiverFault: true, pullFault);
	}

	internal static FaultException GenerateUnableToRenewFault(string faultDetail)
	{
		return GenerateFault("http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault", "UnableToRenew", "http://schemas.xmlsoap.org/ws/2004/09/enumeration", AdwsStrings.UnableToRenewReason, receiverFault: false, new RenewFault(null, faultDetail, null));
	}

	internal static FaultException GenerateCustomActionFault<T>(ArgumentException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			ArgumentError = new ArgumentErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.ArgumentError, receiverFault: false, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(string s) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = s
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(s, out var value))
		{
			val.ShortError = value;
		}
		else
		{
			val.ShortError = null;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.ArgumentError, receiverFault: false, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(LdapException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			DirectoryError = new DirectoryErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.LdapError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(DirectoryOperationException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			DirectoryError = new DirectoryErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(ActiveDirectoryObjectNotFoundException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			DirectoryError = new DirectoryErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(ActiveDirectoryOperationException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			DirectoryError = new DirectoryErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(ActiveDirectoryServerDownException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			DirectoryError = new DirectoryErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(OutOfMemoryException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = e.Message
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			val.ShortError = value;
		}
		else
		{
			val.ShortError = WebServiceConstants.OutOfMemoryException;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(AuthenticationException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = e.Message
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			val.ShortError = value;
		}
		else
		{
			val.ShortError = WebServiceConstants.AuthenticationException;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(UnauthorizedAccessException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = e.Message
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			val.ShortError = value;
		}
		else
		{
			val.ShortError = WebServiceConstants.UnauthorizedAccessException;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(InvalidOperationException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = e.Message
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			val.ShortError = value;
		}
		else
		{
			val.ShortError = WebServiceConstants.InvalidOperationException;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(MultipleMatchesException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = e.Message
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			val.ShortError = value;
		}
		else
		{
			val.ShortError = WebServiceConstants.SDSAM_MultipleMatchesException;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.MultiplePrincipalMatchesError, receiverFault: false, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(NoMatchingPrincipalException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = e.Message
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			val.ShortError = value;
		}
		else
		{
			val.ShortError = WebServiceConstants.SDSAM_NoMatchingPrincipalException;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.NoPrincipalMatchesError, receiverFault: false, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(PrincipalOperationException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			DirectoryError = new DirectoryErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(PrincipalException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = e.Message
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			val.ShortError = value;
		}
		else if (e is PasswordException)
		{
			val.ShortError = WebServiceConstants.SDSAM_PasswordException;
		}
		else
		{
			val.ShortError = WebServiceConstants.SDSAM_PrincipalException;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(NotSupportedException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			Error = e.Message
		};
		if (WebServiceStrings.LongToShortMap.TryGetValue(e.Message, out var value))
		{
			val.ShortError = value;
		}
		else
		{
			val.ShortError = WebServiceConstants.NotSupportedException;
		}
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.OperationNotSupported, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(DirectoryServicesCOMException e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			DirectoryError = new DirectoryErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", AdwsStrings.DirectoryError, receiverFault: true, val);
	}

	internal static FaultException GenerateCustomActionFault<T>(string faultReason, Win32Exception e) where T : CustomActionFault, new()
	{
		T val = new T
		{
			DirectoryError = new DirectoryErrorDetailCA(e)
		};
		return GenerateCustomActionFaultMessage("http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", val.FaultName, "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions", faultReason, receiverFault: true, val);
	}
}

using System.Collections.Generic;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

public static class WebServiceStrings
{
	public static class DataTypes
	{
		public const string AnyUri = "anyURI";

		public const string Duration = "duration";

		public const string PositiveInteger = "positiveInteger";

		public const string ResourceManagementBinary = "binaryType";

		public const string ResourceManagementBoolean = "booleanType";

		public const string ResourceManagementDateTime = "dateTimeType";

		public const string ResourceManagementInteger = "integerType";

		public const string ResourceManagementReference = "referenceType";

		public const string ResourceManagementString = "stringType";

		public const string String = "string";
	}

	public static class ExceptionMessages
	{
		public const string Default = "An unspecified error occurred.";
	}

	public static class FaultNames
	{
		public const string ActionNotSupportedFault = "ActionNotSupportedFault";

		public const string AccessDeniedFault = "AccessDenied";

		public const string AlreadyExistsFault = "AlreadyExists";

		public const string GetADDomainFault = "GetADDomainFault";

		public const string GetADDomainControllerFault = "GetADDomainControllerFault";

		public const string GetADForestFault = "GetADForestFault";

		public const string GetVersionFault = "GetVersionFault";

		public const string AuthenticationRequiredFault = "AuthenticationRequiredFault";

		public const string AuthorizationRequiredFault = "AuthorizationRequiredFault";

		public const string CannotProcessFilter = "CannotProcessFilter";

		public const string ChangeOptionalFeatureFault = "ChangeOptionalFeatureFault";

		public const string ChangePasswordFault = "ChangePasswordFault";

		public const string DestinationUnreachableFault = "DestinationUnreachable";

		public const string EncodingLimitFault = "EncodingLimit";

		public const string EndpointUnavailable = "EndpointUnavailable";

		public const string EnumerationContextLimitExceededFault = "EnumerationContextLimitExceeded";

		public const string FilterDialectRequestedUnavailable = "FilterDialectRequestedUnavailable";

		public const string FragmentDialectNotSupportedFault = "FragmentDialectNotSupported";

		public const string GetADGroupMemberFault = "GetADGroupMemberFault";

		public const string GetADPrincipalAuthorizationGroupFault = "GetADPrincipalAuthorizationGroupFault";

		public const string GetADPrincipalGroupMembershipFault = "GetADPrincipalGroupMembershipFault";

		public const string InvalidEnumerationContext = "InvalidEnumerationContext";

		public const string InvalidExpirationTime = "InvalidExpirationTime";

		public const string InvalidPropertyFault = "InvalidPropertyFault";

		public const string InvalidRepresentationFault = "InvalidRepresentation";

		public const string InvalidSortKeyFault = "InvalidSortKey";

		public const string MaxCharsNotSupportedFault = "MaxCharsNotSupported";

		public const string MaxTimeExceedsLimitFault = "MaxTimeExceedsLimit";

		public const string MoveADOperationMasterRoleFault = "MoveADOperationMasterRoleFault";

		public const string NoConnectionAvailableFault = "NoConnectionAvailableFault";

		public const string PasswordPolicyFault = "PasswordPolicyFault";

		public const string PermissionDeniedFault = "PermissionDeniedFault";

		public const string RequestFailedFault = "RequestFailed";

		public const string SchemaValidationErrorFault = "SchemaValidationError";

		public const string SetPasswordFault = "SetPasswordFault";

		public const string TimedOutFault = "TimedOut";

		public const string TranslateNameFault = "TranslateNameFault";

		public const string UnableToRenewFault = "UnableToRenew";

		public const string UnsupportedSelectOrSortDialectFault = "UnsupportedSelectOrSortDialectFault";

		public const string UnsupportedExpirationType = "UnsupportedExpirationType";

		public const string UnwillingToPerformFault = "UnwillingToPerform";
	}

	public static class Uris
	{
		public const string AdNs = "http://schemas.microsoft.com/2008/1/ActiveDirectory";

		public const string AddataNs = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data";

		public const string AdCustomActionNs = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions";

		public const string AdFault = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault";

		public const string Context = "http://schemas.microsoft.com/ws/2006/05/context";

		public const string Create = "http://schemas.xmlsoap.org/ws/2004/09/transfer/Create";

		public const string CreateResponse = "http://schemas.xmlsoap.org/ws/2004/09/transfer/CreateResponse";

		public const string Delete = "http://schemas.xmlsoap.org/ws/2004/09/transfer/Delete";

		public const string DeleteResponse = "http://schemas.xmlsoap.org/ws/2004/09/transfer/DeleteResponse";

		public const string Enumerate = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Enumerate";

		public const string EnumerateResponse = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/EnumerateResponse";

		public const string Get = "http://schemas.xmlsoap.org/ws/2004/09/transfer/Get";

		public const string GetResponse = "http://schemas.xmlsoap.org/ws/2004/09/transfer/GetResponse";

		public const string GetStatus = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/GetStatus";

		public const string GetStatusResponse = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/GetStatusResponse";

		public const string IdentityManagementDirectoryAccess = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess";

		public const string IdentityManagementDirectoryAccessFault = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/fault";

		public const string LdapQueryNs = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/LdapQuery";

		public const string Psha1 = "http://schemas.xmlsoap.org/ws/2005/02/trust/CK/PSHA1";

		public const string Pull = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Pull";

		public const string PullResponse = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/PullResponse";

		public const string Put = "http://schemas.xmlsoap.org/ws/2004/09/transfer/Put";

		public const string PutResponse = "http://schemas.xmlsoap.org/ws/2004/09/transfer/PutResponse";

		public const string QNameDialect = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/QName";

		public const string Release = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Release";

		public const string ReleaseResponse = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/ReleaseResponse";

		public const string Renew = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/Renew";

		public const string RenewResponse = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/RenewResponse";

		public const string RequestSizeLimitExceeded = "http://schemas.microsoft.com/2006/11/IdentityManagement/DirectoryAccess/RequestSizeLimitExceeded";

		public const string WebService = "http://schemas.microsoft.com/2008/1/ActiveDirectory";

		public const string WSAddressing = "http://www.w3.org/2005/08/addressing";

		public const string WSAddressingAugust2004 = "http://schemas.xmlsoap.org/ws/2004/08/addressing";

		public const string WSAddressingFault = "http://www.w3.org/2005/08/addressing/fault";

		public const string WSAddressingAugust2004Fault = "http://schemas.xmlsoap.org/ws/2004/08/addressing/fault";

		public const string WSEnumeration = "http://schemas.xmlsoap.org/ws/2004/09/enumeration";

		public const string WSEnumerationFault = "http://schemas.xmlsoap.org/ws/2004/09/enumeration/fault";

		public const string WSManagement = "http://schemas.dmtf.org/wbem/wsman/1/wsman.xsd";

		public const string WSManagementFault = "http://schemas.dmtf.org/wbem/wsman/1/wsman/fault";

		public const string WSMetadataExchange = "http://schemas/xmlsoap.org/ws/2004/09/mex";

		public const string WSPolicy = "http://schemas.xmlsoap.org/ws/2004/09/policy";

		public const string WSResourceManagement = "http://schemas.microsoft.com/2006/11/ResourceManagement";

		public const string WSTransfer = "http://schemas.xmlsoap.org/ws/2004/09/transfer";

		public const string WSTransferFault = "http://schemas.xmlsoap.org/ws/2004/09/transfer/fault";

		public const string XmlSchema = "http://www.w3.org/2001/XMLSchema";

		public const string XmlSchemaInstance = "http://www.w3.org/2001/XMLSchema-instance";

		public const string XPathLevel1Dialect = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/XPath-Level-1";

		public const string XPath10SubsetQuery = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Dialect/XpathQuery";
	}

	public static class XmlAttributes
	{
		public const string Name = "name";

		public const string Nil = "nil";

		public const string Type = "type";

		public const string Value = "value";
	}

	public const string AccountManagement = "AccountManagement";

	public const string Action = "Action";

	public const string ActionProcesses = "ActionProcesses";

	public const string Actions = "Actions";

	public const string Address = "Address";

	public const string AdPrefix = "ad";

	public const string AddataPrefix = "addata";

	public const string AddRequest = "AddRequest";

	public const string AddressingPrefix = "wsa";

	public const string AllTime = "AllTime";

	public const string ANRQuery = "ANRQuery";

	public const string AppliesTo = "AppliesTo";

	public const string Ascending = "Ascending";

	public const string At = "At";

	public const string AttributeBasedSet = "AttributeBasedSet";

	public const string AttributedUri = "AttributedURI";

	public const string AttributeName = "AttributeName";

	public const string AttributeNode = "AttributeNode";

	public const string AttributeType = "AttributeType";

	public const string AttributeTypeAndValue = "AttributeTypeAndValue";

	public const string AttributeTypeNotValid = "AttributeTypeNotValid";

	public const string AttributeTypeNotValidForDialect = "AttributeTypeNotValidForDialect";

	public const string AttributeTypeNotValidForEntry = "AttributeTypeNotValidForEntry";

	public const string AttributeValue = "AttributeValue";

	public const string AuthenticationChallenge = "AuthenticationChallenge";

	public const string AuthenticationChallengeResponse = "AuthenticationChallengeResponse";

	public const string AuthenticationProcesses = "AuthenticationProcesses";

	public const string AuthorizationProcesses = "AuthorizationProcesses";

	public const string Base64Binary = "base64Binary";

	public const string BaseObjectSearchRequest = "BaseObjectSearchRequest";

	public const string BaseObjectSearchResponse = "BaseObjectSearchResponse";

	public const string Between = "Between";

	public const string BinarySecret = "BinarySecret";

	public const string Body = "Body";

	public const string Change = "Change";

	public const string ChangeType = "ChangeType";

	public const string ColumnSortingOrder = "ColumnSortingOrder";

	public const string CommonNameKey = "CN=";

	public const string ComputedKey = "ComputedKey";

	public const string Condition = "Condition";

	public const string Conditions = "Conditions";

	public const string Contain = "Contain";

	public const string Context = "Context";

	public const string Control = "control";

	public const string Controls = "controls";

	public const string ControlValue = "controlValue";

	public const string Count = "Count";

	public const string Create = "Create";

	public const string CreatedTime = "CreatedTime";

	public const string Creator = "Creator";

	public const string Criticality = "criticality";

	public const string Current = "Current";

	public const string DateReceived = "DateReceived";

	public const string Data = "Data";

	public const string DefaultNamespacePrefix = "rm";

	public const string Decision = "Decision";

	public const string Delete = "Delete";

	public const string DeletedTime = "DeletedTime";

	public const string Details = "Details";

	public const string Descending = "Descending";

	public const string Dialect = "Dialect";

	public const string FragmentDialect = "FragmentDialect";

	public const string DisplayName = "DisplayName";

	public const string EndOfSequence = "EndOfSequence";

	public const string EndpointReference = "EndpointReference";

	public const string EndsWith = "EndsWith";

	public const string EndTo = "EndTo";

	public const string Entropy = "Entropy";

	public const string Enumerate = "Enumerate";

	public const string EnumerateResponse = "EnumerateResponse";

	public const string EnumerationContext = "EnumerationContext";

	public const string EnumerationDetail = "EnumerationDetail";

	public const string EnumerationDirection = "EnumerationDirection";

	public const string EnumerationPrefix = "wsen";

	public const string ErrorString = "ErrorString";

	public const string Equal = "Equal";

	public const string EventType = "EventType";

	public const string Exist = "Exist";

	public const string ExpirationTime = "ExpirationTime";

	public const string Expires = "Expires";

	public const string FaultDetail = "FaultDetail";

	public const string Filter = "Filter";

	public const string Fragment = "Fragment";

	public const string Get = "Get";

	public const string GetResponse = "GetResponse";

	public const string GetStatus = "GetStatus";

	public const string GetStatusResponse = "GetStatusResponse";

	public const string GreaterOrEqual = "GreaterOrEqual";

	public const string GreaterThan = "GreaterThan";

	public const string Identifier = "Identifier";

	public const string IdentityManagementDirectoryAccessPrefix = "da";

	public const string IdentityManagementOperation = "IdentityManagementOperation";

	public const string In = "In";

	public const string Instance = "instance";

	public const string InstanceId = "InstanceId";

	public const string InstanceType = "type";

	public const string Intersect = "Intersect";

	public const string IsTransitive = "IsTransitive";

	public const string ItemList = "ItemList";

	public const string Items = "Items";

	public const string KeySize = "KeySize";

	public const string LdapQuery = "LdapQuery";

	public const string LdapQueryPrefix = "adlq";

	public const string LeftOperand = "LeftOperand";

	public const string LessOrEqual = "LessOrEqual";

	public const string LessThan = "LessThan";

	public const string LogicalOperator = "LogicalOperator";

	public const string MappingType = "MappingType";

	public const string ManagementPrefix = "wsman";

	public const string MaxCharacters = "MaxCharacters";

	public const string MaxElements = "MaxElements";

	public const string MaxOccurs = "maxOccurs";

	public const string MaxTime = "MaxTime";

	public const string MessageId = "MessageID";

	public const string Metadata = "Metadata";

	public const string MetadataType = "MetadataType";

	public const string MixedAnyType = "MixedAnyType";

	public const string Mode = "Mode";

	public const string ModeType = "ModeType";

	public const string ModifyRequest = "ModifyRequest";

	public const string NotContain = "NotContain";

	public const string NotEndWith = "NotEndWith";

	public const string NotEqual = "NotEqual";

	public const string NotExist = "NotExist";

	public const string NotIn = "NotIn";

	public const string NotStartWith = "NotStartWith";

	public const string ObjectId = "ObjectID";

	public const string ObjectType = "ObjectType";

	public const string ObjectReferenceProperty = "objectReferenceProperty";

	public const string Operation = "Operation";

	public const string PageSize = "PageSize";

	public const string PartialAttribute = "PartialAttribute";

	public const string PasswordElementName = "ResetPassword";

	public const string Phase = "Phase";

	public const string Process = "BusinessProcess";

	public const string ProcessStatus = "ProcessStatus";

	public const string ProcessType = "ProcessType";

	public const string Pull = "Pull";

	public const string PullAdjustment = "PullAdjustment";

	public const string PullAdjustmentType = "PullAdjustmentType";

	public const string PullResponse = "PullResponse";

	public const string Put = "Put";

	public const string PutFragmentType = "PutFragmentType";

	public const string Query = "Query";

	public const string RangeLow = "RangeLow";

	public const string RangeHigh = "RangeHigh";

	public const string Reason = "Reason";

	public const string Reference = "Reference";

	public const string ReferenceParameters = "ReferenceParameters";

	public const string ReferenceParametersType = "ReferenceParametersType";

	public const string ReferenceProperty = "ReferenceProperty";

	public const string RegistrationMode = "Register";

	public const string RelationalOperator = "RelationalOperator";

	public const string Release = "Release";

	public const string Renew = "Renew";

	public const string RenewResponse = "RenewResponse";

	public const string Request = "Request";

	public const string RequestId = "RequestID";

	public const string RequestedAttachedReference = "RequestedAttachedReference";

	public const string RequestDetails = "RequestDetails";

	public const string RequestedUnattachedReference = "RequestedUnattachedReference";

	public const string RequestStatus = "RequestStatus";

	public const string RequiredApproval = "RequiredApproval";

	public const string Resource = "Resource";

	public const string ResourceCreated = "ResourceCreated";

	public const string ResourceFactory = "ResourceFactory";

	public const string ResourceReferenceProperty = "ResourceReferenceProperty";

	public const string ResourceReferencePropertyType = "ResourceReferencePropertyType";

	public const string ResourceType = "ResourceType";

	public const string ResultSet = "ResultSet";

	public const string ResultSettings = "ResultSettings";

	public const string RetryAfter = "RetryAfter";

	public const string ReturnAttributeName = "ReturnAttributeName";

	public const string Search = "Search";

	public const string Selection = "Selection";

	public const string SelectionProperty = "SelectionProperty";

	public const string SearchString = "SearchString";

	public const string Set = "Set";

	public const string Since = "Since";

	public const string SizeLimit = "SizeLimit";

	public const string Sorting = "Sorting";

	public const string SortingOrder = "SortingOrder";

	public const string SortingPrecedence = "SortingPrecedence";

	public const string SortingProperty = "SortingProperty";

	public const string StartingIndex = "StartingIndex";

	public const string StartsWith = "StartWith";

	public const string Status = "Status";

	public const string String = "String";

	public const string StringType = "string";

	public const string SubAttributeName = "SubAttributeName";

	public const string SubConditions = "SubConditions";

	public const string SupportedDialect = "SupportedDialect";

	public const string SupportedSelectOrSortDialect = "SupportedSelectOrSortDialect";

	public const string TextNode = "TextNode";

	public const string Time1 = "Time1";

	public const string Time2 = "Time2";

	public const string TimeConstraint = "TimeConstraint";

	public const string TimeConstraintType = "TimeConstraintType";

	public const string TopologyManagement = "TopologyManagement";

	public const string TraceSourceName = "Microsoft.ActiveDirectory.WebServices.Shared";

	public const string TransferPrefix = "wst";

	public const string Unbounded = "unbounded";

	public const string Union = "Union";

	public const string UserLockedOut = "UserLockedOut";

	public const string UserRegistered = "UserRegistered";

	public const string XmlNs = "xmlns";

	public const string XsdInstancePrefix = "xsd";

	public const string XsdPrefix = "xs";

	public const string XsiInstancePrefix = "xsi";

	public const string DirectoryErrorElement = "DirectoryError";

	public const string ArgumentErrorElement = "ArgumentError";

	public const string MessageElement = "Message";

	public const string ShortMessageElement = "ShortMessage";

	public const string ErrorCodeElement = "ErrorCode";

	public const string Win32ErrorCodeElement = "Win32ErrorCode";

	public const string ExtendedErrorMessageElement = "ExtendedErrorMessage";

	public const string MatchedDNElement = "MatchedDN";

	public const string ReferralElement = "Referral";

	public const string ParameterNameElement = "ParameterName";

	public static Dictionary<string, string> LongToShortMap;

	static WebServiceStrings()
	{
		LongToShortMap = new Dictionary<string, string>();
	}
}

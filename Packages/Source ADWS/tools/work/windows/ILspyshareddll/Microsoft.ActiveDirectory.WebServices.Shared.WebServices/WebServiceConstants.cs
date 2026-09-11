namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

public class WebServiceConstants
{
	public static class MaxLimitsAllowed
	{
		public const int MaxAttributeTypeAndValues = 100;

		public const int MaxAttributeTypes = 100;
	}

	public static class XPathExtensions
	{
		public const string XPathExtensionNs = "http://schemas.microsoft.com/2008/1/ActiveDirectory/XPathInternalExtensions";

		public const string XPathExtensionPrefix = "adxp";

		public const string CaseInsensitiveEquals = "nocaseequals";
	}

	public static readonly string ArgumentException = "EArgument";

	public static readonly string AuthenticationException = "EAuthentication";

	public static readonly string CreateException = "ECreate";

	public static readonly string DirectoryOperationException = "EDirectoryOperation";

	public static readonly string EnumerationContextLimitExceededException = "EEnumContextLimitExceeded";

	public static readonly string InvalidExpressionException = "EInvalidExpression";

	public static readonly string InvalidModifyRequestSyntaxException = "EInvalidModifyRequestSyntax";

	public static readonly string InvalidOperationException = "EInvalidOperation";

	public static readonly string ModifyRequestOperationUnsupportedException = "EModifyOperationUnsupported";

	public static readonly string NoConnectionAvailableException = "ENoConnection";

	public static readonly string NotSupportedException = "ENotSupported";

	public static readonly string OutOfMemoryException = "EOutOfMemory";

	public static readonly string PutException = "EPut";

	public static readonly string SCM_Win32Exception = "EWindows32";

	public static readonly string SDS_DirectoryServicesCOMException = "ECOMServices";

	public static readonly string SDSAD_ActiveDirectoryObjectNotFoundException = "EObjectNotFound";

	public static readonly string SDSAD_ActiveDirectoryOperationException = "EADirectoryOperation";

	public static readonly string SDSAD_ActiveDirectoryServerDownException = "EServerDown";

	public static readonly string SDSAM_MultipleMatchesException = "EMultipleMatchingSecurityPrincipals";

	public static readonly string SDSAM_NoMatchingPrincipalException = "ENoMatchingSecurityPrincipal";

	public static readonly string SDSAM_PasswordException = "EPassword";

	public static readonly string SDSAM_PrincipalException = "ESecurityPrincipal";

	public static readonly string SDSAM_PrincipalOperationException = "ESecurityPrincipalOperation";

	public static readonly string SDSP_LdapException = "ELdap";

	public static readonly string SerializationException = "ESerialization";

	public static readonly string UnauthorizedAccessException = "EUnauthorizedAccess";

	public static readonly string UnknownAttributeException = "EUnknownAttribute";

	public static readonly string XmlException = "EInvalidXml";
}

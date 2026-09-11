using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices;

namespace Microsoft.ActiveDirectory.WebServices;

internal class AdwsStrings
{
	public static string AdwsName;

	public static string InvalidReservationToken;

	public static string TriedToFreeInUseReservedConnection;

	public static string WrongPool;

	public static string EntryReserved;

	public static string EntryNotReserved;

	public static string EntryNotInuse;

	public static string MaxUsersIsTooSmall;

	public static string PoolAlreadyExists;

	public static string NoSuchPool;

	public static string InvalidConnection;

	public static string TriedToReleaseUtilConnectionTooManyTimes;

	[Description("Y")]
	public static string AnonymousNotAllowed;

	[Description("Y")]
	public static string NoConnectionAvailable;

	[Description("Y")]
	public static string MustSpecifyRdnForCreation;

	[Description("Y")]
	public static string MustSpecifyParentForCreation;

	[Description("Y")]
	public static string MustSpecifyObjectClassForCreation;

	public static string MustSpecifyAttributeNameForCreation;

	[Description("Y")]
	public static string CouldntFindParentObjectForCreation;

	[Description("Y")]
	public static string ObjectCreatedButIdentityUnknown;

	[Description("Y")]
	public static string CouldntFindObjectForMove;

	[Description("Y")]
	public static string MustSpecifyRdnForRename;

	[Description("Y")]
	public static string OperationTimeout;

	[Description("Y")]
	public static string MustSpecifyContainerForMove;

	public static string MustSpecifyAttributeNameForUpdate;

	public static string MustSpecifyAttributeNameForTypeLookup;

	[Description("Y")]
	public static string UnknownAttribute;

	public static string InvalidExtendedDnString;

	[Description("Y")]
	public static string InvalidObjectReferenceProperty;

	public static string MustSpecifyAttributeNameForRead;

	[Description("Y")]
	public static string MustSpecifyNonnullAttrValue;

	[Description("Y")]
	public static string MustSpecifyDnForIdentifierLookup;

	[Description("Y")]
	public static string InvalidDnWithStringBinaryAccessPointValue;

	public static string CantPrepareAlreadyPreparedQuery;

	[Description("Y")]
	public static string PageSizeMustBeGreaterThanZero;

	public static string QueryMustBePreparedBeforeUse;

	[Description("Y")]
	public static string ReservedConnectionInvalidated;

	[Description("Y")]
	public static string InvalidSearchFilterForQuery;

	[Description("Y")]
	public static string MustSpecifyBaseDnForQuery;

	public static string MustSpecifyAttributeNameForQuery;

	public static string BadQuery;

	public static string MustSpecifySortAttributeNameForQuery;

	[Description("Y")]
	public static string ServerTimeMustBeNonNegative;

	[Description("Y")]
	public static string NotificationSearchControlNotAllowed;

	[Description("Y")]
	public static string NoDefaultNamingContextFoundForFilter;

	[Description("Y")]
	public static string CouldntRetrieveRootDSEForFilter;

	[Description("Y")]
	public static string ErrorWhileFetchingAttributeValues;

	[Description("Y")]
	public static string NoDCInstanceForGCSchemaLookup;

	public static string BadDirectoryObjectForCreate;

	[Description("Y")]
	public static string CantSetDistinguishedNameForCreate;

	[Description("Y")]
	public static string CantSetObjectRefPropertyForCreate;

	[Description("Y")]
	public static string InvalidRdnForCreateAndUpdate;

	[Description("Y")]
	public static string InvalidParentObjectRefForCreateAndUpdate;

	[Description("Y")]
	public static string CantSetDistinguishedNameForUpdate;

	[Description("Y")]
	public static string CantSetObjectRefPropertyForUpdate;

	[Description("Y")]
	public static string CanOnlyReplaceParentObjectRefForUpdate;

	[Description("Y")]
	public static string CanOnlyReplaceRdnForUpdate;

	public static string QueryFilterMustBeAnLdapQueryFilter;

	public static string QueryMustBeAnLdapQuery;

	[Description("Y")]
	public static string MissingOrMultipleAttributeValues;

	[Description("Y")]
	public static string AttributeValueNotaObjRef;

	[Description("Y")]
	public static string AttributeValueNotaString;

	public static string DialectClassBaseMethodNotImplemented;

	[Description("Y")]
	public static string UnknownXmlNode;

	[Description("Y")]
	public static string MustSpecifyNamespace;

	[Description("Y")]
	public static string EmptyPut;

	[Description("Y")]
	public static string EmptyCreate;

	[Description("Y")]
	public static string ObjectInWrongNamespace;

	[Description("Y")]
	public static string MissingExpression;

	[Description("Y")]
	public static string MissingValue;

	[Description("Y")]
	public static string BadValue;

	[Description("Y")]
	public static string BadPutOrCreateValue;

	[Description("Y")]
	public static string InvalidPredicate;

	[Description("Y")]
	public static string DuplicateAttributeWithValues;

	[Description("Y")]
	public static string MissingLowerRange;

	[Description("Y")]
	public static string BadValueForRangeLow;

	[Description("Y")]
	public static string BadValueForRangeHigh;

	[Description("Y")]
	public static string NotCorrectFilterType;

	public static string NullObjectIdentity;

	public static string NullObjectRDN;

	public static string NullObjectClass;

	public static string NullObjectDistinguishedName;

	public static string MissingXMLNamespace;

	[Description("Y")]
	public static string MissingTypeAttribute;

	[Description("Y")]
	public static string UnknownAttributeType;

	[Description("Y")]
	public static string InvalidBase64Binary;

	[Description("Y")]
	public static string AttributeValueNotByteOrStringOrGuid;

	[Description("Y")]
	public static string EmptyAttribute;

	[Description("Y")]
	public static string InvalidEnumerationCacheEntry;

	[Description("Y")]
	public static string DuplicateEnumerationCacheEntry;

	[Description("Y")]
	public static string MissingOrMultipleFilterNodes;

	[Description("Y")]
	public static string MissingOrMultipleBaseObjectNodes;

	[Description("Y")]
	public static string MissingOrMultipleScopeNodes;

	[Description("Y")]
	public static string ScopeNodeNotOnelevelNorSubtreeNorBase;

	[Description("Y")]
	public static string MissingScopeOrBaseObjectOrFilterNode;

	public static string SuppliedRepresentationIsInvalid;

	[Description("Y")]
	public static string InvalidPutSyntax;

	[Description("Y")]
	public static string PutOperationUnsupported;

	[Description("Y")]
	public static string CreateMissingValues;

	[Description("Y")]
	public static string MaxEnumCtxsTotalReached;

	public static string InvalidExpression;

	[Description("Y")]
	public static string MustSpecifyInstanceInfoInTheHeader;

	[Description("Y")]
	public static string MustSpecifyObjectRefPropInTheHeader;

	[Description("Y")]
	public static string InvalidInstanceInTheHeader;

	public static string EndpointUnableToProcessAtThisTime;

	public static string ActionNotSupportedReason;

	[Description("Y")]
	public static string ActionNotSupportedPutDetail;

	[Description("Y")]
	public static string ActionNotSupportedCreateDetail;

	public static string EncodingLimitExceeded;

	public static string DialectNotSupported;

	public static string SerializationError;

	public static string RequestedToCreateReservedAttributes;

	public static string RequestedToModifyReservedAttributes;

	public static string ReadObjectError;

	public static string UnknownAttributeError;

	public static string LdapError;

	public static string DirectoryError;

	public static string ArgumentError;

	public static string DestinationUnreachable;

	public static string AccessDenied;

	public static string SuppliedAttributeAlreadyExists;

	public static string SuppliedEntryAlreadyExists;

	public static string ConstraintViolation;

	public static string InvalidPutSyntaxReason;

	public static string PutOperationUnsupportedReason;

	public static string ObjectCreatedButIdentityUnknownReason;

	[Description("Y")]
	public static string NoAttrTypeAndValsPresentInTheBody;

	public static string BadPutOrCreateValueReason;

	public static string FilterDialectNotSupported;

	public static string InvalidExpirationTimeReason;

	[Description("Y")]
	public static string InvalidExpirationTimeDetail;

	public static string InvalidSortKeyReason;

	[Description("Y")]
	public static string SortKeyIsSpecialAttribute;

	[Description("Y")]
	public static string TooManySortKeySpecified;

	public static string DialectNotSupportedForSelection;

	public static string DialectNotSupportedForSorting;

	public static string InvalidPropertyReason;

	[Description("Y")]
	public static string InvalidPropertySyntaxDetail;

	[Description("Y")]
	public static string InvalidPropertyValueDetail;

	public static string EnumCtxLimitExceededReason;

	public static string CannotProcessFilterReason;

	[Description("Y")]
	public static string UnrecognizedDateAndTime;

	[Description("Y")]
	public static string EnumContextAbsentInTheRequest;

	[Description("Y")]
	public static string SessionsMismatch;

	[Description("Y")]
	public static string NoSuchEnumCtxGuidExists;

	[Description("Y")]
	public static string UnrecognizedMaxTime;

	[Description("Y")]
	public static string UnrecognizedMaxElements;

	public static string InvalidEnumerationContext;

	public static string MaxCharsNotSupportedReason;

	[Description("Y")]
	public static string MaxCharsNotSupportedDetail;

	public static string MaxTimeExceedsLimitReason;

	[Description("Y")]
	public static string MaxTimeExceedsLimitDetail;

	public static string TimedOutReason;

	[Description("Y")]
	public static string NewExpirationTimeNotSpecified;

	public static string UnableToRenewReason;

	[Description("Y")]
	public static string UnableToRenewLimitExceededDetail;

	[Description("Y")]
	public static string UnableToRenewExpiresInPastDetail;

	[Description("Y")]
	public static string UnableToRenewNoSuchEntryDetail;

	public static string InvalidOperationDuringPullReason;

	[Description("Y")]
	public static string MissingDialect;

	[Description("Y")]
	public static string MissingFilterDialect;

	[Description("Y")]
	public static string MissingSelectionDialect;

	[Description("Y")]
	public static string MissingSortingDialect;

	[Description("Y")]
	public static string ImpersonationLevelNotSetToImpersonate;

	[Description("Y")]
	public static string MustSupplyNtdsSettingsDNArrayForGetADDomainController;

	[Description("Y")]
	public static string MustSupplyServerNameForCustomActions;

	[Description("Y")]
	public static string NonNTDSInstanceInTheHeader;

	[Description("Y")]
	public static string NonNTDSOrADLDSInstanceInTheHeader;

	[Description("Y")]
	public static string NonNTDSOrGCOrADLDSInstanceInTheHeader;

	[Description("Y")]
	public static string CouldntRetrieveNtdsSettingsObjectForGetADDomainController;

	[Description("Y")]
	public static string CouldntRetrieveServerObjectForGetADDomainController;

	[Description("Y")]
	public static string CouldntRetrieveServerReferenceFromServerObjectForGetADDomainController;

	[Description("Y")]
	public static string CouldntRetrieveComputerObjectForGetADDomainController;

	[Description("Y")]
	public static string CouldntRetrieveEnterpriseConfigObjectForGetADDomainController;

	[Description("Y")]
	public static string CouldntConvertDomainDnToDomainDnsNameForGetADDomainController;

	[Description("Y")]
	public static string CouldntRetrieveAddsFsmoRoles;

	[Description("Y")]
	public static string CouldntRetrieveRootDSE;

	[Description("Y")]
	public static string CouldntRetrieveDomainPartition;

	[Description("Y")]
	public static string CouldntRetrieveObjectClassFromDomainPartitionObject;

	[Description("Y")]
	public static string CouldntRetrieveDomainCrossRef;

	[Description("Y")]
	public static string CouldntRetrieveRootDomainCrossRef;

	[Description("Y")]
	public static string CouldntRetrieveDomainCrossRefs;

	[Description("Y")]
	public static string CouldntRetrieveWellKnownObjects;

	[Description("Y")]
	public static string CouldntRetrieveDefaultNamingContext;

	[Description("Y")]
	public static string CouldntRetrieveRootDomainNamingContext;

	[Description("Y")]
	public static string CouldntRetrieveConfigurationNamingContext;

	[Description("Y")]
	public static string CouldntRetrieveConfigurationPartition;

	[Description("Y")]
	public static string UnknownRoleForMoveADOperationMasterRole;

	[Description("Y")]
	public static string CouldNotTransferPdcFsmo;

	[Description("Y")]
	public static string ObjectSidCouldNotBeRetrievedForPdcFsmoTransfer;

	[Description("Y")]
	public static string MustSupplyFeatureIdForChangeOptionalFeature;

	[Description("Y")]
	public static string MustSupplyDistinguishedNameForChangeOptionalFeature;

	[Description("Y")]
	public static string MustSupplyPartitionDn;

	[Description("Y")]
	public static string MustSupplyPrincipalDn;

	[Description("Y")]
	public static string MustSupplyAccountDnForSetPasswordorChangePassword;

	[Description("Y")]
	public static string MustSupplyNewPasswordForSetPasswordorChangePassword;

	[Description("Y")]
	public static string MustSupplyOldPasswordForChangePassword;

	[Description("Y")]
	public static string ResourceContextServerCannotBeEmptyForGetADPrincipalGroupMembership;

	[Description("Y")]
	public static string ResourceContextPartitionCannotBeEmptyForGetADPrincipalGroupMembership;

	[Description("Y")]
	public static string MustFullySpecifyResourceContextForGetADPrincipalGroupMembership;

	[Description("Y")]
	public static string ResourceContextServerHasBadFormatForGetADPrincipalGroupMembership;

	[Description("Y")]
	public static string ResourceContextServerTypeCouldNotBeDeterminedForGetADPrincipalGroupMembership;

	[Description("Y")]
	public static string MustSupplyGroupDnForGetADGroupMember;

	[Description("Y")]
	public static string NoSuchSecurityPrincipal;

	[Description("Y")]
	public static string NoSuchGroup;

	[Description("Y")]
	public static string NoSuchAuthenticablePrincipal;

	[Description("Y")]
	public static string UnknownScopeForGetADPrincipalGroupMembership;

	public static string MultiplePrincipalMatchesError;

	public static string NoPrincipalMatchesError;

	public static string OperationNotSupported;

	[Description("Y")]
	public static string CouldNotRetrieveLocalDomainName;

	[Description("Y")]
	public static string MustSupplyNamesForTranslateName;

	[Description("Y")]
	public static string MustSupplyNonNullNameForTranslateName;

	[Description("Y")]
	public static string UnsupportedInputFormatForTranslateName;

	[Description("Y")]
	public static string UnsupportedOutputFormatForTranslateName;

	[Description("Y")]
	public static string CantCrackNamesForTranslateName;

	[Description("Y")]
	public static string UnknownFormatForNameTranslate;

	[Description("Y")]
	public static string MaxGroupOrMemberEntriesExceeded;

	public static string NotAValidHostWithPortString;

	public static string CouldNotDetermineTypeOfServer;

	public static string CouldNotConstructPerformanceCounter;

	public static string CouldNotAdjustPerformanceCounterValue;

	public static string PerformanceCountersAlreadyInitialized;

	public static string CouldNotCheckIfPerformanceCountersInstalled;

	public static string CouldNotCheckPerformanceCounterVersion;

	public static string CouldNotCheckPerformanceCounterVersionMissingServiceKey;

	public static string CouldNotInstallPerformanceCounters;

	public static string CouldNotUninstallPerformanceCounters;

	public static string CouldNotUpdatePerformanceCounterVersion;

	public static string CouldNotUpdatePerformanceCounterVersionMissingServiceKey;

	public static string ADWS_OBJ_HELP;

	public static string CREATEOPSRATE_NAME;

	public static string CREATEOPSRATE_HELP;

	public static string DELETEOPSRATE_NAME;

	public static string DELETEOPSRATE_HELP;

	public static string GETOPSRATE_NAME;

	public static string GETOPSRATE_HELP;

	public static string PUTOPSRATE_NAME;

	public static string PUTOPSRATE_HELP;

	public static string ENUMOPSRATE_NAME;

	public static string ENUMOPSRATE_HELP;

	public static string PULLOPSRATE_NAME;

	public static string PULLOPSRATE_HELP;

	public static string OPENENUMCTXSCOUNT_NAME;

	public static string OPENENUMCTXSCOUNT_HELP;

	public static string GETADGROUPMEMBEROPSRATE_NAME;

	public static string GETADGROUPMEMBEROPSRATE_HELP;

	public static string GETADPRINCGROUPMEMBERSHIPOPSRATE_NAME;

	public static string GETADPRINCGROUPMEMBERSHIPOPSRATE_HELP;

	public static string SETPASSOPSRATE_NAME;

	public static string SETPASSOPSRATE_HELP;

	public static string CHANGEPASSOPSRATE_NAME;

	public static string CHANGEPASSOPSRATE_HELP;

	public static string GETADPRINCAUTHZGROUPOPSRATES_NAME;

	public static string GETADPRINCAUTHZGROUPOPSRATES_HELP;

	public static string TRANSLATENAMESOPSRATE_NAME;

	public static string TRANSLATENAMESOPSRATE_HELP;

	public static string GETADDOMCTRLOPSRATE_NAME;

	public static string GETADDOMCTRLOPSRATE_HELP;

	public static string GETADDOMAINOPSRATE_NAME;

	public static string GETADDOMAINOPSRATE_HELP;

	public static string MOVEROLEOPSRATE_NAME;

	public static string MOVEROLEOPSRATE_HELP;

	public static string GETADFORESTOPSRATE_NAME;

	public static string GETADFORESTOPSRATE_HELP;

	public static string GETVERSIONOPSRATE_NAME;

	public static string GETVERSIONOPSRATE_HELP;

	public static string NUMDIRINSTANCES_NAME;

	public static string NUMDIRINSTANCES_HELP;

	public static string POSSCONNSCOUNT_NAME;

	public static string POSSCONNSCOUNT_HELP;

	public static string ALLOCCONNSCOUNT_NAME;

	public static string ALLOCCONNSCOUNT_HELP;

	public static string RESERVEDCONNSCOUNT_NAME;

	public static string RESERVEDCONNSCOUNT_HELP;

	public static string NONRESERVEDCONNSINUSECOUNT_NAME;

	public static string NONRESERVEDCONNSINUSECOUNT_HELP;

	public static string RESERVEDCONNSINUSECOUNT_NAME;

	public static string RESERVEDCONNSINUSECOUNT_HELP;

	public static string OPENWEBSERVICESESSIONCOUNT_NAME;

	public static string OPENWEBSERVICESESSIONCOUNT_HELP;

	public static string ACTIVEWEBSERVICESESSIONCOUNT_NAME;

	public static string ACTIVEWEBSERVICESESSIONCOUNT_HELP;

	public static string CHANGEOPTIONALFEATUREOPSRATE_NAME;

	public static string CHANGEOPTIONALFEATUREOPSRATE_HELP;

	public static string OPENWEBSERVICESESSIONRATE_NAME;

	public static string OPENWEBSERVICESESSIONRATE_HELP;

	public static string CUSTACT_LDAP_CACHE_POSSCONNSCOUNT_NAME;

	public static string CUSTACT_LDAP_CACHE_POSSCONNSCOUNT_HELP;

	public static string CUSTACT_LDAP_CACHE_CREATERATE_NAME;

	public static string CUSTACT_LDAP_CACHE_CREATERATE_HELP;

	public static string CUSTACT_LDAP_CACHE_REUSERATE_NAME;

	public static string CUSTACT_LDAP_CACHE_REUSERATE_HELP;

	public static string CUSTACT_DSRPC_CACHE_POSSCONNSCOUNT_NAME;

	public static string CUSTACT_DSRPC_CACHE_POSSCONNSCOUNT_HELP;

	public static string CUSTACT_DSRPC_CACHE_CREATERATE_NAME;

	public static string CUSTACT_DSRPC_CACHE_CREATERATE_HELP;

	public static string CUSTACT_DSRPC_CACHE_REUSERATE_NAME;

	public static string CUSTACT_DSRPC_CACHE_REUSERATE_HELP;

	public static string CUSTACT_COMBINED_CACHE_CONNCOUNT_NAME;

	public static string CUSTACT_COMBINED_CACHE_CONNCOUNT_HELP;

	static AdwsStrings()
	{
		Type typeFromHandle = typeof(AdwsStrings);
		ResourceManager resourceManager = new ResourceManager(typeFromHandle);
		MemberInfo[] members = typeFromHandle.GetMembers(BindingFlags.Static | BindingFlags.Public);
		foreach (MemberInfo memberInfo in members)
		{
			string text = resourceManager.GetString(memberInfo.Name, CultureInfo.CurrentUICulture);
			typeFromHandle.InvokeMember(memberInfo.Name, BindingFlags.SetField, null, null, new object[1] { text }, CultureInfo.CurrentCulture);
			DescriptionAttribute[] array = (DescriptionAttribute[])memberInfo.GetCustomAttributes(typeof(DescriptionAttribute), inherit: false);
			if (array.Length != 0 && array[0].Description == "Y")
			{
				WebServiceStrings.LongToShortMap[text] = memberInfo.Name;
			}
		}
	}

	internal static void Load()
	{
	}
}

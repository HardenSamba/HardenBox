namespace Microsoft.ActiveDirectory.WebServices;

internal class AdwsMsgIds
{
	internal const long StartupCategory = 1L;

	internal const long ConfigurationCategory = 2L;

	internal const long InstanceCategory = 3L;

	internal const long ConnectionCategory = 4L;

	internal const long CertificateCategory = 5L;

	internal const long AdvertisingCategory = 6L;

	internal const long ShutdownCategory = 7L;

	internal const long AdwsServiceStarting = 1073742824L;

	internal const long AdwsCantStartListeningTimeout = 3221226473L;

	internal const long AdwsCantStartListening = 3221226474L;

	internal const long AdwsAlreadyRunning = 3221226475L;

	internal const long AdwsServiceStart = 1073742828L;

	internal const long AdwsServiceCantAdvertise = 3221226477L;

	internal const long AdwsSuccessfulAdvertise = 1073742830L;

	internal const long UnableToRemovePrivilege = 2147484655L;

	internal const long SuccessfullyRemovedPrivileges = 1073742832L;

	internal const long CantInitPerfCounters = 2147484657L;

	internal const long AdwsCantStartListeningOutOfRangeValue = 3221226482L;

	internal const long AdwsCantCreateNetTcpBinding = 3221226483L;

	internal const long ConfigFileLoadedSuccessfully = 1073742924L;

	internal const long BadValueSyntaxInConfigFile = 2147484749L;

	internal const long ValueLessThanMinInConfigFile = 2147484750L;

	internal const long ValueGreaterThanMaxInConfigFile = 2147484751L;

	internal const long InitPoolExceedsMaxPoolInConfigFile = 2147484752L;

	internal const long MaxRsrvIdleTimeoutExceedsMaxRsrvTimeoutInConfigFile = 2147484753L;

	internal const long MaxEnumctxsSessionExceedsMaxEnumctxsTotalInConfigFile = 2147484754L;

	internal const long ConfigFileNotLoadedSuccessfully = 3221226579L;

	internal const long ConfigFilePreventingStartup = 3221226580L;

	internal const long ConfigFileAppSectionEmptyOnStart = 2147484757L;

	internal const long ConfigFileAppSectionEmpty = 2147484758L;

	internal const long SuccessfullyAddedInstance = 1073743024L;

	internal const long SuccessfullyRemovedInstance = 1073743025L;

	internal const long FailedToAddInstance = 3221226674L;

	internal const long FailedToRemoveInstance = 3221226675L;

	internal const long UnableToReadRegForNtds = 3221226676L;

	internal const long SuccessfullyReadRegForNtdsAfterError = 1073743029L;

	internal const long UnableToDetermineIfGc = 3221226678L;

	internal const long SuccessfullyCheckedGcAfterError = 1073743031L;

	internal const long UnableToReadRegForAdam = 3221226680L;

	internal const long ErrorReadingSettingsForAdamInstance = 2147484857L;

	internal const long SuccessfullyReadRegForAdamAfterError = 1073743034L;

	internal const long UnableToReadRegForSnapshot = 3221226683L;

	internal const long ErrorReadingSettingsForSnapshotInstance = 2147484860L;

	internal const long SuccessfullyReadRegForSnapshotAfterError = 1073743037L;

	internal const long ErrorConnecting = 3221226772L;

	internal const long ErrorBoundAnonymously = 2147484949L;

	internal const long SuccessfulConnection = 1073743126L;

	internal const long MissingServerCert = 1073743224L;

	internal const long FoundServerCert = 1073743225L;

	internal const long UnableToProcessCert = 2147485050L;

	internal const long AdwsMinMsgId = 999L;

	internal const long AdwsMaxMsgId = 1700L;

	internal const long EventLogDisplayName = 5001L;
}

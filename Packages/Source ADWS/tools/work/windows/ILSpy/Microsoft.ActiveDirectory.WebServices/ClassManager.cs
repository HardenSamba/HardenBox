namespace Microsoft.ActiveDirectory.WebServices;

internal static class ClassManager
{
	internal static IDirectoryAction DirectoryAction;

	internal static IDirectoryDataAccess DirectoryDataAccess;

	internal static IContainerMapper ContainerMapper;

	internal static ILdapSessionPool SessionPool;

	internal static EnumerationContextCache EnumerationContextCache;

	internal static InstanceMap GlobalInstanceMap;

	internal static void Start()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ClassManager", "Start: starting...");
		SessionPool = new LdapSessionPoolImplementation();
		DirectoryDataAccess = new DirectoryDataAccessImplementation();
		DirectoryAction = new DirectoryActionImplementation();
		ContainerMapper = new ContainerMapperImplementation();
		EnumerationContextCache = new EnumerationContextCache();
		GlobalInstanceMap = InstanceMap.CreateInstanceMap();
	}

	internal static void Stop()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ClassManager", "Stop: closing down...");
		ContainerMapper = null;
		if (GlobalInstanceMap != null)
		{
			GlobalInstanceMap.Dispose();
			GlobalInstanceMap = null;
		}
		if (EnumerationContextCache != null)
		{
			EnumerationContextCache.Dispose();
			EnumerationContextCache = null;
		}
		if (DirectoryAction != null)
		{
			DirectoryAction.Dispose();
			DirectoryAction = null;
		}
		if (DirectoryDataAccess != null)
		{
			DirectoryDataAccess.Dispose();
			DirectoryDataAccess = null;
		}
		if (SessionPool != null)
		{
			SessionPool.Dispose();
			SessionPool = null;
		}
	}
}

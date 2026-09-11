using System;
using System.Collections;
using System.Configuration;
using System.DirectoryServices.Protocols;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Microsoft.ActiveDirectory.WebServices;

internal static class ConfigurationSettings
{
	private class SettingDefault
	{
		internal string settingName;

		internal Type settingType;

		internal object defaultValue;

		internal SettingDefault(string settingName, object defaultValue, Type settingType)
		{
			this.settingName = settingName;
			this.defaultValue = defaultValue;
			this.settingType = settingType;
		}
	}

	private class SettingLimit
	{
		internal string settingName;

		internal object minValue;

		internal object maxValue;

		internal SettingLimit(string settingName, object minValue, object maxValue)
		{
			this.settingName = settingName;
			this.minValue = minValue;
			this.maxValue = maxValue;
		}
	}

	private static uint initialPoolConnections;

	private static uint maxPoolConnections;

	private static uint maxPercentageReservedConnections;

	private static TimeSpan maxReservedIdleTimeout;

	private static TimeSpan maxReservedTimeout;

	private static uint maxConnectionsPerUser;

	private static TimeSpan maxBindLifetime;

	private static uint maxServerDownRetry;

	private static TimeSpan syntaxCacheEntryLifetime;

	private static TimeSpan maxEnumContextExpiration;

	private static TimeSpan operationTimeout;

	private static TimeSpan maxPullTimeout;

	private static uint maxEnumCtxsPerSession;

	private static uint maxEnumCtxsTotal;

	private static string certName;

	private static uint maxGroupOrMemberEntries;

	private static uint customActionConnectionCount;

	private static TimeSpan customActionIdleConnectionTimeout;

	private static TimeSpan instanceRediscoveryInterval;

	private static uint maxConcurrentCalls;

	private static uint maxConcurrentSessions;

	private static DateTime timeLastConfigFileNotLoadedSuccessfully;

	private static DateTime timeLastConfigFileLoadedSuccessfully;

	private static DateTime timeLastConfigFileAppSectionEmptyOnStart;

	private static DateTime timeLastConfigFileAppSectionEmpty;

	private static DateTime timeLastConfigFileValidation;

	private static DateTime timeLastConfigFileSyntaxError;

	private static DebugLevel debugLevel;

	private static string debugLogFile;

	private const DebugLevel defaultDebugLevel = DebugLevel.None;

	private static SettingDefault[] settingDefaults;

	private static SettingLimit[] settingLimits;

	private static FileSystemWatcher fsWatcher;

	private static ReaderWriterLockSlim rwLock;

	internal static uint InitialPoolConnections
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return initialPoolConnections;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			initialPoolConnections = value;
		}
	}

	internal static uint MaxPoolConnections
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxPoolConnections;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxPoolConnections = value;
		}
	}

	internal static uint MaxPercentageReservedConnections
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxPercentageReservedConnections;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxPercentageReservedConnections = value;
		}
	}

	internal static TimeSpan MaxReservedIdleTimeout
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxReservedIdleTimeout;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxReservedIdleTimeout = value;
		}
	}

	internal static TimeSpan MaxReservedTimeout
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxReservedTimeout;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxReservedTimeout = value;
		}
	}

	internal static uint MaxConnectionsPerUser
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxConnectionsPerUser;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxConnectionsPerUser = value;
		}
	}

	internal static TimeSpan MaxBindLifetime
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxBindLifetime;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxBindLifetime = value;
		}
	}

	internal static uint MaxServerDownRetry
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxServerDownRetry;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxServerDownRetry = value;
		}
	}

	internal static TimeSpan SyntaxCacheEntryLifetime
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return syntaxCacheEntryLifetime;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			syntaxCacheEntryLifetime = value;
		}
	}

	internal static TimeSpan MaxEnumContextExpiration
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxEnumContextExpiration;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxEnumContextExpiration = value;
		}
	}

	internal static TimeSpan OperationTimeout
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return operationTimeout;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			operationTimeout = value;
		}
	}

	internal static TimeSpan MaxPullTimeout
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxPullTimeout;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxPullTimeout = value;
		}
	}

	internal static uint MaxEnumCtxsPerSession
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxEnumCtxsPerSession;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxEnumCtxsPerSession = value;
		}
	}

	internal static uint MaxEnumCtxsTotal
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxEnumCtxsTotal;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxEnumCtxsTotal = value;
		}
	}

	internal static string CertName
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return certName;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			certName = value;
		}
	}

	internal static uint MaxGroupOrMemberEntries
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxGroupOrMemberEntries;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxGroupOrMemberEntries = value;
		}
	}

	internal static uint CustomActionConnectionCount
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return customActionConnectionCount;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			customActionConnectionCount = value;
		}
	}

	internal static TimeSpan CustomActionIdleConnectionTimeout
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return customActionIdleConnectionTimeout;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			customActionIdleConnectionTimeout = value;
		}
	}

	internal static TimeSpan InstanceRediscoveryInterval
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return instanceRediscoveryInterval;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			instanceRediscoveryInterval = value;
		}
	}

	internal static uint MaxConcurrentCalls
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxConcurrentCalls;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxConcurrentCalls = value;
		}
	}

	internal static uint MaxConcurrentSessions
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return maxConcurrentSessions;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			maxConcurrentSessions = value;
		}
	}

	internal static DebugLevel DebugLevel
	{
		get
		{
			return debugLevel;
		}
		private set
		{
			debugLevel = value;
		}
	}

	internal static string DebugLogFile
	{
		get
		{
			rwLock.EnterReadLock();
			try
			{
				return debugLogFile;
			}
			finally
			{
				rwLock.ExitReadLock();
			}
		}
		private set
		{
			debugLogFile = value;
		}
	}

	static ConfigurationSettings()
	{
		debugLevel = DebugLevel.None;
		settingDefaults = new SettingDefault[23]
		{
			new SettingDefault("InitialPoolConnections", 5u, typeof(uint)),
			new SettingDefault("MaxPoolConnections", 10u, typeof(uint)),
			new SettingDefault("MaxPercentageReservedConnections", 50u, typeof(uint)),
			new SettingDefault("MaxReservedIdleTimeout", new TimeSpan(0, 2, 0), typeof(TimeSpan)),
			new SettingDefault("MaxReservedTimeout", new TimeSpan(0, 30, 0), typeof(TimeSpan)),
			new SettingDefault("MaxConnectionsPerUser", 5u, typeof(uint)),
			new SettingDefault("MaxBindLifetime", new TimeSpan(0, 15, 0), typeof(TimeSpan)),
			new SettingDefault("MaxServerDownRetry", 10u, typeof(uint)),
			new SettingDefault("SyntaxCacheEntryLifetime", new TimeSpan(1, 0, 0), typeof(TimeSpan)),
			new SettingDefault("MaxEnumContextExpiration", new TimeSpan(0, 30, 0), typeof(TimeSpan)),
			new SettingDefault("OperationTimeout", new TimeSpan(0, 2, 0), typeof(TimeSpan)),
			new SettingDefault("MaxPullTimeout", new TimeSpan(0, 2, 0), typeof(TimeSpan)),
			new SettingDefault("MaxEnumCtxsPerSession", 5u, typeof(uint)),
			new SettingDefault("MaxEnumCtxsTotal", 100u, typeof(uint)),
			new SettingDefault("CertName", null, typeof(string)),
			new SettingDefault("MaxGroupOrMemberEntries", 5000u, typeof(uint)),
			new SettingDefault("CustomActionConnectionCount", 10u, typeof(uint)),
			new SettingDefault("CustomActionIdleConnectionTimeout", new TimeSpan(0, 2, 0), typeof(TimeSpan)),
			new SettingDefault("InstanceRediscoveryInterval", new TimeSpan(0, 1, 0), typeof(TimeSpan)),
			new SettingDefault("MaxConcurrentCalls", 32u, typeof(uint)),
			new SettingDefault("MaxConcurrentSessions", 500u, typeof(uint)),
			new SettingDefault("DebugLevel", DebugLevel.None, typeof(DebugLevel)),
			new SettingDefault("DebugLogFile", null, typeof(string))
		};
		settingLimits = new SettingLimit[21]
		{
			new SettingLimit("InitialPoolConnections", 0u, null),
			new SettingLimit("MaxPoolConnections", 1u, null),
			new SettingLimit("MaxPercentageReservedConnections", 0u, 100u),
			new SettingLimit("MaxReservedIdleTimeout", new TimeSpan(0, 0, 10), null),
			new SettingLimit("MaxReservedTimeout", new TimeSpan(0, 0, 10), null),
			new SettingLimit("MaxConnectionsPerUser", 1u, null),
			new SettingLimit("MaxBindLifetime", new TimeSpan(0, 5, 0), null),
			new SettingLimit("MaxServerDownRetry", 1u, null),
			new SettingLimit("SyntaxCacheEntryLifetime", new TimeSpan(0, 5, 0), null),
			new SettingLimit("MaxEnumContextExpiration", new TimeSpan(0, 0, 10), null),
			new SettingLimit("OperationTimeout", new TimeSpan(0, 0, 10), null),
			new SettingLimit("MaxPullTimeout", new TimeSpan(0, 0, 10), null),
			new SettingLimit("MaxEnumCtxsPerSession", 1u, null),
			new SettingLimit("MaxEnumCtxsTotal", 1u, null),
			new SettingLimit("MaxGroupOrMemberEntries", 1u, null),
			new SettingLimit("CustomActionConnectionCount", 1u, null),
			new SettingLimit("CustomActionIdleConnectionTimeout", new TimeSpan(0, 0, 10), null),
			new SettingLimit("InstanceRediscoveryInterval", new TimeSpan(0, 0, 10), null),
			new SettingLimit("MaxConcurrentCalls", 16u, 2147483647u),
			new SettingLimit("MaxConcurrentSessions", 10u, 2147483647u),
			new SettingLimit("DebugLevel", DebugLevel.Info, DebugLevel.None)
		};
		fsWatcher = null;
		rwLock = null;
		rwLock = new ReaderWriterLockSlim();
		timeLastConfigFileNotLoadedSuccessfully = DateTime.UtcNow.AddSeconds(-10.0);
		timeLastConfigFileLoadedSuccessfully = DateTime.UtcNow.AddSeconds(-10.0);
		timeLastConfigFileAppSectionEmptyOnStart = DateTime.UtcNow.AddSeconds(-10.0);
		timeLastConfigFileAppSectionEmpty = DateTime.UtcNow.AddSeconds(-10.0);
		timeLastConfigFileValidation = DateTime.UtcNow.AddSeconds(-10.0);
		timeLastConfigFileSyntaxError = DateTime.UtcNow.AddSeconds(-10.0);
		LoadConfigSettingsFromFile();
	}

	internal static void Dispose()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "Dispose: disposing");
		if (rwLock != null)
		{
			rwLock.Dispose();
			rwLock = null;
		}
	}

	internal static void StartConfigurationLoading()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "StartConfigurationLoading: entered");
		fsWatcher = new FileSystemWatcher();
		string configurationFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
		fsWatcher.Path = Path.GetDirectoryName(configurationFile);
		fsWatcher.Filter = Path.GetFileName(configurationFile);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "StartConfigurationLoading: establishing watcher on {0}", configurationFile);
		fsWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
		fsWatcher.Changed += OnChanged;
		fsWatcher.Created += OnChanged;
		fsWatcher.Deleted += OnChanged;
		fsWatcher.Renamed += OnRenamed;
		fsWatcher.EnableRaisingEvents = true;
		LoadConfigSettingsFromFile();
	}

	internal static void StopConfigurationLoading()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "StopConfigurationLoading: entered");
		if (fsWatcher != null)
		{
			fsWatcher.EnableRaisingEvents = false;
			fsWatcher.Dispose();
			fsWatcher = null;
		}
	}

	private static void OnChanged(object source, FileSystemEventArgs e)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "OnChanged: file changed (0), reloading", e.ChangeType.ToString());
		LoadConfigSettingsFromFile();
	}

	private static void OnRenamed(object source, RenamedEventArgs e)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "OnRenamed: file renamed, reloading");
		LoadConfigSettingsFromFile();
	}

	private static void LoadConfigSettingsFromFile()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "LoadConfigSettingsFromFile: entered");
		bool flag = true;
		AppSettingsSection appSettingsSection = null;
		rwLock.EnterWriteLock();
		try
		{
			try
			{
				ConfigurationManager.RefreshSection("appSettings");
				appSettingsSection = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).AppSettings;
			}
			catch (ConfigurationErrorsException ex)
			{
				if (ClassManager.SessionPool == null)
				{
					throw;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "ConfigurationSettings", "LoadConfigSettingsFromFile: ConfigurationErrorsException: {0}", ex.Message);
				if (DateTime.UtcNow >= timeLastConfigFileNotLoadedSuccessfully)
				{
					ADWSEventlog.Log(3221226579L, ex.Message);
					timeLastConfigFileNotLoadedSuccessfully = DateTime.UtcNow.AddSeconds(AdwsConstants.EventLogNoiseSuppressionInterval);
				}
				flag = false;
			}
			if (appSettingsSection != null && appSettingsSection.Settings.Count > 0)
			{
				SettingDefault[] array = settingDefaults;
				foreach (SettingDefault settingDefault in array)
				{
					if (((IList)appSettingsSection.Settings.AllKeys).Contains((object)settingDefault.settingName))
					{
						string value = appSettingsSection.Settings[settingDefault.settingName].Value;
						object value2 = value;
						if (ConvertValue(value, settingDefault.settingType, out value2))
						{
							GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "LoadConfigSettingsFromFile: using loaded value {0} for {1}", (value2 != null) ? value2.ToString() : "NULL", settingDefault.settingName);
							LoadSettingProperty(settingDefault.settingName, value2);
							continue;
						}
						GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConfigurationSettings", "LoadConfigSettingsFromFile: can't parse value {0} for {1}, using default value {2}", (value != null) ? value.ToString() : "NULL", settingDefault.settingName, (settingDefault.defaultValue != null) ? settingDefault.defaultValue.ToString() : "NULL");
						LoadSettingProperty(settingDefault.settingName, settingDefault.defaultValue);
						if (DateTime.UtcNow >= timeLastConfigFileSyntaxError)
						{
							ADWSEventlog.Log(2147484749L, settingDefault.settingName, value, settingDefault.defaultValue);
						}
						flag = false;
					}
					else
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "LoadConfigSettingsFromFile: no value specified for {0}, using default value {1}", settingDefault.settingName, (settingDefault.defaultValue != null) ? settingDefault.defaultValue.ToString() : "NULL");
						LoadSettingProperty(settingDefault.settingName, settingDefault.defaultValue);
					}
				}
				if (!flag)
				{
					timeLastConfigFileSyntaxError = DateTime.UtcNow.AddSeconds(AdwsConstants.EventLogNoiseSuppressionInterval);
				}
				if (!ValidateSettingLimits())
				{
					flag = false;
				}
				timeLastConfigFileValidation = DateTime.UtcNow.AddSeconds(AdwsConstants.EventLogNoiseSuppressionInterval);
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConfigurationSettings", "LoadConfigSettingsFromFile: couldn't get app settings, using defaults");
				LoadDefaultSettings();
				if (flag)
				{
					if (ClassManager.SessionPool == null)
					{
						if (DateTime.UtcNow >= timeLastConfigFileAppSectionEmptyOnStart)
						{
							ADWSEventlog.Log(2147484757L);
							timeLastConfigFileAppSectionEmptyOnStart = DateTime.UtcNow.AddSeconds(AdwsConstants.EventLogNoiseSuppressionInterval);
						}
					}
					else if (DateTime.UtcNow >= timeLastConfigFileAppSectionEmpty)
					{
						ADWSEventlog.Log(2147484758L);
						timeLastConfigFileAppSectionEmpty = DateTime.UtcNow.AddSeconds(AdwsConstants.EventLogNoiseSuppressionInterval);
					}
					flag = false;
				}
			}
			if (flag && DateTime.UtcNow >= timeLastConfigFileLoadedSuccessfully)
			{
				ADWSEventlog.Log(1073742924L);
				timeLastConfigFileLoadedSuccessfully = DateTime.UtcNow.AddSeconds(AdwsConstants.EventLogNoiseSuppressionInterval);
			}
		}
		finally
		{
			rwLock.ExitWriteLock();
		}
	}

	private static bool ValidateSettingLimits()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "ValidateSettingLimits: entered");
		bool result = true;
		SettingLimit[] array = settingLimits;
		foreach (SettingLimit settingLimit in array)
		{
			GetSettingField(settingLimit.settingName, out var settingValue);
			if (settingValue == null)
			{
				continue;
			}
			IComparable comparable = settingValue as IComparable;
			if (settingLimit.minValue != null && comparable.CompareTo(settingLimit.minValue) < 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConfigurationSettings", "ValidateSettingLimits: setting {0} value {1} is less than allowed min {1}, using min as value", settingLimit.settingName, settingValue.ToString(), settingLimit.minValue.ToString());
				if (DateTime.UtcNow >= timeLastConfigFileValidation)
				{
					ADWSEventlog.Log(2147484750L, settingLimit.settingName, settingValue, settingLimit.minValue);
				}
				result = false;
				LoadSettingProperty(settingLimit.settingName, settingLimit.minValue);
			}
			if (settingLimit.maxValue != null && comparable.CompareTo(settingLimit.maxValue) > 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConfigurationSettings", "ValidateSettingLimits: setting {0} value {1} is more than allowed max {1}, using max as value", settingLimit.settingName, settingValue.ToString(), settingLimit.maxValue.ToString());
				if (DateTime.UtcNow >= timeLastConfigFileValidation)
				{
					ADWSEventlog.Log(2147484751L, settingLimit.settingName, settingValue, settingLimit.maxValue);
				}
				result = false;
				LoadSettingProperty(settingLimit.settingName, settingLimit.maxValue);
			}
		}
		if (initialPoolConnections > maxPoolConnections)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConfigurationSettings", "ValidateSettingLimits: inital pool connections ({0}) greater than max pool connections ({1}), setting initial to max", initialPoolConnections, maxPoolConnections);
			if (DateTime.UtcNow >= timeLastConfigFileValidation)
			{
				ADWSEventlog.Log(2147484752L, initialPoolConnections, maxPoolConnections);
			}
			result = false;
			initialPoolConnections = maxPoolConnections;
		}
		if (maxReservedIdleTimeout > maxReservedTimeout)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConfigurationSettings", "ValidateSettingLimits: max reserved idle timeout ({0}) greater than max reserved timeout ({1}), setting max idle to max reserved", maxReservedIdleTimeout.ToString(), maxReservedTimeout.ToString());
			if (DateTime.UtcNow >= timeLastConfigFileValidation)
			{
				ADWSEventlog.Log(2147484753L, maxReservedIdleTimeout, maxReservedTimeout);
			}
			result = false;
			maxReservedIdleTimeout = maxReservedTimeout;
		}
		if (maxEnumCtxsPerSession > maxEnumCtxsTotal)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ConfigurationSettings", "ValidateSettingLimits: max enum ctxs per-session ({0}) greater than max enum ctxs total ({1}), setting per-session to total", maxEnumCtxsPerSession, maxEnumCtxsTotal);
			if (DateTime.UtcNow >= timeLastConfigFileValidation)
			{
				ADWSEventlog.Log(2147484754L, maxEnumCtxsPerSession, maxEnumCtxsTotal);
			}
			result = false;
			maxEnumCtxsPerSession = maxEnumCtxsTotal;
		}
		return result;
	}

	private static void LoadDefaultSettings()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "LoadDefaultSettings: entered");
		SettingDefault[] array = settingDefaults;
		foreach (SettingDefault settingDefault in array)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "ConfigurationSettings", "LoadDefaultSettings: using default {0} for {1}", (settingDefault.defaultValue != null) ? settingDefault.defaultValue.ToString() : "NULL", settingDefault.settingName);
			LoadSettingProperty(settingDefault.settingName, settingDefault.defaultValue);
		}
	}

	private static void LoadSettingProperty(string settingName, object settingValue)
	{
		typeof(ConfigurationSettings).InvokeMember(settingName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.SetProperty, null, null, new object[1] { settingValue }, CultureInfo.InvariantCulture);
	}

	private static void GetSettingField(string settingName, out object settingValue)
	{
		settingValue = null;
		Type typeFromHandle = typeof(ConfigurationSettings);
		string name = char.ToLower(settingName[0], CultureInfo.InvariantCulture) + settingName.Substring(1);
		settingValue = typeFromHandle.InvokeMember(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.GetField, null, null, new object[0], CultureInfo.InvariantCulture);
	}

	private static bool ConvertValue(string rawValue, Type settingType, out object value)
	{
		value = null;
		bool flag = true;
		if (settingType == typeof(string))
		{
			if (rawValue != null && rawValue.Length == 0)
			{
				rawValue = null;
			}
			value = rawValue;
		}
		else if (rawValue != null)
		{
			if (settingType == typeof(TimeSpan))
			{
				flag = TimeSpan.TryParse(rawValue, out var result);
				value = result;
			}
			else if (settingType == typeof(uint))
			{
				flag = uint.TryParse(rawValue, out var result2);
				value = result2;
			}
			else if (settingType == typeof(AuthType) || settingType == typeof(DebugLevel))
			{
				try
				{
					value = Enum.Parse(settingType, rawValue);
				}
				catch (ArgumentException)
				{
					flag = false;
				}
				if (flag && !Enum.IsDefined(settingType, value))
				{
					flag = false;
				}
			}
			else
			{
				flag = false;
			}
		}
		else
		{
			flag = false;
		}
		return flag;
	}
}

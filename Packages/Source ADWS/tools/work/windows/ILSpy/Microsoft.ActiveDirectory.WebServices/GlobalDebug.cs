#define TRACE
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Microsoft.ActiveDirectory.WebServices;

internal static class GlobalDebug
{
	private static bool isInitialized;

	public static bool Error => DebugLevel.Error >= ConfigurationSettings.DebugLevel;

	public static bool Warn => DebugLevel.Warn >= ConfigurationSettings.DebugLevel;

	public static bool Info => DebugLevel.Info >= ConfigurationSettings.DebugLevel;

	static GlobalDebug()
	{
		InitializeGlobalDebug();
	}

	internal static void InitializeGlobalDebug()
	{
		if (!isInitialized)
		{
			string debugLogFile = ConfigurationSettings.DebugLogFile;
			if (debugLogFile != null)
			{
				foreach (TraceListener listener in Trace.Listeners)
				{
					if (listener is DefaultTraceListener)
					{
						((DefaultTraceListener)listener).LogFileName = debugLogFile;
					}
				}
				Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, "ADWS Log - AppDomain {0} with ID {1} - {2} ({3})", Thread.GetDomain().FriendlyName, Thread.GetDomainID(), DateTime.Now, TimeZoneInfo.Local));
				Trace.WriteLine(string.Format(CultureInfo.InvariantCulture, "OS Version {0} - CLR Version {1}", new object[2]
				{
					Environment.OSVersion.VersionString,
					Environment.Version.ToString()
				}));
			}
		}
		isInitialized = true;
	}

	internal static void FlushGlobalDebug()
	{
		Trace.Flush();
	}

	public static void WriteLineIf(bool f, string category, string message, params object[] args)
	{
		if (f && isInitialized)
		{
			message = "[" + DateTime.Now.ToString() + "] [" + Thread.CurrentThread.ManagedThreadId.ToString("x", CultureInfo.InvariantCulture) + "] " + message;
			Trace.WriteLineIf(f, string.Format(CultureInfo.InvariantCulture, message, args), category);
		}
	}

	public static void WriteLineIf(bool f, string category, string message)
	{
		if (f && isInitialized)
		{
			message = "[" + DateTime.Now.ToString() + "] [" + Thread.CurrentThread.ManagedThreadId.ToString("x", CultureInfo.InvariantCulture) + "] " + message;
			Trace.WriteLineIf(f, message, category);
		}
	}
}

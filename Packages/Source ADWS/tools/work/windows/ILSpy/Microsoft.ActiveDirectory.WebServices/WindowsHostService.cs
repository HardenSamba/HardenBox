using System;
using System.ServiceModel;
using System.ServiceProcess;
using System.Threading;

namespace Microsoft.ActiveDirectory.WebServices;

internal class WindowsHostService : ServiceBase
{
	private static ServiceHost[] Hosts;

	private static ManualResetEvent ServiceInitializationDone = new ManualResetEvent(initialState: false);

	private static Thread ServiceStartThread = null;

	public WindowsHostService()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "WindowsHostService constructed");
		base.ServiceName = AdwsStrings.AdwsName;
		base.CanStop = true;
	}

	protected override void OnStart(string[] args)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "OnStart: entering.");
		RequestAdditionalTime(AdwsConstants.scmWaitTime);
		ServiceStartThread = new Thread(StartService);
		ServiceStartThread.IsBackground = false;
		ServiceStartThread.Name = "WindowsHostService - StartService";
		ServiceStartThread.Start(this);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "OnStart: ServiceStart thread started.");
	}

	public static void StartService(object o)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "StartService: entering.");
		WindowsHostService windowsHostService = o as WindowsHostService;
		try
		{
			try
			{
				PerfCounters.InstallCountersIfNeeded();
				PerfCounters.Initialize();
			}
			catch (InvalidOperationException ex)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "StartService: Got a InvalidOperationException while initializing performance counters, {0}", ex.ToString());
				ADWSEventlog.Log(2147484657L);
			}
			ServiceHost serviceHost = (ADWSHost)new ADWSHostFactory().CreateServiceHost(string.Empty, new Uri[1] { AdwsConstants.WcfNetEndpointBase });
			try
			{
				try
				{
					serviceHost.Open();
				}
				catch (System.TimeoutException ex2)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "StartService: timed-out starting WCF, {0}", ex2.ToString());
					ADWSEventlog.Log(3221226473L);
					windowsHostService.ExitCode = 1460;
					throw;
				}
				catch (AddressAlreadyInUseException ex3)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "StartService: service is already running, {0}", ex3.ToString());
					ADWSEventlog.Log(3221226475L);
					windowsHostService.ExitCode = 1056;
					throw;
				}
				catch (InvalidOperationException ex4)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "StartService: couldn't start WCF (invalid operation), {0}", ex4.ToString());
					ADWSEventlog.Log(3221226474L);
					windowsHostService.ExitCode = 59;
					throw;
				}
				catch (ArgumentException ex5)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "StartService: couldn't start WCF (invalid argument), {0}", ex5.ToString());
					if (ex5 is ArgumentOutOfRangeException)
					{
						GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "StartService: Out of range value supplied for {0} configuration setting", ex5.ParamName.ToString());
						ADWSEventlog.Log(3221226482L, ex5.ParamName.ToString());
						windowsHostService.ExitCode = 87;
					}
					else
					{
						ADWSEventlog.Log(3221226474L);
						windowsHostService.ExitCode = 59;
					}
					throw;
				}
				catch (CommunicationException ex6)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "StartService: couldn't start WCF, {0}", ex6.ToString());
					ADWSEventlog.Log(3221226474L);
					windowsHostService.ExitCode = 59;
					throw;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "StartService: The ADWS Service opened.");
				Hosts = new ServiceHost[1] { serviceHost };
				if (SetLocationState(locatable: true))
				{
					ADWSEventlog.Log(1073742830L);
				}
				ADWSEventlog.Log(1073742828L);
			}
			catch (Exception ex7)
			{
				if (serviceHost != null)
				{
					((IDisposable)serviceHost).Dispose();
					serviceHost = null;
				}
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "StartService: got an exception: {0}", ex7.ToString());
				throw;
			}
		}
		finally
		{
			ServiceInitializationDone.Set();
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "StartService: thread completed");
		}
	}

	protected override void OnStop()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "OnStop: entering.");
		ServiceInitializationDone.WaitOne();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "OnStop: proceeding.");
		SetLocationState(locatable: false);
		ServiceHost[] hosts = Hosts;
		foreach (ServiceHost serviceHost in hosts)
		{
			if (serviceHost != null)
			{
				if (CommunicationState.Closed != serviceHost.State)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "OnStop: closing the {0} WCF host.", serviceHost.GetType().ToString());
					serviceHost.Close();
				}
				((IDisposable)serviceHost).Dispose();
			}
		}
		base.ExitCode = 0;
	}

	private static bool SetLocationState(bool locatable)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "WindowsHostService", "SetLocationState: trying to set location state to '{0}'", locatable.ToString());
		uint num = NativeMethods.NetLogonSetServiceBits(null, NativeMethods.ServiceBitFlags.DS_WS_FLAG, locatable ? NativeMethods.ServiceBitFlags.DS_WS_FLAG : ((NativeMethods.ServiceBitFlags)0u));
		if (num != 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "WindowsHostService", "SetLocationState: trying to set location state to '{0}' failed with {1}", locatable.ToString(), num);
			ADWSEventlog.Log(3221226477L, locatable);
		}
		return num == 0;
	}
}

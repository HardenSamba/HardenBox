using System;
using System.ServiceProcess;

namespace Microsoft.ActiveDirectory.WebServices;

public class Program
{
	public static void Main(string[] args)
	{
		bool flag = false;
		flag = ADWSEventlog.Start();
		ADWSEventlog.Log(1073742824L);
		try
		{
			AdwsStrings.Load();
			GlobalDebug.InitializeGlobalDebug();
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "ADWS", "Main: entered");
		}
		catch (TypeInitializationException ex)
		{
			ADWSEventlog.Log(3221226580L, ex.GetBaseException().Message);
			return;
		}
		if (!flag)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "ADWS", "Main: Event log start failed... exiting.");
			return;
		}
		Utils.RemoveUnnecessaryPriviledges();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "Program", "Main: Starting Windows service host.");
		ServiceBase.Run(new ServiceBase[1]
		{
			new WindowsHostService()
		});
		PerfCounters.Uninitialize();
		GlobalDebug.FlushGlobalDebug();
	}
}

using System;
using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices;

internal class ADWSHost : ServiceHost
{
	public ADWSHost(Uri[] baseAddresses)
		: base(typeof(ActiveDirectoryWebService), baseAddresses)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHost", "ADWSHost constructed");
	}

	protected override void InitializeRuntime()
	{
		ConfigurationSettings.StartConfigurationLoading();
		ClassManager.Start();
		CustomActionCaches.StartCaches();
		base.Credentials.WindowsAuthentication.AllowAnonymousLogons = false;
		base.InitializeRuntime();
	}

	protected override void OnClosed()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHost", "OnClosed: entered");
		CustomActionCaches.StopCaches();
		ClassManager.Stop();
		ConfigurationSettings.StopConfigurationLoading();
		ConfigurationSettings.Dispose();
		base.OnClosed();
	}
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Security;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSEnumeration;
using Microsoft.ActiveDirectory.WebServices.Shared.WebServices.WSTransfer;

namespace Microsoft.ActiveDirectory.WebServices;

internal class ADWSHostFactory : ServiceHostFactoryBase
{
	public override ServiceHostBase CreateServiceHost(string constructorString, Uri[] baseAddresses)
	{
		ADWSHost aDWSHost = new ADWSHost(baseAddresses);
		try
		{
			bool num = ProvisionCertificate(aDWSHost);
			AddMexBehavior(aDWSHost);
			AddServiceThrottlingBehavior(aDWSHost, ConfigurationSettings.MaxConcurrentCalls, ConfigurationSettings.MaxConcurrentSessions);
			List<AdwsBindingDescriptor> list = new List<AdwsBindingDescriptor>
			{
				new AdwsTransportBindingDescriptor(AdwsConstants.WcfWindowsEndpointPrefix, TcpClientCredentialType.Windows)
			};
			if (num)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "CreateServiceHost: including UserName endpoints");
				list.Add(new AdwsTransportWithMessageCredentialBindingDescriptor(AdwsConstants.WcfUserNameEndpointPrefix, MessageCredentialType.UserName));
			}
			foreach (AdwsBindingDescriptor item in list)
			{
				string endpointPrefix = item.EndpointPrefix;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "CreateServiceHost: adding endpoints for {0}", endpointPrefix);
				aDWSHost.AddServiceEndpoint(typeof(IResource), CreateAdwsBinding(item), endpointPrefix + AdwsConstants.WcfResourceEndpoint);
				aDWSHost.AddServiceEndpoint(typeof(IResourceFactory), CreateAdwsBinding(item), endpointPrefix + AdwsConstants.WcfResourceFactoryEndpoint);
				aDWSHost.AddServiceEndpoint(typeof(IEnumeration), CreateAdwsBinding(item), endpointPrefix + AdwsConstants.WcfEnumEndpoint);
				aDWSHost.AddServiceEndpoint(typeof(IActiveDirectoryAccountManagement), CreateAdwsBinding(item), endpointPrefix + AdwsConstants.WcfAcctMgmtEndpoint);
				aDWSHost.AddServiceEndpoint(typeof(IActiveDirectoryTopologyManagementV1), CreateAdwsBinding(item), endpointPrefix + AdwsConstants.WcfTopoMgmtEndpoint);
			}
			aDWSHost.AddServiceEndpoint("IMetadataExchange", MetadataExchangeBindings.CreateMexTcpBinding(), AdwsConstants.WcfMexEndpoint);
			return aDWSHost;
		}
		catch (Exception)
		{
			if (aDWSHost != null)
			{
				((IDisposable)aDWSHost).Dispose();
				aDWSHost = null;
			}
			throw;
		}
	}

	private void AddMexBehavior(ADWSHost adwsHost)
	{
		ServiceMetadataBehavior serviceMetadataBehavior = adwsHost.Description.Behaviors.Find<ServiceMetadataBehavior>();
		if (serviceMetadataBehavior == null)
		{
			serviceMetadataBehavior = new ServiceMetadataBehavior();
		}
		serviceMetadataBehavior.HttpGetEnabled = false;
		serviceMetadataBehavior.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
		adwsHost.Description.Behaviors.Add(serviceMetadataBehavior);
	}

	private void AddServiceThrottlingBehavior(ADWSHost adwsHost, uint maxCalls, uint maxSessions)
	{
		ServiceThrottlingBehavior serviceThrottlingBehavior = adwsHost.Description.Behaviors.Find<ServiceThrottlingBehavior>();
		if (serviceThrottlingBehavior == null)
		{
			serviceThrottlingBehavior = new ServiceThrottlingBehavior();
		}
		serviceThrottlingBehavior.MaxConcurrentCalls = (int)maxCalls;
		serviceThrottlingBehavior.MaxConcurrentSessions = (int)maxSessions;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "AddServiceThrottlingBehavior: MaxConcurrentCalls={0}, MaxConcurrentSessions={1}", serviceThrottlingBehavior.MaxConcurrentCalls, serviceThrottlingBehavior.MaxConcurrentSessions);
		adwsHost.Description.Behaviors.Add(serviceThrottlingBehavior);
	}

	private NetTcpBinding CreateAdwsBinding(AdwsBindingDescriptor endpointBinding)
	{
		if (endpointBinding is AdwsTransportWithMessageCredentialBindingDescriptor)
		{
			return CreateAdwsTransportWithMessageCredentialBinding(((AdwsTransportWithMessageCredentialBindingDescriptor)endpointBinding).MessageCredentialType);
		}
		return CreateAdwsTransportBinding(((AdwsTransportBindingDescriptor)endpointBinding).TcpClientCredentialType);
	}

	private NetTcpBinding CreateAdwsTransportWithMessageCredentialBinding(MessageCredentialType messageCredentialType)
	{
		NetTcpBinding netTcpBinding;
		try
		{
			netTcpBinding = new NetTcpBinding(AdwsConstants.NetTcpBindingConfigName);
		}
		catch (KeyNotFoundException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "CreateAdwsTransportWithMessageCredentialBinding: no {0} section in config file", AdwsConstants.NetTcpBindingConfigName);
			netTcpBinding = new NetTcpBinding();
		}
		catch (ConfigurationErrorsException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "ADWSHostFactory", "CreateAdwsTransportWithMessageCredentialBinding: ConfigurationErrorsException: {0}", ex2.ToString());
			ADWSEventlog.Log(3221226483L, AdwsConstants.NetTcpBindingConfigName, ex2.Message);
			throw;
		}
		netTcpBinding.Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory";
		netTcpBinding.Security.Mode = SecurityMode.TransportWithMessageCredential;
		netTcpBinding.Security.Transport.ProtectionLevel = ProtectionLevel.EncryptAndSign;
		netTcpBinding.Security.Message.ClientCredentialType = messageCredentialType;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "CreateAdwsTransportWithMessageCredentialBinding: MaxReceivedMessageSize={0}, ReceiveTimeout={1}", netTcpBinding.MaxReceivedMessageSize, netTcpBinding.ReceiveTimeout);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "CreateAdwsTransportWithMessageCredentialBinding: MaxDepth={0}, MaxArrayLength={1}, MaxStringContentLength={2}", netTcpBinding.ReaderQuotas.MaxDepth, netTcpBinding.ReaderQuotas.MaxArrayLength, netTcpBinding.ReaderQuotas.MaxStringContentLength);
		return netTcpBinding;
	}

	private NetTcpBinding CreateAdwsTransportBinding(TcpClientCredentialType tcpClientCredentialType)
	{
		NetTcpBinding netTcpBinding;
		try
		{
			netTcpBinding = new NetTcpBinding(AdwsConstants.NetTcpBindingConfigName);
		}
		catch (KeyNotFoundException)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "CreateAdwsTransportBinding: no {0} section in config file", AdwsConstants.NetTcpBindingConfigName);
			netTcpBinding = new NetTcpBinding();
		}
		catch (ConfigurationErrorsException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "ADWSHostFactory", "CreateAdwsTransportBinding: ConfigurationErrorsException: {0}", ex2.ToString());
			ADWSEventlog.Log(3221226483L, AdwsConstants.NetTcpBindingConfigName, ex2.Message);
			throw;
		}
		netTcpBinding.Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory";
		netTcpBinding.Security.Mode = SecurityMode.Transport;
		netTcpBinding.Security.Transport.ProtectionLevel = ProtectionLevel.EncryptAndSign;
		netTcpBinding.Security.Transport.ClientCredentialType = tcpClientCredentialType;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "CreateAdwsTransportBinding: MaxReceivedMessageSize={0}, ReceiveTimeout={1}", netTcpBinding.MaxReceivedMessageSize, netTcpBinding.ReceiveTimeout);
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "CreateAdwsTransportBinding: MaxDepth={0}, MaxArrayLength={1}, MaxStringContentLength={2}", netTcpBinding.ReaderQuotas.MaxDepth, netTcpBinding.ReaderQuotas.MaxArrayLength, netTcpBinding.ReaderQuotas.MaxStringContentLength);
		return netTcpBinding;
	}

	private bool ProvisionCertificate(ServiceHost host)
	{
		string text = ConfigurationSettings.CertName;
		if (text == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: using host name for certificate name");
			text = Utils.GetComputerDnsName();
		}
		if (text == null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "ADWSHostFactory", "ProvisionCertificate: couldn't retrieve host name");
			ADWSEventlog.Log(1073743224L, text);
			return false;
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "ProvisionCertificate: using cert name {0}", text);
		try
		{
			host.Credentials.ServiceCertificate.SetCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindBySubjectName, text);
		}
		catch (InvalidOperationException ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: caught a InvalidOperationException: {0}", ex.ToString());
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: skipping certificate provisioning");
			ADWSEventlog.Log(1073743224L, text);
			return false;
		}
		try
		{
			if (!(host.Credentials.ServiceCertificate.Certificate.PrivateKey is ICspAsymmetricAlgorithm cspAsymmetricAlgorithm) || cspAsymmetricAlgorithm.CspKeyContainerInfo.KeyNumber != KeyNumber.Exchange)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: certificate cannot do key exchange: {0}", host.Credentials.ServiceCertificate.Certificate.SubjectName.Name);
				GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: skipping certificate provisioning");
				ADWSEventlog.Log(2147485050L, text);
				return false;
			}
		}
		catch (SecurityException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: caught a SecurityException: {0}", ex2.ToString());
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: skipping certificate provisioning");
			ADWSEventlog.Log(2147485050L, text);
			return false;
		}
		catch (CryptographicUnexpectedOperationException ex3)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: caught a CryptographicUnexpectedOperationException: {0}", ex3.ToString());
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: skipping certificate provisioning");
			ADWSEventlog.Log(2147485050L, text);
			return false;
		}
		catch (CryptographicException ex4)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: caught a CryptographicException: {0}", ex4.ToString());
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: skipping certificate provisioning");
			ADWSEventlog.Log(2147485050L, text);
			return false;
		}
		catch (NotSupportedException ex5)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: caught a NotSupportedException: {0}", ex5.ToString());
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: skipping certificate provisioning");
			ADWSEventlog.Log(2147485050L, text);
			return false;
		}
		catch (ArgumentNullException ex6)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: caught a ArgumentNull: {0}", ex6.ToString());
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: skipping certificate provisioning");
			ADWSEventlog.Log(2147485050L, text);
			return false;
		}
		catch (ArgumentException ex7)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: caught an ArgumentException: {0}", ex7.ToString());
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "ADWSHostFactory", "ProvisionCertificate: skipping certificate provisioning");
			ADWSEventlog.Log(2147485050L, text);
			return false;
		}
		ADWSEventlog.Log(1073743225L, text);
		host.Credentials.IssuedTokenAuthentication.AllowUntrustedRsaIssuers = false;
		host.Credentials.IssuedTokenAuthentication.CertificateValidationMode = X509CertificateValidationMode.None;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "ADWSHostFactory", "ProvisionCertificate: loaded certificate");
		return true;
	}
}

using System.Net.Security;
using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[ServiceContract(SessionMode = SessionMode.Required, ProtectionLevel = ProtectionLevel.EncryptAndSign, Name = "TopologyManagement", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public interface IActiveDirectoryTopologyManagementV1 : IActiveDirectoryTopologyManagement
{
	[OperationContract]
	[FaultContract(typeof(GetVersionFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "GetVersionFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	GetVersionResponse GetVersion(GetVersionRequest input);
}

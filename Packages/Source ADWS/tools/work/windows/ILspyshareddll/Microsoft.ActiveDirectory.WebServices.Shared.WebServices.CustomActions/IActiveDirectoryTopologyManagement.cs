using System.Net.Security;
using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[ServiceContract(SessionMode = SessionMode.Required, ProtectionLevel = ProtectionLevel.EncryptAndSign, Name = "TopologyManagement", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public interface IActiveDirectoryTopologyManagement
{
	[OperationContract]
	[FaultContract(typeof(GetADDomainControllerFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "GetADDomainControllerFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	GetADDomainControllerResponse GetADDomainController(GetADDomainControllerRequest input);

	[OperationContract]
	[FaultContract(typeof(GetADDomainFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "GetADDomainFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	GetADDomainResponse GetADDomain(GetADDomainRequest input);

	[OperationContract]
	[FaultContract(typeof(MoveADOperationMasterRoleFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "MoveADOperationMasterRoleFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	MoveADOperationMasterRoleResponse MoveADOperationMasterRole(MoveADOperationMasterRoleRequest input);

	[OperationContract]
	[FaultContract(typeof(GetADForestFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "GetADForestFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	GetADForestResponse GetADForest(GetADForestRequest input);

	[OperationContract]
	[FaultContract(typeof(ChangeOptionalFeatureFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "ChangeOptionalFeatureFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	ChangeOptionalFeatureResponse ChangeOptionalFeature(ChangeOptionalFeatureRequest input);
}

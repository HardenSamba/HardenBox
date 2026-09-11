using System.Net.Security;
using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[ServiceContract(SessionMode = SessionMode.Required, ProtectionLevel = ProtectionLevel.EncryptAndSign, Name = "AccountManagement", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
public interface IActiveDirectoryAccountManagement
{
	[OperationContract]
	[FaultContract(typeof(GetADGroupMemberFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "GetADGroupMemberFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	GetADGroupMemberResponse GetADGroupMember(GetADGroupMemberRequest input);

	[OperationContract]
	[FaultContract(typeof(GetADPrincipalGroupMembershipFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "GetADPrincipalGroupMembershipFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	GetADPrincipalGroupMembershipResponse GetADPrincipalGroupMembership(GetADPrincipalGroupMembershipRequest input);

	[OperationContract]
	[FaultContract(typeof(SetPasswordFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "SetPasswordFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	SetPasswordResponse SetPassword(SetPasswordRequest input);

	[OperationContract]
	[FaultContract(typeof(ChangePasswordFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "ChangePasswordFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	ChangePasswordResponse ChangePassword(ChangePasswordRequest input);

	[OperationContract]
	[FaultContract(typeof(GetADPrincipalAuthorizationGroupFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "GetADPrincipalAuthorizationGroupFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	GetADPrincipalAuthorizationGroupResponse GetADPrincipalAuthorizationGroup(GetADPrincipalAuthorizationGroupRequest input);

	[OperationContract]
	[FaultContract(typeof(TranslateNameFault), Action = "http://schemas.microsoft.com/2008/1/ActiveDirectory/Data/fault", Name = "TranslateNameFault", Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
	TranslateNameResponse TranslateName(TranslateNameRequest input);
}

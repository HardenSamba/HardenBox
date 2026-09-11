using System.DirectoryServices.AccountManagement;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DirectoryRdnPrefix("CN")]
[DirectoryObjectClass("user")]
internal class CustomUserPrincipal : UserPrincipal
{
	public CustomUserPrincipal(PrincipalContext context)
		: base(context)
	{
	}

	public CustomUserPrincipal(PrincipalContext context, string samAccountName, string password, bool enabled)
		: base(context, samAccountName, password, enabled)
	{
	}

	public new static CustomUserPrincipal FindByIdentity(PrincipalContext context, string identityValue)
	{
		return (CustomUserPrincipal)Principal.FindByIdentityWithType(context, typeof(CustomUserPrincipal), identityValue);
	}

	public new static CustomUserPrincipal FindByIdentity(PrincipalContext context, IdentityType identityType, string identityValue)
	{
		return (CustomUserPrincipal)Principal.FindByIdentityWithType(context, typeof(CustomUserPrincipal), identityType, identityValue);
	}
}

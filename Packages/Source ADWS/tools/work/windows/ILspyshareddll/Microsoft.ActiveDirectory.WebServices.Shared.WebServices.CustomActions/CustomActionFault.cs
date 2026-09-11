using System.Runtime.Serialization;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[DataContract(Namespace = "http://schemas.microsoft.com/2008/1/ActiveDirectory/CustomActions")]
[KnownType(typeof(GetADGroupMemberFault))]
[KnownType(typeof(GetADPrincipalGroupMembershipFault))]
[KnownType(typeof(SetPasswordFault))]
[KnownType(typeof(ChangePasswordFault))]
[KnownType(typeof(GetADPrincipalAuthorizationGroupFault))]
[KnownType(typeof(TranslateNameFault))]
[KnownType(typeof(GetADDomainControllerFault))]
[KnownType(typeof(GetADDomainFault))]
[KnownType(typeof(MoveADOperationMasterRoleFault))]
[KnownType(typeof(GetADForestFault))]
[KnownType(typeof(ChangeOptionalFeatureFault))]
[KnownType(typeof(ArgumentErrorDetailCA))]
[KnownType(typeof(DirectoryErrorDetailCA))]
public abstract class CustomActionFault
{
	private ArgumentErrorDetailCA argErrEx;

	private string strEx;

	private string shortStrEx;

	private DirectoryErrorDetailCA dirErrEx;

	[DataMember(IsRequired = false)]
	public ArgumentErrorDetailCA ArgumentError
	{
		get
		{
			return argErrEx;
		}
		set
		{
			argErrEx = value;
		}
	}

	[DataMember(IsRequired = false)]
	public string Error
	{
		get
		{
			return strEx;
		}
		set
		{
			strEx = value;
		}
	}

	[DataMember(IsRequired = false)]
	public DirectoryErrorDetailCA DirectoryError
	{
		get
		{
			return dirErrEx;
		}
		set
		{
			dirErrEx = value;
		}
	}

	[DataMember(IsRequired = false)]
	public string ShortError
	{
		get
		{
			return shortStrEx;
		}
		set
		{
			shortStrEx = value;
		}
	}

	[IgnoreDataMember]
	public abstract string FaultName { get; }

	public CustomActionFault()
	{
		argErrEx = null;
		strEx = null;
		shortStrEx = null;
		dirErrEx = null;
	}

	public CustomActionFault(ArgumentErrorDetailCA Ae, string Se, DirectoryErrorDetailCA De)
	{
		argErrEx = Ae;
		strEx = Se;
		if (WebServiceStrings.LongToShortMap.TryGetValue(Se, out var value))
		{
			shortStrEx = value;
		}
		else
		{
			shortStrEx = WebServiceConstants.ArgumentException;
		}
		dirErrEx = De;
	}
}

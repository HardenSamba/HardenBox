using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class TranslateNameResponse
{
	[MessageBodyMember]
	public ActiveDirectoryNameTranslateResult[] NameTranslateResult;
}

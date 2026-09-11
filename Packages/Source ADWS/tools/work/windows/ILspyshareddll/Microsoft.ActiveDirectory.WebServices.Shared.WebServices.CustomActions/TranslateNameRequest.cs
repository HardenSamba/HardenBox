using System.ServiceModel;

namespace Microsoft.ActiveDirectory.WebServices.Shared.WebServices.CustomActions;

[MessageContract]
public class TranslateNameRequest
{
	[MessageHeader(MustUnderstand = true)]
	public string Server;

	[MessageBodyMember]
	public string[] Names;

	[MessageBodyMember]
	public ActiveDirectoryNameFormat FormatOffered;

	[MessageBodyMember]
	public ActiveDirectoryNameFormat FormatDesired;
}

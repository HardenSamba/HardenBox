using System.Collections.Generic;

namespace Microsoft.ActiveDirectory.WebServices;

internal interface IDirectoryObject
{
	DirectoryObjectIdentifier ObjectIdentity { get; set; }

	DirectoryObjectIdentifier ParentIdentity { get; set; }

	string DistinguishedName { get; set; }

	string RDN { get; set; }

	string ObjectClass { get; set; }

	IList<DirectoryAttribute> Attributes { get; }
}

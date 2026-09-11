using System;

namespace Microsoft.ActiveDirectory.WebServices;

public class DirectoryObjectSerializer : ObjectSerializer
{
	public DirectoryObjectSerializer(DirectoryObject dirObj)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryObjectSerializer", "DirectoryObjectSerializer constructor Entered.");
		ValidateDirectoryObject(dirObj);
	}

	private void ValidateDirectoryObject(DirectoryObject dirObj)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryObjectSerializer", "ValidateDirectoryObject Entered.");
		if (dirObj == null)
		{
			throw new ArgumentNullException("dirObj");
		}
		if (dirObj.ObjectIdentity == null)
		{
			throw new ArgumentException(AdwsStrings.NullObjectIdentity);
		}
		if (dirObj.RDN == null)
		{
			throw new ArgumentException(AdwsStrings.NullObjectRDN);
		}
		if (dirObj.ObjectClass == null)
		{
			throw new ArgumentException(AdwsStrings.NullObjectClass);
		}
		if (dirObj.DistinguishedName == null)
		{
			throw new ArgumentException(AdwsStrings.NullObjectDistinguishedName);
		}
	}
}

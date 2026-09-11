using System;

namespace Microsoft.ActiveDirectory.WebServices;

public class DirectoryObjectFragmentSerializer : ObjectSerializer
{
	public DirectoryObjectFragmentSerializer(DirectoryObjectFragment dirObjFrag)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryObjectFragmentSerializer", "DirectoryObjectFragmentSerializer constructor Entered.");
		ValidateDirectoryObjectFragment(dirObjFrag);
	}

	private void ValidateDirectoryObjectFragment(DirectoryObjectFragment dirObjFrag)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "DirectoryObjectFragmentSerializer", "ValidateDirectoryObjectFragment Entered.");
		if (dirObjFrag == null)
		{
			throw new ArgumentNullException("dirObjFrag");
		}
		if (dirObjFrag.ObjectClass == null)
		{
			throw new ArgumentException(AdwsStrings.NullObjectClass);
		}
	}
}

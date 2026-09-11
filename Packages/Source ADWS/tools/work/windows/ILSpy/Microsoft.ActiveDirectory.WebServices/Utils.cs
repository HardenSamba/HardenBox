using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.ActiveDirectory.WebServices;

internal class Utils
{
	internal class CaseInsensitiveStringComparer : EqualityComparer<string>
	{
		public override bool Equals(string s1, string s2)
		{
			return string.Compare(s1, s2, ignoreCase: true, CultureInfo.InvariantCulture) == 0;
		}

		public override int GetHashCode(string s)
		{
			return s.GetHashCode();
		}
	}

	internal static string GetComputerDnsName()
	{
		uint lpnSize = 0u;
		NativeMethods.GetComputerNameExW(NativeMethods.COMPUTER_NAME_FORMAT.ComputerNameDnsFullyQualified, null, ref lpnSize);
		int lastWin32Error = Marshal.GetLastWin32Error();
		if (lastWin32Error != 234)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "Utils", "GetComputerDnsName: GetComputerNameExW (1st try) failed, gle={0}", lastWin32Error);
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder((int)lpnSize);
		if (!NativeMethods.GetComputerNameExW(NativeMethods.COMPUTER_NAME_FORMAT.ComputerNameDnsFullyQualified, stringBuilder, ref lpnSize))
		{
			lastWin32Error = Marshal.GetLastWin32Error();
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "Utils", "GetComputerDnsName: GetComputerNameExW (2nd try) failed, gle={0}", lastWin32Error);
			return null;
		}
		string text = stringBuilder.ToString();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "Utils", "GetComputerDnsName: computer name is {0}", text);
		return text;
	}

	internal static void MergeArrayIntoList<T>(List<T> list, T[] arrayToMerge, IEqualityComparer<T> comparer)
	{
		if (arrayToMerge == null)
		{
			return;
		}
		foreach (T val in arrayToMerge)
		{
			if (!list.Contains(val, comparer))
			{
				list.Add(val);
			}
		}
	}

	internal static void RemoveUnnecessaryPriviledges()
	{
		bool flag = false;
		string[] array = new string[7] { "SeBackupPrivilege", "SeRestorePrivilege", "SeAssignPrimaryTokenPrivilege", "SeIncreaseQuotaPrivilege", "SeDebugPrivilege", "SeTcbPrivilege", "SeShutdownPrivilege" };
		foreach (string text in array)
		{
			if (RemovePriviledgeFromProcess(text) != 0)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "Utils", "RemoveUnnecessaryPriviledges: could not remove priviledge {0}", text);
				flag = true;
				ADWSEventlog.Log(2147484655L, text);
			}
		}
		if (!flag)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "Utils", "RemoveUnnecessaryPriviledges: all present unnecessary priviledges removed successfully");
			ADWSEventlog.Log(1073742832L);
		}
	}

	internal static int RemovePriviledgeFromProcess(string priviledgeName)
	{
		IntPtr tokenHandle = IntPtr.Zero;
		int num = 0;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "Utils", "RemovePriviledgeFromProcess: trying to remove priviledge {0}", priviledgeName);
		using Process process = Process.GetCurrentProcess();
		if (!NativeMethods.OpenProcessToken(process.Handle, NativeMethods.TOKEN_RIGHTS.TOKEN_QUERY | NativeMethods.TOKEN_RIGHTS.TOKEN_ADJUST_PRIVILEGES, ref tokenHandle))
		{
			num = Marshal.GetLastWin32Error();
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "Utils", "RemovePriviledgeFromProcess: error opening process token, gle={0}", num);
			return num;
		}
		try
		{
			NativeMethods.TokenPrivileges NewState = default(NativeMethods.TokenPrivileges);
			NewState.privilegeCount = 1u;
			NewState.luid.highPart = 0u;
			NewState.luid.lowPart = 0u;
			NewState.attribute = 4u;
			if (!NativeMethods.LookupPrivilegeValue(null, priviledgeName, ref NewState.luid))
			{
				num = Marshal.GetLastWin32Error();
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "Utils", "RemovePriviledgeFromProcess: error looking up priviledge {0}, gle={1}", priviledgeName, num);
				return num;
			}
			if (!NativeMethods.AdjustTokenPrivileges(tokenHandle, DisableAllPrivileges: false, ref NewState, 0, IntPtr.Zero, IntPtr.Zero))
			{
				num = Marshal.GetLastWin32Error();
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "Utils", "RemovePriviledgeFromProcess: error adjusting priviledge {0}, gle={1}", priviledgeName, num);
				return num;
			}
			num = Marshal.GetLastWin32Error();
			if (num == 1300)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "Utils", "RemovePriviledgeFromProcess: unable to remove {0} priviledge because it was absent ", priviledgeName);
				return 0;
			}
		}
		finally
		{
			if (tokenHandle != IntPtr.Zero)
			{
				NativeMethods.CloseHandle(tokenHandle);
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "Utils", "RemovePriviledgeFromProcess: removed priviledge {0}", priviledgeName);
		return num;
	}

	internal static T[] ToArray<T>(IList<T> iList)
	{
		if (iList == null || iList.Count == 0)
		{
			return null;
		}
		T[] array = new T[iList.Count];
		for (int i = 0; i < iList.Count; i++)
		{
			array[i] = iList[i];
		}
		return array;
	}
}

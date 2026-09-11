using System;
using System.Collections.Generic;

namespace Microsoft.ActiveDirectory.WebServices;

internal abstract class Query : IDisposable
{
	private List<DirectoryAttribute> allRequestedAttributesToRetrieve;

	private DirectoryAttribute sortKey;

	private bool ascendingSort;

	private InstanceType instance;

	internal bool disposed;

	internal List<DirectoryAttribute> AllRequestedAttributesToRetrieve
	{
		get
		{
			CheckDisposed();
			return allRequestedAttributesToRetrieve;
		}
	}

	internal DirectoryAttribute SortKey
	{
		get
		{
			CheckDisposed();
			return sortKey;
		}
		set
		{
			CheckDisposed();
			sortKey = value;
		}
	}

	internal bool AscendingSort
	{
		get
		{
			CheckDisposed();
			return ascendingSort;
		}
		set
		{
			CheckDisposed();
			ascendingSort = value;
		}
	}

	internal InstanceType Instance
	{
		get
		{
			CheckDisposed();
			return instance;
		}
		set
		{
			CheckDisposed();
			instance = value;
		}
	}

	public abstract QueryFilter QueryFilter { get; set; }

	internal Query()
	{
		allRequestedAttributesToRetrieve = new List<DirectoryAttribute>();
		ascendingSort = true;
		sortKey = null;
	}

	public abstract bool NoMoreResults();

	internal bool ReturnParentInformation()
	{
		CheckDisposed();
		if (AllRequestedAttributesToRetrieve.Count == 0)
		{
			return true;
		}
		if (DirectoryUtilities.FindDirAttribute(AllRequestedAttributesToRetrieve, AdwsConstants.containerHierarchyParent, AdwsConstants.AdNs, RemoveIfFound: false, OneValueEnforced: false) != null)
		{
			return true;
		}
		return false;
	}

	internal string[] GetServerAttributesToRetrieve()
	{
		CheckDisposed();
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "Query", "GetServerAttributes entered");
		if (AllRequestedAttributesToRetrieve.Count == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "Query", "GetServerAttributes returning String[]{\"*\"}");
			return new string[1] { "*" };
		}
		int num = 0;
		foreach (DirectoryAttribute item in AllRequestedAttributesToRetrieve)
		{
			if (item.Xmlns.Equals(AdwsConstants.AddataNs, StringComparison.InvariantCultureIgnoreCase))
			{
				num++;
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "Query", "GetServerAttributes: found {0} regular attrs.", num);
		if (num == 0)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "Query", "GetServerAttributes: no regular attrs found --returning String[]{}");
			return new string[0];
		}
		string[] array = new string[num];
		int num2 = 0;
		foreach (DirectoryAttribute item2 in AllRequestedAttributesToRetrieve)
		{
			if (item2.Xmlns.Equals(AdwsConstants.AddataNs, StringComparison.InvariantCultureIgnoreCase))
			{
				array[num2++] = item2.Name;
			}
		}
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "Query", "GetServerAttributes: returning {0} Strings", num2);
		return array;
	}

	public void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "Query", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	public virtual void Dispose()
	{
		lock (this)
		{
			if (!disposed)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "Query", "Dispose: disposing...");
				disposed = true;
			}
		}
	}
}

using System.Collections.Generic;

namespace Microsoft.ActiveDirectory.WebServices;

internal class QuotaTracker<K>
{
	private Dictionary<K, uint> quota;

	internal QuotaTracker()
	{
		quota = new Dictionary<K, uint>();
	}

	internal bool TryToIncrementCount(K key, uint maxResourceUsagePerKey)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "QuotaTracker", "TryToIncrementCount: entering, key={0}", key.ToString());
		lock (this)
		{
			if (!quota.ContainsKey(key))
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "QuotaTracker", "TryToIncrementCount: creating new record for key={0}, count = 1", key.ToString());
				quota[key] = 1u;
			}
			else
			{
				uint num = quota[key];
				if (maxResourceUsagePerKey == 0)
				{
					maxResourceUsagePerKey = 1u;
				}
				if (num >= maxResourceUsagePerKey)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Warn, "QuotaTracker", "TryToIncrementCount: key {0} at limit (max={1})", key.ToString(), maxResourceUsagePerKey);
					return false;
				}
				quota[key] = num + 1;
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "QuotaTracker", "TryToIncrementCount: updating record for key={0}, new count = {1}", key.ToString(), num + 1);
			}
			return true;
		}
	}

	internal void DecrementCount(K key)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "QuotaTracker", "DecrementCount: entering, key={0}", key.ToString());
		lock (this)
		{
			if (quota.ContainsKey(key))
			{
				uint num = quota[key];
				if (num == 1)
				{
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "QuotaTracker", "DecrementCount: removing record for key={0}", key.ToString());
					quota.Remove(key);
				}
				else
				{
					quota[key] = num - 1;
					GlobalDebug.WriteLineIf(GlobalDebug.Info, "QuotaTracker", "DecrementCount: updating record for key={0}, new count = {1}", key.ToString(), num - 1);
				}
			}
			else
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Error, "QuotaTracker", "DecrementCount: unknown key {0}", key.ToString());
			}
		}
	}

	internal void Clear()
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "QuotaTracker", "Clear: clearing all usage");
		quota.Clear();
	}
}

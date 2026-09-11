using System;

namespace Microsoft.ActiveDirectory.WebServices;

internal interface ICustomActionConnCacheEntry<K, C> : IDisposable
{
	K ConnectionKey { get; }

	C ConnectionObject { get; }

	bool IsFakeEntry { get; }
}

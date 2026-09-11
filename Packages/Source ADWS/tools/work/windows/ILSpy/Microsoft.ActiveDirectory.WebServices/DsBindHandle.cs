using System;

namespace Microsoft.ActiveDirectory.WebServices;

internal class DsBindHandle : IDisposable
{
	private bool disposed;

	private IntPtr _hDS = IntPtr.Zero;

	private IntPtr _hAuthIdentity = IntPtr.Zero;

	internal IntPtr hDS
	{
		get
		{
			CheckDisposed();
			return _hDS;
		}
		set
		{
			CheckDisposed();
			_hDS = value;
		}
	}

	internal IntPtr hAuthIdentity
	{
		get
		{
			CheckDisposed();
			return _hAuthIdentity;
		}
		set
		{
			CheckDisposed();
			_hAuthIdentity = value;
		}
	}

	internal DsBindHandle()
	{
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	~DsBindHandle()
	{
		Dispose(disposing: false);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "DsBindHandle", "Dispose: disposing...");
			DirectoryUtilities.FreeDsRpcBinding(this);
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Warn, "DsBindHandle", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}
}

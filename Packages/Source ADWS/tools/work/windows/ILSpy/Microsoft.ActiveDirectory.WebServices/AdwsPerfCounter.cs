using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.ActiveDirectory.WebServices;

internal class AdwsPerfCounter : IDisposable
{
	private bool disposed;

	private string categoryName;

	private string counterName;

	private string helpText;

	private PerformanceCounterType counterType;

	private PerformanceCounter perfCtr;

	internal string CategoryName
	{
		get
		{
			CheckDisposed();
			return categoryName;
		}
	}

	internal string CounterName
	{
		get
		{
			CheckDisposed();
			return counterName;
		}
	}

	internal string HelpText
	{
		get
		{
			CheckDisposed();
			return helpText;
		}
	}

	internal PerformanceCounterType CounterType
	{
		get
		{
			CheckDisposed();
			return counterType;
		}
	}

	internal AdwsPerfCounter(string counterName, string helpText, PerformanceCounterType counterType)
	{
		categoryName = PerfCounterNames.Category;
		this.counterName = counterName;
		this.helpText = helpText;
		this.counterType = counterType;
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "AdwsPerfCounter", "AdwsPerfCounter: constructed '{0}' performance counter", this.counterName);
	}

	internal void Init()
	{
		CheckDisposed();
		try
		{
			perfCtr = new PerformanceCounter(categoryName, counterName, readOnly: false);
		}
		catch (Win32Exception ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Init: caught Win32Exception, counter={0}, category={1}, err={2}, exception={3}", counterName, categoryName, ex.NativeErrorCode.ToString(), ex.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotConstructPerformanceCounter, ex);
		}
		catch (ArgumentNullException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Init: caught ArgumentNullException, counter={0}, category={1}, exception={2}", (counterName != null) ? counterName : "NULL", (categoryName != null) ? categoryName : "NULL", ex2.ToString());
			throw new InvalidOperationException(AdwsStrings.CouldNotConstructPerformanceCounter, ex2);
		}
	}

	public void Dispose()
	{
		if (!disposed)
		{
			if (perfCtr != null)
			{
				GlobalDebug.WriteLineIf(GlobalDebug.Info, "AdwsPerfCounter", "Dispose: disposing '{0}' performance counter", counterName);
				perfCtr.Dispose();
				perfCtr = null;
			}
			disposed = true;
		}
	}

	private void CheckDisposed()
	{
		if (disposed)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "CheckDisposed: accessing disposed object");
			throw new ObjectDisposedException(GetType().ToString());
		}
	}

	internal void Set(int counterValue, bool throwException)
	{
		CheckDisposed();
		if (perfCtr == null)
		{
			return;
		}
		try
		{
			perfCtr.RawValue = counterValue;
		}
		catch (Win32Exception ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Set: caught Win32Exception, counter={0}, category={1}, err={2}, exception={3}", counterName, categoryName, ex.NativeErrorCode.ToString(), ex.ToString());
			if (throwException)
			{
				throw new InvalidOperationException(AdwsStrings.CouldNotAdjustPerformanceCounterValue, ex);
			}
		}
		catch (InvalidOperationException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Set: caught InvalidOperationException, counter={0}, category={1}, exception={2}", counterName, categoryName, ex2.ToString());
			if (throwException)
			{
				throw;
			}
		}
	}

	internal void Set(int counterValue)
	{
		Set(counterValue, throwException: false);
	}

	internal void Increment(bool throwException)
	{
		CheckDisposed();
		if (perfCtr == null)
		{
			return;
		}
		try
		{
			perfCtr.Increment();
		}
		catch (Win32Exception ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Increment: caught Win32Exception, counter={0}, category={1}, err={2}, exception={3}", counterName, categoryName, ex.NativeErrorCode.ToString(), ex.ToString());
			if (throwException)
			{
				throw new InvalidOperationException(AdwsStrings.CouldNotAdjustPerformanceCounterValue, ex);
			}
		}
		catch (InvalidOperationException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Increment: caught InvalidOperationException, counter={0}, category={1}, exception={2}", counterName, categoryName, ex2.ToString());
			if (throwException)
			{
				throw;
			}
		}
	}

	internal void Increment()
	{
		Increment(throwException: false);
	}

	internal void Decrement(bool throwException)
	{
		CheckDisposed();
		if (perfCtr == null)
		{
			return;
		}
		try
		{
			perfCtr.Decrement();
		}
		catch (Win32Exception ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Decrement: caught Win32Exception, counter={0}, category={1}, err={2}, exception={3}", counterName, categoryName, ex.NativeErrorCode.ToString(), ex.ToString());
			if (throwException)
			{
				throw new InvalidOperationException(AdwsStrings.CouldNotAdjustPerformanceCounterValue, ex);
			}
		}
		catch (InvalidOperationException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Decrement: caught InvalidOperationException, counter={0}, category={1}, exception={2}", counterName, categoryName, ex2.ToString());
			if (throwException)
			{
				throw;
			}
		}
	}

	internal void Decrement()
	{
		Decrement(throwException: false);
	}

	internal void Adjust(long adj, bool throwException)
	{
		CheckDisposed();
		if (perfCtr == null)
		{
			return;
		}
		try
		{
			perfCtr.IncrementBy(adj);
		}
		catch (Win32Exception ex)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Adjust: caught Win32Exception, counter={0}, category={1}, err={2}, exception={3}", counterName, categoryName, ex.NativeErrorCode.ToString(), ex.ToString());
			if (throwException)
			{
				throw new InvalidOperationException(AdwsStrings.CouldNotAdjustPerformanceCounterValue, ex);
			}
		}
		catch (InvalidOperationException ex2)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "AdwsPerfCounter", "Adjust: caught InvalidOperationException, counter={0}, category={1}, exception={2}", counterName, categoryName, ex2.ToString());
			if (throwException)
			{
				throw;
			}
		}
	}

	internal void Adjust(long adj)
	{
		Adjust(adj, throwException: false);
	}
}

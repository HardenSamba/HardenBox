using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Microsoft.ActiveDirectory.WebServices;

internal static class ADWSEventlog
{
	private static EventLog eventLog;

	internal static bool Start()
	{
		if (EventLog.SourceExists("ADWS"))
		{
			string text = EventLog.LogNameFromSourceName("ADWS", ".");
			if (text == null || text.Length == 0)
			{
				return false;
			}
			try
			{
				eventLog = new EventLog(text, ".", "ADWS");
			}
			catch (ArgumentException)
			{
				return false;
			}
			if (eventLog == null)
			{
				return false;
			}
			return true;
		}
		return false;
	}

	internal static void Log(long eventId, params object[] v)
	{
		if (eventLog == null)
		{
			return;
		}
		if ((eventId & 0xFFFF) <= 999 || (eventId & 0xFFFF) >= 1700)
		{
			throw new ArgumentException("eventId");
		}
		string[] array = new string[(v != null) ? v.Length : 0];
		for (int i = 0; i < v.Length; i++)
		{
			array[i] = ((v[i] == null) ? "-" : v[i].ToString());
		}
		EventLogEntryType entryType = EventLogEntryType.SuccessAudit;
		switch (eventId & 0xC0000000u)
		{
		case 0L:
			entryType = EventLogEntryType.SuccessAudit;
			break;
		case 1073741824L:
			entryType = EventLogEntryType.Information;
			break;
		case 2147483648L:
			entryType = EventLogEntryType.Warning;
			break;
		case 3221225472L:
			entryType = EventLogEntryType.Error;
			break;
		}
		int categoryId = (int)(eventId & 0xFFFF) / 100 - 9;
		try
		{
			EventInstance instance = new EventInstance(eventId, categoryId, entryType);
			EventLog obj = eventLog;
			object[] values = array;
			obj.WriteEvent(instance, values);
		}
		catch (InvalidEnumArgumentException)
		{
			throw;
		}
		catch (ArgumentOutOfRangeException)
		{
			throw;
		}
		catch (ArgumentException)
		{
			throw;
		}
		catch (Win32Exception)
		{
		}
	}
}

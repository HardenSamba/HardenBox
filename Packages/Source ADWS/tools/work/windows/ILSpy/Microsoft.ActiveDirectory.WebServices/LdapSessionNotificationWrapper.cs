namespace Microsoft.ActiveDirectory.WebServices;

internal class LdapSessionNotificationWrapper
{
	private LdapSession associatedLdapSession;

	private DataAccessNotifyOfTimeout routineToNotify;

	internal LdapSession AssociatedLdapSession
	{
		get
		{
			return associatedLdapSession;
		}
		set
		{
			associatedLdapSession = value;
		}
	}

	internal LdapSessionNotificationWrapper(DataAccessNotifyOfTimeout routineToNotify)
	{
		this.routineToNotify = routineToNotify;
	}

	internal void SessionPoolTimedOutCallback(ReservationToken tokenOfExpiredReservation, bool wasIdle)
	{
		GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionNotificationWrapper", "SessionPoolTimedOutCallback: got called (wasIdle = {0})", wasIdle.ToString());
		if (routineToNotify != null && associatedLdapSession != null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Info, "LdapSessionNotificationWrapper", "SessionPoolTimedOutCallback: calling handler");
			routineToNotify(associatedLdapSession, wasIdle);
		}
		else if (routineToNotify != null)
		{
			GlobalDebug.WriteLineIf(GlobalDebug.Error, "LdapSessionNotificationWrapper", "SessionPoolTimedOutCallback: got a callback with a registered notification routine but associated session is null");
		}
	}
}

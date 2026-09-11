namespace Microsoft.ActiveDirectory.WebServices;

internal delegate void DataAccessNotifyOfTimeout(LdapSession expiredSession, bool wasIdle);

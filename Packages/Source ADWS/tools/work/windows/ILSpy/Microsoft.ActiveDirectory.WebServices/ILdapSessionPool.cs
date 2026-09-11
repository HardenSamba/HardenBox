using System;
using System.DirectoryServices.Protocols;

namespace Microsoft.ActiveDirectory.WebServices;

internal interface ILdapSessionPool : IDisposable
{
	void InitializeInstance(InstanceType instance, uint initialConnectionCount, uint maxConnectionCount);

	bool IsInstanceKnown(InstanceType instance);

	void RemoveInstance(InstanceType instance);

	LdapConnection GetConnectionAsCurrentUser(InstanceType instance, TimeSpan timeToWaitToAcquire);

	void NotifyConnectionDown(LdapConnection ldapConn);

	void NotifyConnectionUp(LdapConnection ldapConn);

	void ReleaseConnection(LdapConnection ldapConn);

	ReservationToken ReserveConnectionAsCurrentUser(InstanceType instance, SessionPoolNotifyOfTimeout routineToNotify, TimeSpan timeToWaitToAcquire);

	LdapConnection GetReservedConnection(ReservationToken token);

	void ReleaseReservedConnection(ReservationToken token);

	void ReleaseReservation(ReservationToken token);

	void NotifyReservedConnectionDown(ReservationToken token);

	void NotifyReservedConnectionUp(ReservationToken token);

	UtilityConnection GetUtilityConnection(InstanceType instance);

	void ReleaseUtilityConnection(UtilityConnection utilConnection);
}

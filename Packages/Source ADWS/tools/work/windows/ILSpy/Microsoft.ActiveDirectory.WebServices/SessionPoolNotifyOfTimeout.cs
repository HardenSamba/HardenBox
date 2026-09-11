namespace Microsoft.ActiveDirectory.WebServices;

internal delegate void SessionPoolNotifyOfTimeout(ReservationToken tokenOfExpiredReservation, bool wasIdle);

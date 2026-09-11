import logging
import threading
from typing import Any, Dict, List, Optional

from classes.directory import SambaDB
from classes.directory.reservation_token import ReservationToken

DEFAULT_INSTANCE = "primary"
DEFAULT_PER_USER_LIMIT = 10
DEFAULT_IDLE_TIMEOUT = 600


class LdapSessionPool:

    def __init__(
        self,
        logger: logging.Logger,
        per_user_limit: int = DEFAULT_PER_USER_LIMIT,
        idle_timeout: int = DEFAULT_IDLE_TIMEOUT,
    ) -> None:
        self.logger = logger
        self._lock = threading.RLock()
        self._tokens: Dict[str, ReservationToken] = {}
        self._sessions: Dict[Any, Any] = {}
        self.per_user_limit = per_user_limit
        self.idle_timeout = idle_timeout

    def acquire(
        self,
        owner_sid: str,
        session_info: Any = None,
        instance: str = DEFAULT_INSTANCE,
    ) -> ReservationToken:
        with self._lock:
            self._scavenge_unlocked()
            active: List[ReservationToken] = [
                t for t in self._tokens.values()
                if t.owner_sid == owner_sid and not t.released
            ]
            if len(active) >= self.per_user_limit:
                raise RuntimeError(
                    f"Per-user LDAP session limit reached ({self.per_user_limit})"
                )
            session = self._sessions.get((owner_sid, instance))
            if session is None:
                session = SambaDB(self.logger, session_info=session_info)
                self._sessions[(owner_sid, instance)] = session
            token = ReservationToken(owner_sid, instance, session)
            self._tokens[token.id] = token
            return token

    def release(self, token_id: str) -> bool:
        with self._lock:
            token = self._tokens.pop(token_id, None)
            if token is not None:
                token.release()
                return True
            return False

    def get(self, token_id: str) -> Optional[ReservationToken]:
        token = self._tokens.get(token_id)
        if token is None or token.released:
            return None
        token.touch()
        return token

    def _scavenge_unlocked(self) -> None:
        stale = [tid for tid, t in self._tokens.items()
                 if t.age_seconds() > self.idle_timeout]
        for tid in stale:
            token = self._tokens.pop(tid, None)
            if token is not None:
                token.release()

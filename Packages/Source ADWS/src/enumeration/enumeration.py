import logging
from typing import Optional

from classes.enumeration.enumeration_cache import EnumerationContextCache

_INSTANCE: Optional[EnumerationContextCache] = None


def get_enumeration_cache(logger: logging.Logger) -> EnumerationContextCache:
    global _INSTANCE
    if _INSTANCE is None:
        _INSTANCE = EnumerationContextCache(logger)
    return _INSTANCE

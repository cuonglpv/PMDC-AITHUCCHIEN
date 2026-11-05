import secrets

from fastapi import Header, Request

from document_ai.contracts.models import Code
from document_ai.errors import TaskFailure


def require_service_key(request: Request, x_service_key: str | None = Header(default=None)) -> None:
    """Only the Claims API holds this key. The browser never reaches this service."""
    expected: str = request.app.state.settings.service_key
    if x_service_key is None or not secrets.compare_digest(x_service_key, expected):
        raise TaskFailure(401, Code.SERVICE_KEY_INVALID, "The service key is missing or wrong")

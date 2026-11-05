from pathlib import Path
from uuid import uuid4

import pytest
from fastapi.testclient import TestClient

from document_ai.config import Settings
from document_ai.gateway.fake.gateway import FakeGateway
from document_ai.main import create_app
from document_ai.pages.reader import PageReadError

SERVICE_KEY = "test-service-key"
CONTRACT = Path(__file__).parents[3] / "docs" / "api" / "document-ai.yaml"


class MemoryReader:
    """Serves any key, except the ones marked unreadable."""

    def read(self, image_key: str) -> bytes:
        if "unreadable" in image_key:
            raise PageReadError(image_key)
        return b"png"


def make_client(gateway=None, openapi_url=None) -> TestClient:
    app = create_app(
        Settings(service_key=SERVICE_KEY),
        gateway=gateway or FakeGateway(),
        reader=MemoryReader(),
        openapi_url=openapi_url,
    )
    return TestClient(app, raise_server_exceptions=False, headers={"X-Service-Key": SERVICE_KEY})


@pytest.fixture
def client() -> TestClient:
    return make_client()


def task_body(**extra) -> dict:
    return {"runId": str(uuid4()), "model": {"name": "test-pipeline", "version": "v1"}, **extra}


def page(image_key: str = "pages/c/d/1.png", page_type: str | None = "INVOICE") -> dict:
    return {"pageId": str(uuid4()), "imageKey": image_key, "pageType": page_type}

"""How the service really starts, which the tests that build an app directly do not cover."""

import importlib

import pytest
from fastapi import FastAPI
from fastapi.testclient import TestClient


def load_asgi():
    import document_ai.asgi

    return importlib.reload(document_ai.asgi)


def test_the_server_module_has_an_app_from_the_environment(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_SERVICE_KEY", "from-the-environment")
    monkeypatch.setenv("DOCUMENT_AI_STORAGE_ACCESS_KEY", "key")
    monkeypatch.setenv("DOCUMENT_AI_STORAGE_SECRET_KEY", "secret")

    app = load_asgi().app

    assert isinstance(app, FastAPI)
    assert TestClient(app).get("/health").json() == {"status": "up"}


def test_the_service_refuses_to_start_without_a_service_key(monkeypatch):
    monkeypatch.delenv("DOCUMENT_AI_SERVICE_KEY", raising=False)
    monkeypatch.chdir("/")

    with pytest.raises(ValueError, match="service_key"):
        load_asgi()


def test_an_unknown_gateway_is_refused_at_startup(monkeypatch):
    monkeypatch.setenv("DOCUMENT_AI_SERVICE_KEY", "key")
    monkeypatch.setenv("DOCUMENT_AI_GATEWAY", "a-provider-nobody-chose")

    with pytest.raises(ValueError, match="Unknown gateway"):
        load_asgi()

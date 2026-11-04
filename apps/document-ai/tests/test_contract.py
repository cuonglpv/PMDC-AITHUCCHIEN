"""Every route against docs/api/document-ai.yaml, with generated requests and the fake gateway. No provider is called."""

import pytest
import schemathesis
import yaml
from conftest import CONTRACT, SERVICE_KEY, make_client
from hypothesis import HealthCheck, settings
from schemathesis.specs.openapi.checks import (
    content_type_conformance,
    response_schema_conformance,
    status_code_conformance,
)


def _app_serving_the_contract():
    """Schemathesis reads the hand-written contract, not the document FastAPI would generate."""
    contract = yaml.safe_load(CONTRACT.read_text(encoding="utf-8"))
    app = make_client(openapi_url="/openapi.json").app
    app.openapi = lambda: contract
    return app


schema = schemathesis.openapi.from_asgi("/openapi.json", _app_serving_the_contract())


@schema.parametrize()
@settings(max_examples=15, deadline=None, suppress_health_check=list(HealthCheck))
def test_every_response_matches_the_contract(case):
    response = case.call(headers={"X-Service-Key": SERVICE_KEY})
    case.validate_response(
        response,
        checks=[status_code_conformance, content_type_conformance, response_schema_conformance],
    )


def test_the_contract_has_six_tasks():
    contract = yaml.safe_load(CONTRACT.read_text(encoding="utf-8"))
    assert len(contract["paths"]) == 6


@pytest.mark.parametrize("route", ["page-check", "field-extraction", "benefit-mapping"])
def test_no_service_key_gets_401_before_the_body_is_read(route):
    client = make_client()
    response = client.post(f"/v1/tasks/{route}", headers={"X-Service-Key": "wrong"}, json={})
    assert response.status_code == 401
    assert response.json()["code"] == "SERVICE_KEY_INVALID"
    assert response.headers["content-type"].startswith("application/problem+json")

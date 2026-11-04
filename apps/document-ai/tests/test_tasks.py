"""The rules in C3: it reports, it does not decide; every value has a source; bad output is a failed call."""

from uuid import uuid4

import pytest
from conftest import make_client, page, task_body

from document_ai.gateway.fake.gateway import FakeGateway
from document_ai.gateway.ports import (
    BadModelOutput,
    FieldAnswer,
    MappingAnswer,
    ModelError,
    ModelTimeout,
    PageAnswer,
    SentenceAnswer,
    SignalAnswer,
)

BOX = {"x": 0.1, "y": 0.2, "w": 0.3, "h": 0.04}


class Scripted(FakeGateway):
    """A gateway whose answers each test chooses."""

    def __init__(self, **answers):
        self.answers = answers

    def _give(self, name, fallback):
        answer = self.answers.get(name, fallback)
        if isinstance(answer, Exception):
            raise answer
        return answer

    def check_page(self, model, image):
        return self._give("page", PageAnswer("INVOICE", 0.98, 0.94))

    def read_fields(self, model, page_type, image, catalogue):
        return self._give("fields", [])

    def map_lines(self, model, lines, categories):
        return self._give("mappings", super().map_lines(model, lines, categories))

    def score_lines(self, model, diagnosis, lines):
        return self._give("signals", super().score_lines(model, diagnosis, lines))

    def draft_summary(self, model, facts):
        return self._give("sentences", super().draft_summary(model, facts))


def post_page_check(client, *pages):
    return client.post("/v1/tasks/page-check", json=task_body(pages=list(pages)))


def post_extraction(client, *pages, catalogue=("invoice_number", "line_amount")):
    body = task_body(
        documents=[{"documentId": str(uuid4()), "pages": list(pages)}],
        fieldCatalogue=list(catalogue),
    )
    return client.post("/v1/tasks/field-extraction", json=body)


def test_page_check_reports_the_raw_score_even_when_it_is_low():
    client = make_client(Scripted(page=PageAnswer("RECEIPT", 0.41, 0.2)))
    result = post_page_check(client, page()).json()["pages"][0]
    assert result == {
        "pageId": result["pageId"],
        "pageType": "RECEIPT",
        "typeConfidence": 0.41,
        "qualityScore": 0.2,
    }


def test_the_model_used_comes_back_so_it_can_be_stored_on_the_run(client):
    body = post_page_check(client, page()).json()
    assert body["model"] == {"name": "test-pipeline", "version": "v1"}


def test_one_unreadable_page_does_not_fail_the_run(client):
    body = post_page_check(client, page(), page("pages/unreadable.png")).json()
    assert len(body["pages"]) == 1
    assert body["failedPages"][0]["code"] == "PAGE_READ_FAILED"


def test_no_readable_page_fails_the_task(client):
    response = post_page_check(client, page("pages/unreadable.png"))
    assert response.status_code == 502
    assert response.json()["code"] == "PAGE_READ_FAILED"


def test_a_timeout_fails_the_whole_run():
    response = post_page_check(make_client(Scripted(page=ModelTimeout())), page(), page())
    assert response.status_code == 504
    assert response.json()["code"] == "MODEL_TIMEOUT"


def test_a_provider_error_on_every_page_fails_the_task():
    response = post_page_check(make_client(Scripted(page=ModelError())), page())
    assert response.status_code == 502
    assert response.json()["code"] == "MODEL_ERROR"


def test_an_unknown_page_type_is_bad_output():
    response = post_page_check(
        make_client(Scripted(page=PageAnswer("NOT_A_TYPE", 0.9, 0.9))), page()
    )
    assert response.status_code == 502
    assert response.json()["code"] == "BAD_MODEL_OUTPUT"


def test_a_field_it_cannot_find_comes_back_empty_and_is_never_guessed(client):
    fields = post_extraction(client, page(), catalogue=("diagnosis",)).json()["documents"][0][
        "fields"
    ]
    assert fields == [
        {
            "fieldKey": "diagnosis",
            "itemNo": 0,
            "value": None,
            "confidence": 0.0,
            "pageId": None,
            "bbox": None,
        }
    ]


def test_repeating_fields_that_are_not_found_mean_no_item(client):
    fields = post_extraction(client, page(), catalogue=("line_amount",)).json()["documents"][0][
        "fields"
    ]
    assert fields == []


def test_each_value_carries_its_page_and_region():
    gateway = Scripted(fields=[FieldAnswer("invoice_number", 0, "HN-0045821", 0.88, BOX)])
    the_page = page()
    field = post_extraction(make_client(gateway), the_page, catalogue=("invoice_number",)).json()[
        "documents"
    ][0]["fields"][0]
    assert field["value"] == "HN-0045821"
    assert field["pageId"] == the_page["pageId"]
    assert field["bbox"] == BOX


def test_a_value_with_no_region_is_bad_output():
    gateway = Scripted(fields=[FieldAnswer("invoice_number", 0, "HN-1", 0.99, None)])
    response = post_extraction(make_client(gateway), page(), catalogue=("invoice_number",))
    assert response.status_code == 502
    assert response.json()["code"] == "BAD_MODEL_OUTPUT"


def test_a_low_confidence_value_is_still_reported_because_the_api_applies_thresholds():
    gateway = Scripted(fields=[FieldAnswer("invoice_number", 0, "HN-1", 0.31, BOX)])
    field = post_extraction(make_client(gateway), page(), catalogue=("invoice_number",)).json()[
        "documents"
    ][0]["fields"][0]
    assert field["value"] == "HN-1"
    assert field["confidence"] == 0.31


def test_the_most_confident_reading_wins_across_pages():
    class TwoReadings(FakeGateway):
        seen = 0

        def read_fields(self, model, page_type, image, catalogue):
            TwoReadings.seen += 1
            confidence = 0.6 if TwoReadings.seen == 1 else 0.9
            return [FieldAnswer("invoice_number", 0, f"read-{TwoReadings.seen}", confidence, BOX)]

    field = post_extraction(
        make_client(TwoReadings()), page(), page(), catalogue=("invoice_number",)
    ).json()["documents"][0]["fields"][0]
    assert field["value"] == "read-2"


def test_page_reread_returns_that_page_only():
    gateway = Scripted(fields=[FieldAnswer("invoice_number", 0, "HN-2", 0.97, BOX)])
    body = task_body(documentId=str(uuid4()), page=page(), fieldCatalogue=["invoice_number"])
    response = make_client(gateway).post("/v1/tasks/page-reread", json=body)
    assert response.status_code == 200
    assert response.json()["fields"][0]["value"] == "HN-2"


def test_page_reread_of_an_unreadable_page_fails(client):
    body = task_body(
        documentId=str(uuid4()),
        page=page("pages/unreadable.png"),
        fieldCatalogue=["invoice_number"],
    )
    response = client.post("/v1/tasks/page-reread", json=body)
    assert response.status_code == 502
    assert response.json()["code"] == "PAGE_READ_FAILED"


def mapping_body(line_id):
    return task_body(
        lines=[{"lineId": line_id, "description": "SA bụng"}],
        categories=[{"code": "DIAGNOSTIC_IMAGING", "name": "Diagnostic imaging"}],
    )


def test_mapping_picks_from_the_list(client):
    line_id = str(uuid4())
    mapping = client.post("/v1/tasks/benefit-mapping", json=mapping_body(line_id)).json()[
        "mappings"
    ][0]
    assert mapping["lineId"] == line_id
    assert mapping["categoryCode"] == "DIAGNOSTIC_IMAGING"


def test_a_category_outside_the_list_is_bad_output():
    line_id = str(uuid4())
    gateway = Scripted(mappings=[MappingAnswer(line_id, "INVENTED", 0.99)])
    response = make_client(gateway).post("/v1/tasks/benefit-mapping", json=mapping_body(line_id))
    assert response.status_code == 502
    assert response.json()["code"] == "BAD_MODEL_OUTPUT"


def test_a_missing_line_in_the_mapping_is_bad_output():
    gateway = Scripted(mappings=[])
    response = make_client(gateway).post(
        "/v1/tasks/benefit-mapping", json=mapping_body(str(uuid4()))
    )
    assert response.status_code == 502


def clinical_body(line_id):
    return task_body(
        diagnosis="J02.9 acute pharyngitis",
        lines=[{"lineId": line_id, "description": "Siêu âm ổ bụng", "categoryCode": "IMAGING"}],
    )


def test_clinical_check_reports_a_score_and_never_a_severity():
    line_id = str(uuid4())
    gateway = Scripted(
        signals=[SignalAnswer(line_id, "SERVICE_DIAGNOSIS", 0.32, "Not usually related.")]
    )
    signal = (
        make_client(gateway)
        .post("/v1/tasks/clinical-check", json=clinical_body(line_id))
        .json()["signals"][0]
    )
    assert signal["relevanceScore"] == 0.32
    assert "severity" not in signal


@pytest.mark.parametrize("reason", ["This looks like fraud.", "Có dấu hiệu GIAN LẬN."])
def test_a_reason_that_calls_something_fraud_is_bad_output(reason):
    line_id = str(uuid4())
    gateway = Scripted(signals=[SignalAnswer(line_id, "SERVICE_DIAGNOSIS", 0.1, reason)])
    response = make_client(gateway).post("/v1/tasks/clinical-check", json=clinical_body(line_id))
    assert response.status_code == 502
    assert response.json()["code"] == "BAD_MODEL_OUTPUT"


def summary_body():
    return task_body(
        facts=[{"id": "rule-EX-04", "kind": "RULE", "text": "EX-04: cosmetics are not covered"}]
    )


def test_summary_cites_the_facts_it_was_given(client):
    sentence = client.post("/v1/tasks/summary-draft", json=summary_body()).json()["sentences"][0]
    assert sentence["citations"] == ["rule-EX-04"]


def test_a_sentence_without_a_citation_is_allowed_so_the_reviewer_can_see_it():
    gateway = Scripted(sentences=[SentenceAnswer("No source for this.", [])])
    response = make_client(gateway).post("/v1/tasks/summary-draft", json=summary_body())
    assert response.status_code == 200


def test_a_citation_to_a_fact_it_was_not_given_is_bad_output():
    gateway = Scripted(sentences=[SentenceAnswer("Made up.", ["fact-999"])])
    response = make_client(gateway).post("/v1/tasks/summary-draft", json=summary_body())
    assert response.status_code == 502
    assert response.json()["code"] == "BAD_MODEL_OUTPUT"


def test_an_invalid_request_is_a_bug_in_the_caller(client):
    response = client.post("/v1/tasks/page-check", json={"pages": []})
    assert response.status_code == 422
    assert response.json()["code"] == "INVALID_REQUEST"


def test_an_unexpected_failure_says_service_error():
    class Broken(FakeGateway):
        def check_page(self, model, image):
            raise RuntimeError("a bug")

    response = post_page_check(make_client(Broken()), page())
    assert response.status_code == 500
    assert response.json()["code"] == "SERVICE_ERROR"


def test_bad_output_on_one_page_only_fails_that_page():
    class OneBad(FakeGateway):
        seen = 0

        def check_page(self, model, image):
            OneBad.seen += 1
            if OneBad.seen == 1:
                raise BadModelOutput("nonsense")
            return PageAnswer("INVOICE", 0.9, 0.9)

    body = post_page_check(make_client(OneBad()), page(), page()).json()
    assert len(body["pages"]) == 1
    assert body["failedPages"][0]["code"] == "BAD_MODEL_OUTPUT"

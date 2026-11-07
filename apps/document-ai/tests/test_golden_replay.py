"""
Golden replay: the fake gateway knows a recorded golden page by its image and answers as recorded. Any other image gets
the careful answer, so nothing is auto-accepted.
"""

import io
import json
import random
from itertools import combinations
from pathlib import Path
from uuid import uuid4

import pytest
from conftest import SERVICE_KEY, page, task_body
from fastapi.testclient import TestClient
from PIL import Image, ImageDraw, ImageFilter

from document_ai.config import Settings
from document_ai.contracts.models import FieldKey
from document_ai.gateway.fake.gateway import FakeGateway
from document_ai.golden.fingerprint import (
    HASH_TOLERANCE,
    SHARPNESS_TOLERANCE,
    Fingerprint,
    fingerprint,
    hamming,
)
from document_ai.golden.record import describe_pages
from document_ai.golden.replay import ReplayBook
from document_ai.main import create_app

GOLDEN = Path(__file__).parents[3] / "fixtures" / "golden"
BOX = {"x": 0.7, "y": 0.1, "w": 0.2, "h": 0.02}


def page_image(seed: int, blur: float = 0) -> bytes:
    """A page of dark blocks on white: sharp edges, and a layout of its own for each seed."""
    rng = random.Random(seed)
    image = Image.new("L", (600, 850), 255)
    draw = ImageDraw.Draw(image)
    for _ in range(60):
        x, y = rng.randrange(0, 560), rng.randrange(0, 830)
        draw.rectangle(
            (x, y, x + rng.randrange(10, 220), y + rng.randrange(4, 40)), fill=rng.randrange(0, 120)
        )
    if blur:
        image = image.filter(ImageFilter.GaussianBlur(blur))
    buffer = io.BytesIO()
    image.save(buffer, "PNG")
    return buffer.getvalue()


RECORDED = page_image(1)
UNKNOWN = page_image(2)


def recorded_page(image: bytes, **extra) -> dict:
    found = fingerprint(image)
    return {
        "document": "invoice.pdf",
        "pageNo": 1,
        "pageHash": f"{found.page_hash:016x}",
        "pageSharpness": round(found.sharpness, 2),
        "pageCheck": {"pageType": "INVOICE", "typeConfidence": 0.98, "qualityScore": 0.95},
        "fields": [
            {
                "fieldKey": "invoice_number",
                "itemNo": 0,
                "value": "HN-0045821",
                "confidence": 0.96,
                "bbox": BOX,
            },
            {
                "fieldKey": "line_amount",
                "itemNo": 1,
                "value": "300.000",
                "confidence": 0.98,
                "bbox": BOX,
            },
        ],
        **extra,
    }


def write_replay(root: Path, pages: list[dict], scenario: str = "clean") -> Path:
    folder = root / scenario / "ai-responses"
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "pages.json").write_text(json.dumps(pages), encoding="utf-8")
    return root


class ImageReader:
    """Serves real image bytes by key."""

    def __init__(self, images: dict[str, bytes]):
        self.images = images

    def read(self, image_key: str) -> bytes:
        return self.images[image_key]


def replay_client(tmp_path: Path) -> TestClient:
    settings = Settings(
        service_key=SERVICE_KEY, replay_dir=str(write_replay(tmp_path, [recorded_page(RECORDED)]))
    )
    reader = ImageReader({"pages/recorded.png": RECORDED, "pages/unknown.png": UNKNOWN})
    app = create_app(settings, reader=reader)
    return TestClient(app, raise_server_exceptions=False, headers={"X-Service-Key": SERVICE_KEY})


def extraction(client: TestClient, image_key: str, catalogue: list[str]) -> list[dict]:
    body = task_body(
        documents=[{"documentId": str(uuid4()), "pages": [page(image_key)]}],
        fieldCatalogue=catalogue,
    )
    response = client.post("/v1/tasks/field-extraction", json=body)
    assert response.status_code == 200, response.text
    return response.json()["documents"][0]["fields"]


def test_a_recorded_page_gets_its_recorded_page_check(tmp_path):
    client = replay_client(tmp_path)
    body = task_body(pages=[page("pages/recorded.png")])
    result = client.post("/v1/tasks/page-check", json=body).json()["pages"][0]
    assert (result["pageType"], result["typeConfidence"], result["qualityScore"]) == (
        "INVOICE",
        0.98,
        0.95,
    )


def test_a_recorded_page_gets_its_recorded_fields(tmp_path):
    fields = extraction(replay_client(tmp_path), "pages/recorded.png", ["invoice_number"])
    assert [(f["fieldKey"], f["value"], f["confidence"], f["bbox"]) for f in fields] == [
        ("invoice_number", "HN-0045821", 0.96, BOX)
    ]


def test_only_fields_in_the_requested_catalogue_are_replayed(tmp_path):
    fields = extraction(replay_client(tmp_path), "pages/recorded.png", ["line_amount", "diagnosis"])
    found = {(f["fieldKey"], f["itemNo"]): f["value"] for f in fields}
    assert found == {("line_amount", 1): "300.000", ("diagnosis", 0): None}


def test_an_image_that_was_not_recorded_gets_the_careful_answer(tmp_path):
    client = replay_client(tmp_path)
    result = client.post(
        "/v1/tasks/page-check", json=task_body(pages=[page("pages/unknown.png")])
    ).json()["pages"][0]
    assert (result["pageType"], result["typeConfidence"]) == ("OTHER", 0.5)
    fields = extraction(client, "pages/unknown.png", ["invoice_number"])
    assert [(f["fieldKey"], f["value"]) for f in fields] == [("invoice_number", None)]


def test_bytes_that_are_not_an_image_get_the_careful_answer(tmp_path):
    book = ReplayBook.load(write_replay(tmp_path, [recorded_page(RECORDED)]))
    gateway = FakeGateway(book)
    assert gateway.check_page(None, b"not an image") == FakeGateway().check_page(None, b"")
    assert gateway.read_fields(None, "INVOICE", b"not an image", ["invoice_number"]) == []


@pytest.mark.parametrize(("flipped", "replayed"), [(0, True), (HASH_TOLERANCE, True), (7, False)])
def test_a_hash_replays_up_to_six_bits_away(tmp_path, flipped, replayed):
    book = ReplayBook.load(write_replay(tmp_path, [recorded_page(RECORDED)]))
    recorded = fingerprint(RECORDED)
    near = Fingerprint(recorded.page_hash ^ ((1 << flipped) - 1), recorded.sharpness)
    assert hamming(near.page_hash, recorded.page_hash) == flipped
    assert (book.find(near) is not None) is replayed


def test_a_blurred_capture_of_a_recorded_page_is_not_that_page(tmp_path):
    book = ReplayBook.load(write_replay(tmp_path, [recorded_page(RECORDED)]))
    sharp, blurred = fingerprint(RECORDED), fingerprint(page_image(1, blur=3))
    # The layout is the same, so the hash alone would replay the sharp page's answer.
    assert hamming(sharp.page_hash, blurred.page_hash) <= HASH_TOLERANCE
    assert sharp.sharpness / blurred.sharpness > SHARPNESS_TOLERANCE
    assert book.find(blurred) is None


def test_the_nearest_recorded_page_wins(tmp_path):
    first = recorded_page(RECORDED)
    second = {**recorded_page(RECORDED), "document": "copy.pdf"}
    second["pageHash"] = f"{int(first['pageHash'], 16) ^ 0b11:016x}"
    book = ReplayBook.load(write_replay(tmp_path, [second, first]))
    assert book.find(fingerprint(RECORDED)).document == "invoice.pdf"


def test_the_hash_is_the_difference_hash_of_a_9_by_8_grey_image():
    bright_left = Image.linear_gradient("L").rotate(-90)  # 255 on the left, 0 on the right
    buffer = io.BytesIO()
    bright_left.save(buffer, "PNG")
    assert fingerprint(buffer.getvalue()).page_hash == 2**64 - 1
    buffer = io.BytesIO()
    Image.new("L", (300, 400), 200).save(buffer, "PNG")
    assert fingerprint(buffer.getvalue()) == Fingerprint(0, 0.0)


def test_replay_is_off_when_the_setting_is_unset(monkeypatch):
    monkeypatch.delenv("DOCUMENT_AI_REPLAY_DIR", raising=False)
    app = create_app(Settings(service_key=SERVICE_KEY))
    assert app.state.gateway.replay is None


def test_replay_reads_the_folder_the_setting_names(monkeypatch, tmp_path):
    monkeypatch.setenv(
        "DOCUMENT_AI_REPLAY_DIR", str(write_replay(tmp_path, [recorded_page(RECORDED)]))
    )
    app = create_app(Settings(service_key=SERVICE_KEY))
    assert [recorded.document for recorded in app.state.gateway.replay.pages] == ["invoice.pdf"]


def test_a_replay_folder_without_recordings_is_refused_at_startup(tmp_path):
    with pytest.raises(ValueError, match="No recorded pages"):
        create_app(Settings(service_key=SERVICE_KEY, replay_dir=str(tmp_path)))


def test_a_recording_with_a_field_outside_the_contract_is_refused(tmp_path):
    bad = recorded_page(RECORDED)
    bad["fields"][0]["fieldKey"] = "patient_mood"
    with pytest.raises(ValueError, match="patient_mood"):
        ReplayBook.load(write_replay(tmp_path, [bad]))


def test_the_recorder_prints_each_page_with_the_recorded_page_it_matches(tmp_path):
    book = ReplayBook.load(write_replay(tmp_path, [recorded_page(RECORDED)]))
    documents = [
        {"id": "d1", "originalFilename": "invoice.pdf", "pages": [{"pageNo": 1}]},
        {"id": "d2", "originalFilename": "other.pdf", "pages": [{"pageNo": 1}]},
    ]
    reader = ImageReader({"pages/c1/d1/1.png": RECORDED, "pages/c1/d2/1.png": UNKNOWN})
    rows = describe_pages("c1", documents, reader, book)
    assert rows[0]["document"] == "invoice.pdf"
    assert rows[0]["pageHash"] == recorded_page(RECORDED)["pageHash"]
    assert rows[0]["matches"] == "clean/invoice.pdf p1, 0 bits"
    assert rows[1]["matches"] is None


# ---- The recorded golden scenarios ----------------------------------------------------------------------------


GOLDEN_BOOK = ReplayBook.load(GOLDEN)


def golden(scenario: str, document: str, page_no: int = 1):
    return next(
        recorded
        for recorded in GOLDEN_BOOK.pages
        if (recorded.scenario, recorded.document, recorded.page_no) == (scenario, document, page_no)
    )


def test_no_page_image_can_match_two_recorded_golden_pages():
    """
    An image within tolerance of two recordings could replay the wrong one. Two recordings at least twice the hash
    tolerance apart, or twice the sharpness tolerance apart, can never both be within tolerance of one image.
    """
    for one, other in combinations(GOLDEN_BOOK.pages, 2):
        bits = hamming(one.fingerprint.page_hash, other.fingerprint.page_hash)
        ratio = max(one.fingerprint.sharpness, other.fingerprint.sharpness) / min(
            one.fingerprint.sharpness, other.fingerprint.sharpness
        )
        assert bits > 2 * HASH_TOLERANCE or ratio > SHARPNESS_TOLERANCE**2, (one, other)


def test_every_golden_page_is_recorded_once():
    found = sorted((p.scenario, p.document, p.page_no) for p in GOLDEN_BOOK.pages)
    assert found == [
        ("blurry", "receipt-blurry.jpg", 1),
        ("clean", "invoice.pdf", 1),
        ("clean", "invoice.pdf", 2),
        ("clean", "itemised-bill.xlsx", 1),
        ("clean", "prescription.docx", 1),
        ("clean", "receipt.jpg", 1),
        ("clean", "visit-summary.pdf", 1),
    ]


MONEY_AND_IDENTITY = {
    "insured_name",
    "national_id",
    "policy_number",
    "invoice_total",
    "line_amount",
}


def test_the_clean_claim_leaves_only_the_diagnosis_for_a_person():
    """BR-03: 0.95 for money and identity, 0.90 for the rest, below 0.70 left empty."""
    for recorded in GOLDEN_BOOK.pages:
        if recorded.scenario != "clean":
            continue
        assert recorded.page.type_confidence >= 0.80, recorded.document
        for field in recorded.fields:
            if field.field_key == "diagnosis":
                assert field.confidence < 0.70
            elif field.field_key in MONEY_AND_IDENTITY:
                assert field.confidence >= 0.95, (recorded.document, field)
            else:
                assert field.confidence >= 0.90, (recorded.document, field)


def test_the_clean_invoice_follows_the_golden_case():
    first, second = golden("clean", "invoice.pdf", 1), golden("clean", "invoice.pdf", 2)
    assert (first.page.page_type, second.page.page_type) == ("INVOICE", "INVOICE")
    header = {f.field_key: f.value for f in first.fields + second.fields if f.item_no == 0}
    assert header["invoice_number"] == "HN-0045821"
    assert header["provider_tax_code"] == "0101234567"
    assert header["invoice_total"] == "1.400.000"
    amounts = [
        f.value
        for f in sorted(first.fields, key=lambda f: f.item_no)
        if f.field_key == "line_amount"
    ]
    assert amounts == ["300.000", "250.000", "150.000", "300.000", "400.000"]
    for key in ("line_description", "line_quantity", "line_unit_price"):
        assert sorted(f.item_no for f in first.fields if f.field_key == key) == [1, 2, 3, 4, 5]


def test_the_other_clean_documents_follow_the_golden_case():
    assert golden("clean", "receipt.jpg").page.page_type == "RECEIPT"
    prescription = golden("clean", "prescription.docx")
    assert prescription.page.page_type == "PRESCRIPTION"
    assert [f.value for f in prescription.fields if f.field_key == "medication"] == [
        "Amoxicillin 500mg"
    ]
    visit = golden("clean", "visit-summary.pdf")
    assert visit.page.page_type == "VISIT_SUMMARY"
    assert [(f.value, f.confidence < 0.70) for f in visit.fields if f.field_key == "diagnosis"] == [
        ("J02.9", True)
    ]
    bill = golden("clean", "itemised-bill.xlsx")
    assert (bill.page.page_type, bill.fields) == ("OTHER", ())


def test_the_blurry_receipt_is_low_quality_with_low_confidences():
    blurry = golden("blurry", "receipt-blurry.jpg")
    assert blurry.page.quality_score < 0.50
    assert blurry.page.type_confidence < 0.80
    assert blurry.fields
    assert all(field.confidence < 0.70 for field in blurry.fields)


def test_every_golden_field_is_a_contract_field_with_a_region():
    keys = {key.value for key in FieldKey}
    for recorded in GOLDEN_BOOK.pages:
        for field in recorded.fields:
            assert field.field_key in keys
            assert field.value is not None and field.bbox is not None
            assert field.bbox["x"] + field.bbox["w"] <= 1 and field.bbox["y"] + field.bbox["h"] <= 1

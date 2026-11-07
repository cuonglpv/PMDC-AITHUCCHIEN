"""
The recorded answers: <replay dir>/<scenario>/ai-responses/pages.json, one entry per golden page. Read once at start-up
and never written, so the service stays stateless.
"""

from dataclasses import dataclass
from pathlib import Path
from typing import Annotated

from pydantic import BaseModel, ConfigDict, Field, TypeAdapter, ValidationError

from document_ai.contracts.models import Bbox, FieldKey, PageType
from document_ai.gateway.ports import FieldAnswer, PageAnswer
from document_ai.golden.fingerprint import Fingerprint, hamming, sharpness_ratio

Score = Annotated[float, Field(ge=0, le=1)]


class _Strict(BaseModel):
    model_config = ConfigDict(extra="forbid")


class _PageCheck(_Strict):
    pageType: PageType
    typeConfidence: Score
    qualityScore: Score


class _Field(_Strict):
    fieldKey: FieldKey
    itemNo: Annotated[int, Field(ge=0)]
    value: str | None
    confidence: Score
    bbox: Bbox | None


class _Page(_Strict):
    document: str
    pageNo: Annotated[int, Field(ge=1)]
    pageHash: Annotated[str, Field(pattern=r"^[0-9a-f]{16}$")]
    pageSharpness: Annotated[float, Field(ge=0)]
    pageCheck: _PageCheck
    fields: list[_Field]


_RECORDING = TypeAdapter(list[_Page])


@dataclass(frozen=True)
class RecordedPage:
    scenario: str
    document: str
    page_no: int
    fingerprint: Fingerprint
    page: PageAnswer
    fields: tuple[FieldAnswer, ...]

    @property
    def label(self) -> str:
        return f"{self.scenario}/{self.document} p{self.page_no}"


def _recorded(scenario: str, entry: _Page) -> RecordedPage:
    return RecordedPage(
        scenario=scenario,
        document=entry.document,
        page_no=entry.pageNo,
        fingerprint=Fingerprint(int(entry.pageHash, 16), entry.pageSharpness),
        page=PageAnswer(
            page_type=entry.pageCheck.pageType.value,
            type_confidence=entry.pageCheck.typeConfidence,
            quality_score=entry.pageCheck.qualityScore,
        ),
        fields=tuple(
            FieldAnswer(
                field_key=field.fieldKey.value,
                item_no=field.itemNo,
                value=field.value,
                confidence=field.confidence,
                bbox=field.bbox.model_dump() if field.bbox is not None else None,
            )
            for field in entry.fields
        ),
    )


@dataclass(frozen=True)
class ReplayBook:
    pages: tuple[RecordedPage, ...]

    @classmethod
    def load(cls, root: Path | str) -> "ReplayBook":
        files = sorted(Path(root).glob("*/ai-responses/pages.json"))
        if not files:
            raise ValueError(
                f"No recorded pages under {root}: expected <scenario>/ai-responses/pages.json"
            )
        pages: list[RecordedPage] = []
        for file in files:
            scenario = file.parent.parent.name
            try:
                entries = _RECORDING.validate_json(file.read_bytes())
            except ValidationError as error:
                raise ValueError(f"{file} is not a valid recording: {error}") from error
            pages.extend(_recorded(scenario, entry) for entry in entries)
        return cls(tuple(pages))

    def find(self, found: Fingerprint) -> RecordedPage | None:
        """The nearest recording within tolerance, or None when the image is not a golden page."""
        matches = [page for page in self.pages if page.fingerprint.matches(found)]
        return min(
            matches,
            key=lambda page: (
                hamming(page.fingerprint.page_hash, found.page_hash),
                sharpness_ratio(page.fingerprint.sharpness, found.sharpness),
            ),
            default=None,
        )

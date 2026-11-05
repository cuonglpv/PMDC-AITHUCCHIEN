"""
Model Gateway: the only way out to a provider (C3, rule 6).

Tasks ask for answers, one method per kind of task. An adapter decides how to get them: reading a value with its
region will most likely take OCR and a language model together, and that combination stays inside the adapter.
`model` names a pipeline, such as field-extractor v2.4. The gateway does not choose it.
"""

from dataclasses import dataclass
from typing import Protocol

from document_ai.contracts.models import MappingCategory, MappingLine, ModelRef, SummaryFact


class ModelError(Exception):
    """The provider answered with an error."""


class ModelTimeout(Exception):
    """The provider did not answer in time."""


class BadModelOutput(Exception):
    """The answer failed the shape check, which counts as a failed call (C3, rule 5)."""


@dataclass(frozen=True)
class PageAnswer:
    page_type: str
    type_confidence: float
    quality_score: float


@dataclass(frozen=True)
class FieldAnswer:
    field_key: str
    item_no: int
    value: str | None
    confidence: float
    bbox: dict[str, float] | None


@dataclass(frozen=True)
class MappingAnswer:
    line_id: str
    category_code: str
    confidence: float


@dataclass(frozen=True)
class SignalAnswer:
    line_id: str
    flag_type: str
    relevance_score: float
    reason: str


@dataclass(frozen=True)
class SentenceAnswer:
    text: str
    citations: list[str]


class ModelGateway(Protocol):
    def check_page(self, model: ModelRef, image: bytes) -> PageAnswer: ...

    def read_fields(
        self, model: ModelRef, page_type: str | None, image: bytes, catalogue: list[str]
    ) -> list[FieldAnswer]: ...

    def map_lines(
        self, model: ModelRef, lines: list[MappingLine], categories: list[MappingCategory]
    ) -> list[MappingAnswer]: ...

    def score_lines(
        self, model: ModelRef, diagnosis: str, lines: list[tuple[str, str]]
    ) -> list[SignalAnswer]: ...

    def draft_summary(self, model: ModelRef, facts: list[SummaryFact]) -> list[SentenceAnswer]: ...

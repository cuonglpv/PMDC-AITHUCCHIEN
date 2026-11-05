"""
A gateway that calls no provider, for local work and tests while the provider is still open.

Its answers are the careful ones: every value is left for a person, so nothing is auto-accepted. Replaying recorded
answers for the golden scenarios arrives with fixtures/golden/*/ai-responses.
"""

from document_ai.contracts.models import MappingCategory, MappingLine, ModelRef, SummaryFact
from document_ai.gateway.ports import (
    FieldAnswer,
    MappingAnswer,
    PageAnswer,
    SentenceAnswer,
    SignalAnswer,
)


class FakeGateway:
    def check_page(self, model: ModelRef, image: bytes) -> PageAnswer:
        return PageAnswer(page_type="OTHER", type_confidence=0.5, quality_score=0.9)

    def read_fields(
        self, model: ModelRef, page_type: str | None, image: bytes, catalogue: list[str]
    ) -> list[FieldAnswer]:
        return []

    def map_lines(
        self, model: ModelRef, lines: list[MappingLine], categories: list[MappingCategory]
    ) -> list[MappingAnswer]:
        return [
            MappingAnswer(
                line_id=str(line.lineId), category_code=categories[0].code, confidence=0.5
            )
            for line in lines
        ]

    def score_lines(
        self, model: ModelRef, diagnosis: str, lines: list[tuple[str, str]]
    ) -> list[SignalAnswer]:
        return [
            SignalAnswer(
                line_id=line_id,
                flag_type="SERVICE_DIAGNOSIS",
                relevance_score=1.0,
                reason="The fake gateway made no clinical judgement.",
            )
            for line_id, _ in lines
        ]

    def draft_summary(self, model: ModelRef, facts: list[SummaryFact]) -> list[SentenceAnswer]:
        return [SentenceAnswer(text=fact.text, citations=[fact.id]) for fact in facts]

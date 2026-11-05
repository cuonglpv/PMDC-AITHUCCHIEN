"""Bad model output is a failed call. These checks run on every answer, whichever provider gave it."""

from collections.abc import Iterable

from document_ai.gateway.ports import (
    BadModelOutput,
    FieldAnswer,
    MappingAnswer,
    PageAnswer,
    SentenceAnswer,
    SignalAnswer,
)

PAGE_TYPES = {"INVOICE", "RECEIPT", "PRESCRIPTION", "VISIT_SUMMARY", "LAB_RESULT", "OTHER"}
FLAG_TYPES = {"MEDICATION_DIAGNOSIS", "SERVICE_DIAGNOSIS", "LAB_DIAGNOSIS", "QUANTITY"}
BANNED_WORDS = ("fraud", "gian lận", "trục lợi")


def _score(name: str, value: float) -> None:
    if not 0 <= value <= 1:
        raise BadModelOutput(f"{name} {value} is outside 0 to 1")


def check_page(answer: PageAnswer) -> PageAnswer:
    if answer.page_type not in PAGE_TYPES:
        raise BadModelOutput(f"Unknown page type {answer.page_type}")
    _score("typeConfidence", answer.type_confidence)
    _score("qualityScore", answer.quality_score)
    return answer


def check_fields(answers: Iterable[FieldAnswer], catalogue: list[str]) -> list[FieldAnswer]:
    checked = []
    for answer in answers:
        if answer.field_key not in catalogue:
            raise BadModelOutput(f"{answer.field_key} was not asked for")
        _score("confidence", answer.confidence)
        if answer.value is not None and answer.bbox is None:
            # A value without a source cannot be accepted (BR-02).
            raise BadModelOutput(f"{answer.field_key} has a value but no region")
        if answer.bbox is not None:
            for part in ("x", "y", "w", "h"):
                _score(part, answer.bbox[part])
        checked.append(answer)
    return checked


def check_mappings(
    answers: list[MappingAnswer], line_ids: set[str], category_codes: set[str]
) -> list[MappingAnswer]:
    if {answer.line_id for answer in answers} != line_ids:
        raise BadModelOutput("Each line needs exactly one mapping")
    for answer in answers:
        if answer.category_code not in category_codes:
            raise BadModelOutput(f"{answer.category_code} is not in the list it was given")
        _score("confidence", answer.confidence)
    return answers


def check_signals(answers: list[SignalAnswer], line_ids: set[str]) -> list[SignalAnswer]:
    if {answer.line_id for answer in answers} != line_ids:
        raise BadModelOutput("Each line needs exactly one signal")
    for answer in answers:
        if answer.flag_type not in FLAG_TYPES:
            raise BadModelOutput(f"Unknown flag type {answer.flag_type}")
        _score("relevanceScore", answer.relevance_score)
        if len(answer.reason) > 500:
            raise BadModelOutput("The reason is longer than 500 characters")
        lowered = answer.reason.lower()
        if any(word in lowered for word in BANNED_WORDS):
            # A risk flag is a signal, not a finding (BR-08).
            raise BadModelOutput("The reason calls something fraud")
    return answers


def check_sentences(answers: list[SentenceAnswer], fact_ids: set[str]) -> list[SentenceAnswer]:
    for answer in answers:
        unknown = set(answer.citations) - fact_ids
        if unknown:
            raise BadModelOutput(f"Citations to facts it was not given: {sorted(unknown)}")
    return answers

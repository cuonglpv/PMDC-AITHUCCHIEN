"""
FIELD_EXTRACTION and PAGE_REREAD (AI-01). Returns each header field in the catalogue with its value, page, region and
confidence. A field it cannot find comes back with a null value; it never guesses.
"""

from uuid import UUID

from document_ai.contracts.models import (
    ExtractionDocumentResult,
    FieldExtractionRequest,
    FieldExtractionResponse,
    FieldKey,
    FieldSuggestion,
    ModelRef,
    PageFailure,
    PageRereadRequest,
    PageRereadResponse,
    TypedPageRef,
)
from document_ai.gateway import output_checks
from document_ai.gateway.ports import FieldAnswer, ModelGateway
from document_ai.pages.reader import PageReader
from document_ai.tasks.failures import fail_if_nothing_succeeded, read_page_or_fail

# These repeat per expense line or prescription item, so "not found" simply means no item.
REPEATING_FIELDS = {
    FieldKey.line_description,
    FieldKey.line_quantity,
    FieldKey.line_unit_price,
    FieldKey.line_amount,
    FieldKey.medication,
}


def _to_suggestion(answer: FieldAnswer, page_id: UUID) -> FieldSuggestion:
    found = answer.value is not None
    return FieldSuggestion(
        fieldKey=answer.field_key,
        itemNo=answer.item_no,
        value=answer.value,
        confidence=answer.confidence,
        pageId=page_id if found else None,
        bbox=answer.bbox if found else None,
    )


def _merge(best: dict[tuple[FieldKey, int], FieldSuggestion], suggestion: FieldSuggestion) -> None:
    """A field can appear on several pages of a document. The most confident reading wins."""
    key = (suggestion.fieldKey, suggestion.itemNo)
    current = best.get(key)
    current_has_value = current is not None and current.value is not None
    if (
        current is None
        or (suggestion.value is not None and not current_has_value)
        or (suggestion.value is not None and suggestion.confidence.root > current.confidence.root)
    ):
        best[key] = suggestion


def _with_not_found(
    best: dict[tuple[FieldKey, int], FieldSuggestion], catalogue: list[FieldKey]
) -> list[FieldSuggestion]:
    for field in catalogue:
        if field not in REPEATING_FIELDS and (field, 0) not in best:
            best[(field, 0)] = FieldSuggestion(
                fieldKey=field, itemNo=0, value=None, confidence=0.0, pageId=None, bbox=None
            )
    return sorted(best.values(), key=lambda item: (item.fieldKey.value, item.itemNo))


def _read_page(
    page: TypedPageRef,
    catalogue: list[FieldKey],
    model: ModelRef,
    gateway: ModelGateway,
    reader: PageReader,
) -> tuple[list[FieldSuggestion] | None, PageFailure | None]:
    keys = [field.value for field in catalogue]
    page_type = page.pageType.value if page.pageType is not None else None

    def work(image: bytes) -> list[FieldSuggestion]:
        answers = output_checks.check_fields(
            gateway.read_fields(model, page_type, image, keys), keys
        )
        return [_to_suggestion(answer, page.pageId) for answer in answers]

    return read_page_or_fail(page, reader, work)


def run(
    request: FieldExtractionRequest, gateway: ModelGateway, reader: PageReader
) -> FieldExtractionResponse:
    documents: list[ExtractionDocumentResult] = []
    failures: list[PageFailure] = []
    pages_read = 0

    for document in request.documents:
        best: dict[tuple[FieldKey, int], FieldSuggestion] = {}
        for page in document.pages:
            suggestions, failure = _read_page(
                page, request.fieldCatalogue, request.model, gateway, reader
            )
            if failure is not None:
                failures.append(failure)
                continue
            pages_read += 1
            for suggestion in suggestions or []:
                _merge(best, suggestion)
        documents.append(
            ExtractionDocumentResult(
                documentId=document.documentId,
                fields=_with_not_found(best, request.fieldCatalogue),
            )
        )

    fail_if_nothing_succeeded(pages_read, failures)
    return FieldExtractionResponse(
        runId=request.runId, model=request.model, documents=documents, failedPages=failures
    )


def reread(
    request: PageRereadRequest, gateway: ModelGateway, reader: PageReader
) -> PageRereadResponse:
    suggestions, failure = _read_page(
        request.page, request.fieldCatalogue, request.model, gateway, reader
    )
    if failure is not None:
        # One page was asked for, so there is nothing else to report.
        fail_if_nothing_succeeded(0, [failure])

    best: dict[tuple[FieldKey, int], FieldSuggestion] = {}
    for suggestion in suggestions or []:
        _merge(best, suggestion)
    return PageRereadResponse(
        runId=request.runId,
        model=request.model,
        documentId=request.documentId,
        fields=_with_not_found(best, request.fieldCatalogue),
    )

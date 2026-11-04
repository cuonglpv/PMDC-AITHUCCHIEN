"""PAGE_CHECK (AI-01). Always returns the best page type; the Claims API decides what is Unclassified or Low quality."""

from document_ai.contracts.models import (
    PageCheckRequest,
    PageCheckResponse,
    PageCheckResult,
    PageFailure,
)
from document_ai.gateway import output_checks
from document_ai.gateway.ports import ModelGateway
from document_ai.pages.reader import PageReader
from document_ai.tasks.failures import fail_if_nothing_succeeded, read_page_or_fail


def run(request: PageCheckRequest, gateway: ModelGateway, reader: PageReader) -> PageCheckResponse:
    results: list[PageCheckResult] = []
    failures: list[PageFailure] = []

    for page in request.pages:
        answer, failure = read_page_or_fail(
            page,
            reader,
            lambda image: output_checks.check_page(gateway.check_page(request.model, image)),
        )
        if failure is not None:
            failures.append(failure)
        elif answer is not None:
            results.append(
                PageCheckResult(
                    pageId=page.pageId,
                    pageType=answer.page_type,
                    typeConfidence=answer.type_confidence,
                    qualityScore=answer.quality_score,
                )
            )

    fail_if_nothing_succeeded(len(results), failures)
    return PageCheckResponse(
        runId=request.runId, model=request.model, pages=results, failedPages=failures
    )

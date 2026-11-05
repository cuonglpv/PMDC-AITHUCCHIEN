"""SUMMARY_DRAFT (AI-04). Writes sentences that cite only the facts it was given. It submits nothing."""

from document_ai.contracts.models import (
    SummaryDraftRequest,
    SummaryDraftResponse,
    SummaryDraftSentence,
)
from document_ai.gateway import output_checks
from document_ai.gateway.ports import ModelGateway, SentenceAnswer
from document_ai.tasks.failures import run_call


def run(request: SummaryDraftRequest, gateway: ModelGateway) -> SummaryDraftResponse:
    fact_ids = {fact.id for fact in request.facts}

    def call() -> list[SentenceAnswer]:
        answers = gateway.draft_summary(request.model, request.facts)
        return output_checks.check_sentences(answers, fact_ids)

    answers = run_call(call)
    return SummaryDraftResponse(
        runId=request.runId,
        model=request.model,
        sentences=[
            SummaryDraftSentence(text=answer.text, citations=answer.citations) for answer in answers
        ],
    )

"""
CLINICAL_CHECK (AI-03). Returns a signal type, relevance score and one-sentence reason per line. It does not raise
flags or call anything fraud: the Claims API creates a risk_flag from the score and sets its severity.
"""

from document_ai.contracts.models import ClinicalCheckRequest, ClinicalCheckResponse, ClinicalSignal
from document_ai.gateway import output_checks
from document_ai.gateway.ports import ModelGateway, SignalAnswer
from document_ai.tasks.failures import run_call


def run(request: ClinicalCheckRequest, gateway: ModelGateway) -> ClinicalCheckResponse:
    lines = [(str(line.lineId), line.description) for line in request.lines]
    line_ids = {line_id for line_id, _ in lines}

    def call() -> list[SignalAnswer]:
        answers = gateway.score_lines(request.model, request.diagnosis, lines)
        return output_checks.check_signals(answers, line_ids)

    answers = run_call(call)
    return ClinicalCheckResponse(
        runId=request.runId,
        model=request.model,
        signals=[
            ClinicalSignal(
                lineId=answer.line_id,
                flagType=answer.flag_type,
                relevanceScore=answer.relevance_score,
                reason=answer.reason,
            )
            for answer in answers
        ],
    )

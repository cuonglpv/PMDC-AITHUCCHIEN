"""BENEFIT_MAPPING (AI-02). Picks a category from the list it was given. Never decides coverage or amounts."""

from document_ai.contracts.models import BenefitMappingRequest, BenefitMappingResponse, LineMapping
from document_ai.gateway import output_checks
from document_ai.gateway.ports import MappingAnswer, ModelGateway
from document_ai.tasks.failures import run_call


def run(request: BenefitMappingRequest, gateway: ModelGateway) -> BenefitMappingResponse:
    line_ids = {str(line.lineId) for line in request.lines}
    codes = {category.code for category in request.categories}

    def call() -> list[MappingAnswer]:
        answers = gateway.map_lines(request.model, request.lines, request.categories)
        return output_checks.check_mappings(answers, line_ids, codes)

    answers = run_call(call)
    return BenefitMappingResponse(
        runId=request.runId,
        model=request.model,
        mappings=[
            LineMapping(
                lineId=answer.line_id,
                categoryCode=answer.category_code,
                confidence=answer.confidence,
            )
            for answer in answers
        ],
    )

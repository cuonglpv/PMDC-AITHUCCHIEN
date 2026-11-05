"""
Task API: one operation per ai_task, with the input and output shapes generated from document-ai.yaml.
Routes are plain functions because the storage client blocks; FastAPI runs them on a thread pool.
"""

from fastapi import APIRouter, Depends, Request

from document_ai.contracts.models import (
    BenefitMappingRequest,
    BenefitMappingResponse,
    ClinicalCheckRequest,
    ClinicalCheckResponse,
    FieldExtractionRequest,
    FieldExtractionResponse,
    PageCheckRequest,
    PageCheckResponse,
    PageRereadRequest,
    PageRereadResponse,
    SummaryDraftRequest,
    SummaryDraftResponse,
)
from document_ai.gateway.ports import ModelGateway
from document_ai.pages.reader import PageReader
from document_ai.security import require_service_key
from document_ai.tasks import (
    benefit_mapping,
    clinical_signals,
    field_extraction,
    page_check,
    summary_draft,
)

router = APIRouter(prefix="/tasks", dependencies=[Depends(require_service_key)])


def _gateway(request: Request) -> ModelGateway:
    return request.app.state.gateway


def _reader(request: Request) -> PageReader:
    return request.app.state.reader


@router.post("/page-check", operation_id="runPageCheck")
def run_page_check(
    body: PageCheckRequest,
    gateway: ModelGateway = Depends(_gateway),
    reader: PageReader = Depends(_reader),
) -> PageCheckResponse:
    return page_check.run(body, gateway, reader)


@router.post("/field-extraction", operation_id="runFieldExtraction")
def run_field_extraction(
    body: FieldExtractionRequest,
    gateway: ModelGateway = Depends(_gateway),
    reader: PageReader = Depends(_reader),
) -> FieldExtractionResponse:
    return field_extraction.run(body, gateway, reader)


@router.post("/page-reread", operation_id="runPageReread")
def run_page_reread(
    body: PageRereadRequest,
    gateway: ModelGateway = Depends(_gateway),
    reader: PageReader = Depends(_reader),
) -> PageRereadResponse:
    return field_extraction.reread(body, gateway, reader)


@router.post("/benefit-mapping", operation_id="runBenefitMapping")
def run_benefit_mapping(
    body: BenefitMappingRequest, gateway: ModelGateway = Depends(_gateway)
) -> BenefitMappingResponse:
    return benefit_mapping.run(body, gateway)


@router.post("/clinical-check", operation_id="runClinicalCheck")
def run_clinical_check(
    body: ClinicalCheckRequest, gateway: ModelGateway = Depends(_gateway)
) -> ClinicalCheckResponse:
    return clinical_signals.run(body, gateway)


@router.post("/summary-draft", operation_id="runSummaryDraft")
def run_summary_draft(
    body: SummaryDraftRequest, gateway: ModelGateway = Depends(_gateway)
) -> SummaryDraftResponse:
    return summary_draft.run(body, gateway)

"""The Document AI Service. It is stateless: no database, no files written, nothing kept between requests."""

from fastapi import FastAPI

from document_ai.api.tasks import router
from document_ai.config import Settings
from document_ai.errors import add_error_handlers
from document_ai.gateway.fake.gateway import FakeGateway
from document_ai.gateway.ports import ModelGateway
from document_ai.pages.reader import PageReader, S3PageReader


def _build_gateway(settings: Settings) -> ModelGateway:
    if settings.gateway == "fake":
        return FakeGateway()
    raise ValueError(f"Unknown gateway: {settings.gateway}. No provider has been chosen yet.")


def create_app(
    settings: Settings | None = None,
    gateway: ModelGateway | None = None,
    reader: PageReader | None = None,
    openapi_url: str | None = None,
) -> FastAPI:
    settings = settings or Settings()  # type: ignore[call-arg]

    app = FastAPI(
        title="ClaimLens Document AI Service",
        docs_url=None,
        redoc_url=None,
        openapi_url=openapi_url,
    )
    app.state.settings = settings
    app.state.gateway = gateway or _build_gateway(settings)
    app.state.reader = reader or S3PageReader(settings)

    add_error_handlers(app)
    app.include_router(router, prefix="/v1")

    @app.get("/health", include_in_schema=False)
    def health() -> dict[str, str]:
        return {"status": "up"}

    return app

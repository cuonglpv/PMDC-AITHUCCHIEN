"""RFC 9457 responses. `code` becomes ai_run.error_code in the Claims API, which then follows the manual fallback."""

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException

from document_ai.contracts.models import Code, Problem


class TaskFailure(Exception):
    """A whole task failed. The status and code come from the contract."""

    def __init__(self, status: int, code: Code, title: str) -> None:
        super().__init__(title)
        self.status = status
        self.code = code
        self.title = title


def _problem(status: int, code: Code, title: str) -> JSONResponse:
    body = Problem(title=title, status=status, code=code)
    return JSONResponse(
        body.model_dump(mode="json", exclude_none=True),
        status_code=status,
        media_type="application/problem+json",
    )


def add_error_handlers(app: FastAPI) -> None:
    @app.exception_handler(TaskFailure)
    async def on_task_failure(_: Request, error: TaskFailure) -> JSONResponse:
        return _problem(error.status, error.code, error.title)

    @app.exception_handler(RequestValidationError)
    async def on_invalid_request(_: Request, __: RequestValidationError) -> JSONResponse:
        # A request that breaks the contract is a bug in the caller.
        return _problem(422, Code.INVALID_REQUEST, "The request does not match the contract")

    @app.exception_handler(HTTPException)
    async def on_http_error(_: Request, error: HTTPException) -> JSONResponse:
        # A body that cannot be parsed reaches here as a 400; the contract calls that INVALID_REQUEST too.
        if error.status_code == 400:
            return _problem(422, Code.INVALID_REQUEST, "The request does not match the contract")
        return JSONResponse({"detail": error.detail}, status_code=error.status_code)

    @app.exception_handler(Exception)
    async def on_unexpected(_: Request, __: Exception) -> JSONResponse:
        return _problem(500, Code.SERVICE_ERROR, "An unexpected failure in this service")

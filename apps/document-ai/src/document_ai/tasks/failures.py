"""Turns gateway errors into the failure codes of the contract."""

from collections.abc import Callable

from document_ai.contracts.models import Code, Code1, PageFailure, PageRef
from document_ai.errors import TaskFailure
from document_ai.gateway.ports import BadModelOutput, ModelError, ModelTimeout
from document_ai.pages.reader import PageReader, PageReadError


def task_failure(error: Exception) -> TaskFailure:
    match error:
        case ModelTimeout():
            return TaskFailure(504, Code.MODEL_TIMEOUT, "The provider did not answer in time")
        case BadModelOutput():
            return TaskFailure(502, Code.BAD_MODEL_OUTPUT, "The model's answer failed the check")
        case PageReadError():
            return TaskFailure(502, Code.PAGE_READ_FAILED, "No page image could be read")
        case _:
            return TaskFailure(502, Code.MODEL_ERROR, "The provider answered with an error")


def run_call[T](call: Callable[[], T]) -> T:
    """For tasks with no per-page result: any failure fails the whole task."""
    try:
        return call()
    except (ModelError, ModelTimeout, BadModelOutput) as error:
        raise task_failure(error) from error


def read_page_or_fail[T](
    page: PageRef,
    reader: PageReader,
    work: Callable[[bytes], T],
) -> tuple[T | None, PageFailure | None]:
    """
    One bad page does not fail a run: it is reported, and only that page falls back. A timeout is different: the
    provider is unreachable, so the rest of the run would only wait for the same thing.
    """
    try:
        return work(reader.read(page.imageKey)), None
    except PageReadError:
        return None, PageFailure(pageId=page.pageId, code=Code1.PAGE_READ_FAILED)
    except BadModelOutput:
        return None, PageFailure(pageId=page.pageId, code=Code1.BAD_MODEL_OUTPUT)
    except ModelError:
        return None, PageFailure(pageId=page.pageId, code=Code1.MODEL_ERROR)
    except ModelTimeout as error:
        raise task_failure(error) from error


def fail_if_nothing_succeeded(succeeded: int, failures: list[PageFailure]) -> None:
    if succeeded > 0 or not failures:
        return
    first = failures[0].code
    if first == Code1.PAGE_READ_FAILED:
        raise task_failure(PageReadError())
    raise task_failure(BadModelOutput() if first == Code1.BAD_MODEL_OUTPUT else ModelError())

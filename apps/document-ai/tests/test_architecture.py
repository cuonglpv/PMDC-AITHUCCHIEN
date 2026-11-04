"""C3 rules that a reader of the imports can check."""

import ast
from pathlib import Path

SOURCE = Path(__file__).parents[1] / "src" / "document_ai"


def imports_of(path: Path) -> set[str]:
    found: set[str] = set()
    for node in ast.walk(ast.parse(path.read_text(encoding="utf-8"))):
        if isinstance(node, ast.Import):
            found.update(alias.name for alias in node.names)
        elif isinstance(node, ast.ImportFrom) and node.module:
            found.add(node.module)
    return found


def all_python_files() -> list[Path]:
    return [path for path in SOURCE.rglob("*.py") if path.name != "models.py"]


def test_the_service_has_no_database_client():
    banned = ("sqlalchemy", "psycopg", "asyncpg")
    for path in all_python_files():
        assert not any(name.startswith(banned) for name in imports_of(path)), path


def test_only_the_page_reader_uses_the_storage_client():
    for path in all_python_files():
        if path.parent.name != "pages":
            assert not any(name.startswith(("boto3", "botocore")) for name in imports_of(path)), (
                path
            )


def test_tasks_reach_providers_only_through_the_gateway():
    for path in (SOURCE / "tasks").rglob("*.py"):
        assert not any(".providers" in name for name in imports_of(path)), path


def test_the_page_reader_has_no_way_to_write():
    text = (SOURCE / "pages" / "reader.py").read_text(encoding="utf-8")
    assert "put_object" not in text
    assert "delete_object" not in text

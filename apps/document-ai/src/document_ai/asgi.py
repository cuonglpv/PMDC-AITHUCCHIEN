"""What the server runs: `fastapi run src/document_ai/asgi.py`. It reads its settings from the environment."""

from document_ai.main import create_app

app = create_app()

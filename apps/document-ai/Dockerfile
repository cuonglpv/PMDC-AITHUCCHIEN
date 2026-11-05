FROM python:3.12-slim

ENV UV_LINK_MODE=copy UV_PROJECT_ENVIRONMENT=/app/.venv
RUN pip install --no-cache-dir uv==0.11.32

WORKDIR /app
COPY pyproject.toml uv.lock ./
COPY src ./src
RUN uv sync --frozen --no-dev

USER nobody
EXPOSE 8001
CMD ["/app/.venv/bin/fastapi", "run", "src/document_ai/asgi.py", "--port", "8001"]

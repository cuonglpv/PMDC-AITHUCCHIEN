"""
Prints the fingerprint of every page of a claim, so pages.json holds hashes of pages the real Claims API pipeline drew
(PDFium, LibreOffice), never of the HTML they were made from.

    uv run python -m document_ai.golden.record --claim <claimId>

Environment:
    CLAIMLENS_TOKEN                    access token of a user who can see the claim, to list its documents
    DOCUMENT_AI_STORAGE_ACCESS_KEY     the service's read-only storage identity (pages/ only); also
    DOCUMENT_AI_STORAGE_SECRET_KEY     DOCUMENT_AI_STORAGE_ENDPOINT and DOCUMENT_AI_STORAGE_BUCKET if not the default
    DOCUMENT_AI_REPLAY_DIR             optional: also say which recorded page each one matches

That identity can read page images but cannot list them, so the claim's documents come from the Claims API
(listDocuments) and each page is read at its key, pages/<claimId>/<documentId>/<pageNo>.png.
"""

import argparse
import json
import os
import sys
import urllib.request
from typing import Any

from document_ai.config import Settings
from document_ai.golden.fingerprint import fingerprint, hamming
from document_ai.golden.replay import ReplayBook
from document_ai.pages.reader import PageReader, S3PageReader


def describe_pages(
    claim_id: str, documents: list[dict[str, Any]], reader: PageReader, book: ReplayBook | None
) -> list[dict[str, Any]]:
    rows = []
    for document in documents:
        for page in document["pages"]:
            key = f"pages/{claim_id}/{document['id']}/{page['pageNo']}.png"
            found = fingerprint(reader.read(key))
            row: dict[str, Any] = {
                "document": document["originalFilename"],
                "pageNo": page["pageNo"],
                "pageHash": f"{found.page_hash:016x}",
                "pageSharpness": round(found.sharpness, 2),
            }
            if book is not None:
                match = book.find(found)
                row["matches"] = (
                    None
                    if match is None
                    else f"{match.label}, {hamming(match.fingerprint.page_hash, found.page_hash)} bits"
                )
            rows.append(row)
    return rows


def _documents(api: str, claim_id: str, token: str) -> list[dict[str, Any]]:
    request = urllib.request.Request(
        f"{api.rstrip('/')}/claims/{claim_id}/documents",
        headers={"Authorization": f"Bearer {token}", "Accept": "application/json"},
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        return [item for item in json.load(response)["items"] if item["status"] == "READY"]


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--claim", required=True, help="the claim whose pages to fingerprint")
    parser.add_argument("--api", default="http://localhost:8088/api/v1", help="Claims API base URL")
    args = parser.parse_args(argv)

    token = os.environ.get("CLAIMLENS_TOKEN")
    if not token:
        parser.error("set CLAIMLENS_TOKEN to an access token that can see the claim")

    # The recorder serves no requests, so it needs no service key; it only borrows the storage settings.
    settings = Settings(service_key="")
    book = ReplayBook.load(settings.replay_dir) if settings.replay_dir else None
    documents = _documents(args.api, args.claim, token)
    for row in describe_pages(args.claim, documents, S3PageReader(settings), book):
        sys.stdout.write(json.dumps(row, ensure_ascii=False) + "\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

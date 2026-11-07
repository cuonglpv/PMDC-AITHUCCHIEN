import { ApiProblem } from "./problem";
import { apiOrigin, session } from "./session";
import type { Document, Problem } from "./types";

export type UploadOptions = {
  /** Called as the bytes leave the browser, from 0 to 1. */
  onProgress?: (fraction: number) => void;
  /** Aborting cancels the request; the promise then rejects with an `AbortError`. */
  signal?: AbortSignal;
};

type Answer = { status: number; body: unknown };

/**
 * `uploadDocument` (US03): one file per request. It uses XMLHttpRequest because `fetch` cannot report upload progress.
 * Like `authFetch`, it sends the access token and, on 401 `AUTH_REQUIRED`, refreshes once and sends the file again.
 * A refusal throws an `ApiProblem` (413 `FILE_TOO_LARGE`, 415 `UNSUPPORTED_FILE_TYPE`, 409 `CLAIM_STATE_CONFLICT`, ...).
 */
export async function uploadDocument(claimId: string, file: File, options: UploadOptions = {}): Promise<Document> {
  let answer = await send(claimId, file, session.getToken(), options);

  if (answer.status === 401 && (answer.body as Problem | null)?.code === "AUTH_REQUIRED") {
    const token = await session.refresh();
    if (token) {
      options.onProgress?.(0);
      answer = await send(claimId, file, token, options);
    }
  }

  if (answer.status === 201 && answer.body) {
    return answer.body as Document;
  }
  throw new ApiProblem(answer.status, isProblem(answer.body) ? answer.body : null);
}

function send(claimId: string, file: File, token: string | null, { onProgress, signal }: UploadOptions): Promise<Answer> {
  return new Promise((resolve, reject) => {
    if (signal?.aborted) {
      reject(abortError());
      return;
    }

    const xhr = new XMLHttpRequest();
    const abort = () => xhr.abort();
    const done = () => signal?.removeEventListener("abort", abort);

    xhr.open("POST", `${apiOrigin()}/api/v1/claims/${encodeURIComponent(claimId)}/documents`);
    xhr.setRequestHeader("Accept", "application/json");
    if (token) {
      xhr.setRequestHeader("Authorization", `Bearer ${token}`);
    }

    xhr.upload.addEventListener("progress", (event) => {
      if (event.lengthComputable && event.total > 0) {
        onProgress?.(event.loaded / event.total);
      }
    });
    xhr.addEventListener("load", () => {
      done();
      resolve({ status: xhr.status, body: parse(xhr.responseText) });
    });
    xhr.addEventListener("error", () => {
      done();
      // The network failed or the proxy dropped the connection: there is no answer to read.
      reject(new ApiProblem(0, null));
    });
    xhr.addEventListener("abort", () => {
      done();
      reject(abortError());
    });
    signal?.addEventListener("abort", abort);

    const form = new FormData();
    form.append("file", file, file.name);
    xhr.send(form);
  });
}

function parse(text: string): unknown {
  try {
    return text ? JSON.parse(text) : null;
  } catch {
    return null;
  }
}

function isProblem(body: unknown): body is Problem {
  return typeof body === "object" && body !== null && "code" in body;
}

function abortError() {
  return new DOMException("The upload was cancelled.", "AbortError");
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}

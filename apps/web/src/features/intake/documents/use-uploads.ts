"use client";

import { useCallback, useEffect, useState } from "react";
import { ApiProblem } from "@/lib/api/problem";
import type { Document } from "@/lib/api/types";
import { refusalBeforeUpload } from "@/lib/labels/documents";
import { isAbortError, uploadDocument } from "@/lib/api/upload";

export type Upload = {
  key: string;
  file: File;
  /** From 0 to 1. */
  progress: number;
  /** Set when the API refused the file or the network failed. The row stays until the officer dismisses it. */
  error: ApiProblem | null;
  controller: AbortController;
};

let nextKey = 0;

/** The refusal the API would give, made in the browser so a file it cannot take is never sent (413 or 415). */
function localRefusal(file: File): ApiProblem | null {
  const code = refusalBeforeUpload(file);
  if (!code) {
    return null;
  }
  const status = code === "FILE_TOO_LARGE" ? 413 : 415;
  return new ApiProblem(status, { type: "about:blank", title: "Not sent", status, code, requestId: "" });
}

/**
 * Files on their way to the API, one request each, so each row shows its own progress. A stored file leaves this
 * list and joins the claim's documents through `onStored`.
 */
export function useUploads(claimId: string, onStored: (document: Document) => void) {
  const [uploads, setUploads] = useState<Upload[]>([]);

  const update = useCallback(
    (key: string, change: Partial<Upload>) =>
      setUploads((current) => current.map((upload) => (upload.key === key ? { ...upload, ...change } : upload))),
    [],
  );
  const drop = useCallback(
    (key: string) => setUploads((current) => current.filter((upload) => upload.key !== key)),
    [],
  );

  const run = useCallback(
    async (upload: Upload) => {
      try {
        const document = await uploadDocument(claimId, upload.file, {
          signal: upload.controller.signal,
          onProgress: (progress) => update(upload.key, { progress }),
        });
        drop(upload.key);
        onStored(document);
      } catch (error) {
        if (isAbortError(error)) {
          drop(upload.key);
        } else {
          update(upload.key, { error: error instanceof ApiProblem ? error : new ApiProblem(0, null) });
        }
      }
    },
    [claimId, drop, onStored, update],
  );

  const add = (files: File[]) => {
    const added: Upload[] = files.map((file) => ({
      key: `upload-${(nextKey += 1)}`,
      file,
      progress: 0,
      error: localRefusal(file),
      controller: new AbortController(),
    }));
    setUploads((current) => [...current, ...added]);
    // A file the browser already refused is listed with its reason and never sent.
    added.filter((upload) => !upload.error).forEach((upload) => void run(upload));
  };

  const retry = (key: string) => {
    const upload = uploads.find((item) => item.key === key);
    if (!upload) {
      return;
    }
    const again = { ...upload, progress: 0, error: null, controller: new AbortController() };
    update(key, again);
    void run(again);
  };

  // Leaving the browser page would lose a file still on its way; the browser asks first.
  const running = uploads.filter((upload) => !upload.error).length;
  useEffect(() => {
    if (running === 0) {
      return;
    }
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [running]);

  return {
    uploads,
    running,
    add,
    retry,
    cancel: (key: string) => uploads.find((upload) => upload.key === key)?.controller.abort(),
    dismiss: drop,
  };
}

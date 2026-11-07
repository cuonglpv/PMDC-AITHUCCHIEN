"use client";

import type { ReactNode } from "react";
import { StatusPill } from "@/components/ui/StatusPill";
import type { ApiProblem } from "@/lib/api/problem";
import type { Document } from "@/lib/api/types";
import { formatFileSize } from "@/lib/format";
import { INTAKE_PROBLEM_LABEL } from "@/lib/labels/documents";
import type { Upload } from "./use-uploads";

const IMAGE_FORMATS = new Set(["JPG", "PNG"]);

export function problemMessage(problem: ApiProblem, fallback: string): string {
  return (problem.code && INTAKE_PROBLEM_LABEL[problem.code]) || fallback;
}

/** A file still on its way: progress and Cancel, or the reason it was refused next to it. */
export function UploadRow({
  upload,
  onCancel,
  onRetry,
  onDismiss,
}: {
  upload: Upload;
  onCancel: () => void;
  onRetry: () => void;
  onDismiss: () => void;
}) {
  const { file, error } = upload;
  const percent = Math.round(upload.progress * 100);
  // A refusal (413, 415, 409) will not change by sending the same file again; a dropped connection might.
  const canRetry = error !== null && (error.status === 0 || error.status >= 500);

  return (
    <FileRow
      icon={file.type.startsWith("image/") ? "image" : "description"}
      name={file.name}
      meta={formatFileSize(file.size)}
      status={
        error ? (
          <StatusPill tone="danger">Not uploaded</StatusPill>
        ) : (
          <span className="flex items-center gap-2">
            <span
              role="progressbar"
              aria-label={`Uploading ${file.name}`}
              aria-valuemin={0}
              aria-valuemax={100}
              aria-valuenow={percent}
              className="block h-1 w-40 bg-surface-sunken"
            >
              <span className="block h-1 bg-ink transition-[width] duration-150" style={{ width: `${percent}%` }} />
            </span>
            <span className="w-10 font-mono text-xs">{percent}%</span>
          </span>
        )
      }
      actions={
        error ? (
          <>
            {canRetry && (
              <IconButton icon="refresh" label={`Retry ${file.name}`} onClick={onRetry} />
            )}
            <IconButton icon="close" label={`Dismiss ${file.name}`} onClick={onDismiss} />
          </>
        ) : (
          <IconButton icon="close" label={`Cancel upload of ${file.name}`} onClick={onCancel} />
        )
      }
      message={
        error && (
          <span role="alert" className="text-danger">
            {problemMessage(error, "The upload failed. Check the connection and try again.")}
          </span>
        )
      }
    />
  );
}

/** A stored file: converting, ready, or rejected with its reason. */
export function DocumentRow({
  document,
  removable,
  removing,
  removeError,
  onRemove,
}: {
  document: Document;
  removable: boolean;
  removing: boolean;
  removeError: ApiProblem | undefined;
  onRemove: () => void;
}) {
  const busy = document.status === "UPLOADING" || document.status === "CONVERTING";
  const pages = document.pageCount ?? document.pages.length;

  return (
    <FileRow
      icon={IMAGE_FORMATS.has(document.fileFormat) ? "image" : "description"}
      name={document.originalFilename}
      meta={busy || pages === 0 ? formatFileSize(document.sizeBytes) : `${pages} ${pages === 1 ? "page" : "pages"}`}
      status={
        busy ? (
          <span className="flex items-center gap-1.5 text-[13px] text-on-surface-muted">
            <span className="material-symbols-outlined animate-spin text-[16px]" aria-hidden="true">
              progress_activity
            </span>
            Converting…
          </span>
        ) : document.status === "REJECTED" ? (
          <StatusPill tone="danger">Rejected</StatusPill>
        ) : (
          <span className="flex items-center gap-1 text-[13px] text-success">
            <span className="material-symbols-outlined text-[18px]" aria-hidden="true">
              check
            </span>
            <span className="sr-only">Ready</span>
          </span>
        )
      }
      actions={
        removable && (
          <IconButton
            icon="close"
            label={`Remove ${document.originalFilename}`}
            disabled={removing}
            onClick={onRemove}
          />
        )
      }
      message={
        <>
          {document.status === "REJECTED" && (
            <span className="text-danger">{document.rejectionReason ?? "The file could not be read."}</span>
          )}
          {removeError && (
            <span role="alert" className="block text-danger">
              {problemMessage(removeError, "The file could not be removed. Please try again.")}
            </span>
          )}
        </>
      }
    />
  );
}

function FileRow({
  icon,
  name,
  meta,
  status,
  actions,
  message,
}: {
  icon: string;
  name: string;
  meta: string;
  status: ReactNode;
  actions: ReactNode;
  message: ReactNode;
}) {
  return (
    <li className="border-t border-line px-4 py-2.5 first:border-t-0" aria-label={name}>
      <div className="flex min-h-9 items-center gap-3">
        <span className="material-symbols-outlined text-[18px] text-on-surface-muted" aria-hidden="true">
          {icon}
        </span>
        <span className="min-w-0 truncate text-sm font-medium">{name}</span>
        <span className="shrink-0 font-mono text-xs text-on-surface-muted">{meta}</span>
        <span className="ml-auto flex shrink-0 items-center gap-2">
          {status}
          {actions}
        </span>
      </div>
      {message && <div className="pl-8 text-[13px]">{message}</div>}
    </li>
  );
}

function IconButton({
  icon,
  label,
  onClick,
  disabled,
}: {
  icon: string;
  label: string;
  onClick: () => void;
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
      className="flex size-8 items-center justify-center rounded-sm text-on-surface-muted hover:bg-surface-sunken hover:text-ink disabled:opacity-50"
    >
      <span className="material-symbols-outlined text-[18px]" aria-hidden="true">
        {icon}
      </span>
    </button>
  );
}

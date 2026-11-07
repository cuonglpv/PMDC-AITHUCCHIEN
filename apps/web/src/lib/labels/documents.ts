import type { ChosenPageType, DocumentStatus, ErrorCode, PageQuality, PageType } from "@/lib/api/types";

export const DOCUMENT_STATUS_LABEL: Record<DocumentStatus, string> = {
  UPLOADING: "Uploading",
  CONVERTING: "Converting",
  READY: "Ready",
  REJECTED: "Rejected",
  REMOVED: "Removed",
};

export const PAGE_TYPE_LABEL: Record<PageType, string> = {
  INVOICE: "Invoice",
  RECEIPT: "Receipt",
  PRESCRIPTION: "Prescription",
  VISIT_SUMMARY: "Visit summary",
  LAB_RESULT: "Lab result",
  OTHER: "Other",
  UNCLASSIFIED: "Unclassified",
};

/** The types a person may choose for a page (`reclassifyPage` takes any type except Unclassified), in menu order. */
export const CHOSEN_PAGE_TYPES: readonly ChosenPageType[] = [
  "INVOICE",
  "RECEIPT",
  "PRESCRIPTION",
  "VISIT_SUMMARY",
  "LAB_RESULT",
  "OTHER",
];

export const PAGE_QUALITY_LABEL: Record<PageQuality, string> = {
  NOT_CHECKED: "Not checked",
  READABLE: "Readable",
  LOW_QUALITY: "Low quality, re-capture",
};

/**
 * What the file picker offers, from the `uploadDocument` encoding in claims-api.yaml. A hint only: the API checks type
 * and size and answers 415 or 413.
 */
export const UPLOAD_ACCEPT =
  ".jpg,.jpeg,.png,.pdf,.docx,.xlsx,image/jpeg,image/png,application/pdf," +
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document," +
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

/**
 * The API's file limit: 20 MB, counted as 20 × 1024 × 1024 bytes like `PreparationOptions.MaxFileBytes` and the
 * database check. The browser refuses a larger file before sending it, because the proxy in front of the API cuts a
 * large request off with a bare 413 or a dropped connection. The API stays the real check.
 */
export const MAX_UPLOAD_BYTES = 20 * 1024 * 1024;

/** Extensions the API can take. A fast hint only: the API decides by the file's bytes and may still answer 415. */
export const UPLOAD_EXTENSIONS = ["jpg", "jpeg", "png", "pdf", "docx", "xlsx"] as const;

/** Why the browser will not send a file, with the same code the API would answer; null when it may be sent. */
export function refusalBeforeUpload(file: File): "FILE_TOO_LARGE" | "UNSUPPORTED_FILE_TYPE" | null {
  const extension = file.name.includes(".") ? file.name.split(".").pop()!.toLowerCase() : "";
  if (!(UPLOAD_EXTENSIONS as readonly string[]).includes(extension)) {
    return "UNSUPPORTED_FILE_TYPE";
  }
  return file.size > MAX_UPLOAD_BYTES ? "FILE_TOO_LARGE" : null;
}

/** Why a file or a submission was refused, by the code the API sends (docs/07-api-contract.md). */
export const INTAKE_PROBLEM_LABEL: Partial<Record<ErrorCode, string>> = {
  FILE_TOO_LARGE: "This file is larger than 20 MB. Compress or split it, then upload it again.",
  UNSUPPORTED_FILE_TYPE: "This file type is not accepted. Use JPG, PNG, PDF, DOCX or XLSX.",
  CLAIM_STATE_CONFLICT: "This claim has already been submitted, so it can no longer change.",
  UPLOAD_IN_PROGRESS: "A file is still uploading or converting. Wait for it to finish, then submit.",
  NO_READY_DOCUMENT: "No document is ready yet. Upload at least one file that converts, then submit.",
  NOT_FOUND: "This file no longer exists. It may have been removed already.",
};

/** When `reclassifyPage` fails for a reason with no label above. */
export const PAGE_TYPE_NOT_SAVED = "The page type could not be saved. Please try again.";

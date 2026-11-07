import type { DocumentStatus, ErrorCode, PageQuality, PageType } from "@/lib/api/types";

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

/** Why a file or a submission was refused, by the code the API sends (docs/07-api-contract.md). */
export const INTAKE_PROBLEM_LABEL: Partial<Record<ErrorCode, string>> = {
  FILE_TOO_LARGE: "This file is larger than 20 MB. Compress or split it, then upload it again.",
  UNSUPPORTED_FILE_TYPE: "This file type is not accepted. Use JPG, PNG, PDF, DOCX or XLSX.",
  CLAIM_STATE_CONFLICT: "This claim has already been submitted, so it can no longer change.",
  UPLOAD_IN_PROGRESS: "A file is still uploading or converting. Wait for it to finish, then submit.",
  NO_READY_DOCUMENT: "No document is ready yet. Upload at least one file that converts, then submit.",
  NOT_FOUND: "This file no longer exists. It may have been removed already.",
};

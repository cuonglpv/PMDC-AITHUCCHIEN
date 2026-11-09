import type { ClaimDetail, ClaimField, ClaimProgress } from "@/lib/api/types";
import { claimDetail } from "./intake";

/** Reviewer test data, typed from schema.d.ts so a contract change breaks the tests that rely on it. */
export const REVIEWER = { id: "10000000-0000-4000-8000-000000000002", fullName: "Trần Thu Hà", initials: "TH" };

export const INVOICE_ID = "40000000-0000-4000-8000-000000000001";
export const SUMMARY_ID = "40000000-0000-4000-8000-000000000002";
export const PAGE_1 = "41000000-0000-4000-8000-000000000001";
export const PAGE_2 = "41000000-0000-4000-8000-000000000002";
const RUN_ID = "50000000-0000-4000-8000-000000000009";

export const REGION = { x: 0.1, y: 0.2, w: 0.3, h: 0.05 };

export function reviewerClaim(overrides: Partial<ClaimDetail> = {}): ClaimDetail {
  return claimDetail({
    status: "NEEDS_VERIFICATION",
    assignee: REVIEWER,
    assignedAt: "2026-10-14T01:20:00Z",
    counts: { fieldsToVerify: 3, blockingAlerts: 0, warningAlerts: 0, mappingsToConfirm: 0, openFlags: 0, keptFlags: 0 },
    allowedActions: ["EDIT_FIELDS"],
    ...overrides,
  });
}

/** A field the AI read with a level of Check: its value lives only in `ai.value` until a person accepts it. */
export function checkField(overrides: Partial<ClaimField> = {}): ClaimField {
  return {
    id: "60000000-0000-4000-8000-000000000001",
    documentId: INVOICE_ID,
    fieldKey: "invoice_total",
    itemNo: 0,
    expenseLineId: null,
    status: "NEEDS_VERIFICATION",
    required: true,
    valueKind: "MONEY",
    valueText: null,
    valueAmount: null,
    valueDate: null,
    sourcePageId: null,
    sourceBbox: null,
    ai: { value: "1,400,000", confidence: 0.62, level: "CHECK", pageId: PAGE_1, bbox: REGION, runId: RUN_ID },
    aiMarker: true,
    resolvedBy: null,
    resolvedAt: null,
    rowVersion: 1,
    ...overrides,
  };
}

/** AI read nothing here: level Missing, no value, no source. */
export function missingField(overrides: Partial<ClaimField> = {}): ClaimField {
  return checkField({
    id: "60000000-0000-4000-8000-000000000002",
    documentId: SUMMARY_ID,
    fieldKey: "diagnosis",
    status: "NEEDS_ENTRY",
    valueKind: "TEXT",
    ai: { value: null, confidence: 0.31, level: "MISSING", pageId: null, bbox: null, runId: RUN_ID },
    ...overrides,
  });
}

/** Taken as it stands because of its level: the AI marker stays until a person touches it. */
export function autoAcceptedField(overrides: Partial<ClaimField> = {}): ClaimField {
  return checkField({
    id: "60000000-0000-4000-8000-000000000003",
    fieldKey: "service_date",
    status: "AUTO_ACCEPTED",
    valueKind: "DATE",
    valueText: "12/10/2026",
    valueDate: "2026-10-12",
    sourcePageId: PAGE_2,
    sourceBbox: { x: 0.5, y: 0.1, w: 0.2, h: 0.04 },
    ai: {
      value: "12/10/2026",
      confidence: 0.97,
      level: "HIGH",
      pageId: PAGE_2,
      bbox: { x: 0.5, y: 0.1, w: 0.2, h: 0.04 },
      runId: RUN_ID,
    },
    ...overrides,
  });
}

/** The same field after a person resolved it. */
export function resolved(field: ClaimField, overrides: Partial<ClaimField> = {}): ClaimField {
  return {
    ...field,
    status: "CONFIRMED",
    aiMarker: false,
    resolvedBy: REVIEWER,
    resolvedAt: "2026-10-14T03:00:00Z",
    rowVersion: field.rowVersion + 1,
    ...overrides,
  };
}

export function progress(overrides: Partial<ClaimProgress> = {}): ClaimProgress {
  return {
    status: "NEEDS_VERIFICATION",
    isReturned: false,
    rowVersion: 4,
    fieldsToVerify: 2,
    blockingAlerts: 0,
    warningAlerts: 0,
    mappingsToConfirm: 0,
    currentRevisionNo: null,
    payableAmount: null,
    isRevisionStale: false,
    revisionCreated: false,
    ...overrides,
  };
}

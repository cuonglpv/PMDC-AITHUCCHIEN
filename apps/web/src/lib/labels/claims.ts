import type { ClaimStatus, ErrorCode, IntakeChannel, PolicyStatus, Priority, WarningCode } from "@/lib/api/types";

type Tone = "neutral" | "success" | "warning" | "info" | "danger";

export const CLAIM_STATUS_LABEL: Record<ClaimStatus, string> = {
  DRAFT: "Draft",
  PROCESSING: "Processing",
  NEEDS_VERIFICATION: "Needs verification",
  READY_FOR_REVIEW: "Ready for review",
  IN_REVIEW: "In review",
  PENDING_APPROVAL: "Pending approval",
  APPROVED: "Approved",
  PARTIALLY_APPROVED: "Partially approved",
  REJECTED: "Rejected",
};

/** docs/DESIGN.md, Status pill mapping. */
export const CLAIM_STATUS_TONE: Record<ClaimStatus, Tone> = {
  DRAFT: "neutral",
  PROCESSING: "info",
  NEEDS_VERIFICATION: "warning",
  READY_FOR_REVIEW: "info",
  IN_REVIEW: "info",
  PENDING_APPROVAL: "neutral",
  APPROVED: "success",
  PARTIALLY_APPROVED: "success",
  REJECTED: "danger",
};

export const POLICY_STATUS_LABEL: Record<PolicyStatus, string> = {
  ACTIVE: "Active",
  LAPSED: "Lapsed",
  TERMINATED: "Terminated",
};

export const POLICY_STATUS_TONE: Record<PolicyStatus, Tone> = {
  ACTIVE: "success",
  LAPSED: "warning",
  TERMINATED: "danger",
};

export const CHANNEL_LABEL: Record<IntakeChannel, string> = {
  COUNTER: "Counter",
  POST: "Post",
  EMAIL: "Email",
  PARTNER: "Partner",
};

export const CHANNELS = Object.keys(CHANNEL_LABEL) as IntakeChannel[];

/** Warnings that do not stop registration (US02). Others fall back to the message the API sends. */
export const WARNING_LABEL: Partial<Record<WarningCode, string>> = {
  COVERAGE_OUTSIDE_PERIOD:
    "The service date is outside the policy's coverage period. The claim is registered; only a Senior Approver can reject it later.",
  POLICY_NOT_ACTIVE: "The policy is not active. The claim is registered anyway.",
};

export const PRIORITY_LABEL: Record<Priority, string> = {
  HIGH: "High",
  NORMAL: "Normal",
};

/** The status pill of a claim. A returned claim stays `IN_REVIEW` but reads as Returned (DESIGN.md, Status pill mapping). */
export function claimStatusPill(claim: { status: ClaimStatus; isReturned: boolean }): { label: string; tone: Tone } {
  return claim.isReturned
    ? { label: "Returned", tone: "warning" }
    : { label: CLAIM_STATUS_LABEL[claim.status], tone: CLAIM_STATUS_TONE[claim.status] };
}

/** The action bar and the Evidence counter. */
export function fieldsToVerifyLabel(count: number): string {
  if (count === 0) {
    return "No fields to verify";
  }
  return count === 1 ? "1 field to verify" : `${count} fields to verify`;
}

/** Why "Start review" did not open a claim, by the code the API sends. */
export const START_REVIEW_PROBLEM_LABEL: Partial<Record<ErrorCode, string>> = {
  CLAIM_ALREADY_ASSIGNED: "Another reviewer took this claim first.",
  CLAIM_STATE_CONFLICT: "This claim has moved on and can no longer be started.",
};

export const START_REVIEW_FAILED = "The claim could not be started. Please try again.";

/** Top bar lookup. A claim that does not exist and one out of scope read the same, so a number reveals nothing. */
export const GO_TO_CLAIM_NOT_FOUND = "No claim with this number that you can open.";
export const GO_TO_CLAIM_FAILED = "The claim could not be looked up. Please try again.";

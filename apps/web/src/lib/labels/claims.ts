import type { ClaimStatus, IntakeChannel, PolicyStatus, WarningCode } from "@/lib/api/types";

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

import type { ConfidenceLevel, PageAiStatus } from "@/lib/api/types";

/** docs/DESIGN.md, Confidence badge. The API computes the level; the web only names it. */
export const CONFIDENCE_LEVEL_LABEL: Record<ConfidenceLevel, string> = {
  HIGH: "High",
  CHECK: "Check",
  MISSING: "Missing",
};

/** What a page card says about its AI page check (US04) while it has no result to show. */
export const PAGE_AI_STATUS_LABEL: Record<PageAiStatus, string> = {
  QUEUED: "Waiting for AI check",
  READING: "Waiting for AI check",
  DONE: "AI check done",
  FAILED: "AI check failed",
};

/** The page-type control on an Unclassified page (SCR-13). */
export const CHOOSE_PAGE_TYPE_LABEL = "Choose type";

/** The fallback banner on SCR-13: the AI could not help, and the work goes on by hand. */
export const INTAKE_FALLBACK_LABEL = {
  DRAFT: "AI check unavailable. The work continues by hand, and the claim can still be submitted.",
  SUBMITTED: "AI assistance unavailable. The fields will be entered by hand.",
} as const;

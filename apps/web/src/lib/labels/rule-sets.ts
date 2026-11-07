import type { components } from "@/lib/api/schema";

export type RuleType = components["schemas"]["RuleType"];
export type RuleSetStatus = components["schemas"]["RuleSetStatus"];
export type LimitScope = components["schemas"]["LimitScope"];

export const RULE_TYPE_LABEL: Record<RuleType, string> = {
  ELIGIBILITY: "Eligibility",
  COVERAGE: "Coverage",
  EXCLUSION: "Exclusion",
  WAITING_PERIOD: "Waiting period",
  COINSURANCE: "Reimbursement rate",
  LIMIT: "Limit",
};

export const RULE_SET_STATUS_LABEL: Record<RuleSetStatus, string> = {
  DRAFT: "Draft",
  PUBLISHED: "Published",
  DISCARDED: "Discarded",
};

export const LIMIT_SCOPE_LABEL: Record<LimitScope, string> = {
  PER_VISIT: "Per visit",
  ANNUAL: "Annual",
};

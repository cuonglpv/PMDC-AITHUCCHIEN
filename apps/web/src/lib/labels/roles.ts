import type { StaffRole } from "@/lib/api/types";

/** The API sends codes; the web app turns them into English labels (docs/07-api-contract.md). */
export const ROLE_LABEL: Record<StaffRole, string> = {
  INTAKE_OFFICER: "Intake Officer",
  CLAIMS_REVIEWER: "Claims Reviewer",
  SENIOR_APPROVER: "Senior Approver",
  ADMINISTRATOR: "Administrator",
};

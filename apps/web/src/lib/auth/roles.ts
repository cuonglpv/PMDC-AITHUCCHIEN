import type { StaffRole } from "@/lib/api/types";

/**
 * Where each role lands and what it sees. This is UX only: the API enforces every permission,
 * and its 403 is the real check (docs/architecture/c4/02-container.md, rule 2).
 */
export const ROLE_HOME: Record<StaffRole, string> = {
  INTAKE_OFFICER: "/intake",
  CLAIMS_REVIEWER: "/queue",
  SENIOR_APPROVER: "/approvals",
  ADMINISTRATOR: "/plans",
};

export type NavItem = {
  group: "Work" | "Policy" | "Insights";
  label: string;
  href: string;
  icon: string;
  roles: readonly StaffRole[];
  release?: "should";
};

/** docs/04-information-architecture.md, Navigation model. Items a role cannot use are not shown at all. */
export const NAV: readonly NavItem[] = [
  { group: "Work", label: "Intake", href: "/intake", icon: "post_add", roles: ["INTAKE_OFFICER"] },
  { group: "Work", label: "Review queue", href: "/queue", icon: "assignment", roles: ["CLAIMS_REVIEWER"] },
  { group: "Work", label: "Approval queue", href: "/approvals", icon: "task_alt", roles: ["SENIOR_APPROVER"] },
  {
    group: "Policy",
    label: "Benefit plans",
    href: "/plans",
    icon: "policy",
    roles: ["CLAIMS_REVIEWER", "SENIOR_APPROVER", "ADMINISTRATOR"],
  },
  {
    group: "Insights",
    label: "AI quality dashboard",
    href: "/insights/ai-quality",
    icon: "insights",
    roles: ["SENIOR_APPROVER"],
    release: "should",
  },
];

const includeShould = process.env.NEXT_PUBLIC_RELEASE_SHOULD === "true";

export function navFor(role: StaffRole): NavItem[] {
  return NAV.filter((item) => item.roles.includes(role) && (includeShould || item.release !== "should"));
}

const OPEN_TO: Record<string, readonly StaffRole[]> = {
  "/intake": ["INTAKE_OFFICER"],
  "/queue": ["CLAIMS_REVIEWER"],
  "/approvals": ["SENIOR_APPROVER"],
  "/claims": ["CLAIMS_REVIEWER", "SENIOR_APPROVER"],
  "/plans": ["CLAIMS_REVIEWER", "SENIOR_APPROVER", "ADMINISTRATOR"],
  "/insights": ["SENIOR_APPROVER"],
};

/** Whether to show the 403 page straight away, without asking the API. */
export function canOpen(role: StaffRole, pathname: string): boolean {
  const section = `/${pathname.split("/")[1] ?? ""}`;
  const roles = OPEN_TO[section];
  return roles === undefined || roles.includes(role);
}

/**
 * Where a claim opens for this role, and null for a role that never reads claims (BR-11). The top bar lookup is offered
 * only to roles with a path here.
 */
export function claimPathFor(role: StaffRole, claimId: string): string | null {
  switch (role) {
    case "INTAKE_OFFICER":
      return `/intake/${claimId}`;
    case "CLAIMS_REVIEWER":
    case "SENIOR_APPROVER":
      return `/claims/${claimId}`;
    default:
      return null;
  }
}

/** Only a path inside this app may be a return target, so a crafted link cannot send a user elsewhere. */
export function safeReturnPath(next: string | null): string | null {
  return next && next.startsWith("/") && !next.startsWith("//") && !next.startsWith("/\\") ? next : null;
}

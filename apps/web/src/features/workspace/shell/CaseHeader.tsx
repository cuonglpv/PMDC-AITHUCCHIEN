"use client";

import { StatusPill } from "@/components/ui/StatusPill";
import { formatDate } from "@/lib/format";
import { claimStatusPill } from "@/lib/labels/claims";
import { useWorkspace } from "./workspace-context";

/** One row: who and what the claim is, its status and who holds it. No buttons; the action lives in the sticky bar. */
export function CaseHeader() {
  const { claim } = useWorkspace();
  const pill = claimStatusPill(claim);
  return (
    <header aria-label="Claim" className="flex items-center justify-between gap-6 bg-surface px-6 py-3">
      <div className="flex min-w-0 items-baseline gap-3">
        <span className="font-mono text-[13px] font-medium whitespace-nowrap">{claim.claimNumber}</span>
        <h1 className="truncate text-base font-semibold">{claim.member.fullName}</h1>
        <p className="truncate text-[13px] text-on-surface-muted">
          Policy <span className="font-mono">{claim.policy.policyNumber}</span> · Service{" "}
          <span className="font-mono">{formatDate(claim.serviceDate)}</span>
        </p>
      </div>
      <div className="flex shrink-0 items-center gap-3">
        <span className="text-[13px] text-on-surface-muted">
          {claim.assignee ? `Assigned to ${claim.assignee.fullName}` : "Not assigned"}
        </span>
        <StatusPill tone={pill.tone}>{pill.label}</StatusPill>
      </div>
    </header>
  );
}

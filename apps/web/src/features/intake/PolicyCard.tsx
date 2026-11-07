"use client";

import type { ReactNode } from "react";
import { StatusPill } from "@/components/ui/StatusPill";
import { formatDate, formatMoney, formatPeriod } from "@/lib/format";
import { POLICY_STATUS_LABEL, POLICY_STATUS_TONE } from "@/lib/labels/claims";
import type { Selection } from "./new-claim/claim-form";
import { usePolicySummary } from "./queries";

/**
 * The member and policy being claimed against, with the remaining balance from `getPolicySummary`. The balance comes
 * from the rule-set version in force on the service date, so it follows the date the officer types.
 */
export function PolicyCard({
  selection,
  serviceDate,
  showBalance = true,
  children,
}: {
  selection: Selection;
  serviceDate?: string;
  showBalance?: boolean;
  children?: ReactNode;
}) {
  const summary = usePolicySummary(showBalance ? selection.policyId : undefined, serviceDate);
  const status = summary.data?.status ?? selection.policyStatus;

  return (
    <section aria-label="Member and policy" className="rounded-sm border border-line bg-surface p-4 text-[13px]">
      <h2 className="text-base font-semibold">{selection.fullName}</h2>
      <p className="mt-0.5 font-mono text-on-surface-muted">
        {selection.memberCode} · ID {selection.nationalIdMasked}
      </p>
      <dl className="mt-3 divide-y divide-line border-t border-line">
        <Row term="Plan">{selection.planName}</Row>
        <Row term="Policy">
          <span className="font-mono">{selection.policyNumber}</span>
        </Row>
        <Row term="Policy status">
          <StatusPill tone={POLICY_STATUS_TONE[status]}>{POLICY_STATUS_LABEL[status]}</StatusPill>
        </Row>
        <Row term="Coverage">
          <span className="font-mono">{formatPeriod(selection.coverageStart, selection.coverageEnd)}</span>
        </Row>
        {showBalance && (
          <Row term="Remaining balance">
            {summary.isPending ? (
              <span className="text-on-surface-muted">Loading…</span>
            ) : summary.isError ? (
              <span className="text-on-surface-muted">Not available</span>
            ) : summary.data.remainingBalance === null ? (
              <span className="text-on-surface-muted">No rule set in force on {formatDate(summary.data.asOfDate)}</span>
            ) : (
              <span className="text-right">
                <span className="block font-mono">{formatMoney(summary.data.remainingBalance)}</span>
                <span className="block text-xs text-on-surface-muted">
                  of {formatMoney(summary.data.annualLimit)}, as of {formatDate(summary.data.asOfDate)}
                </span>
              </span>
            )}
          </Row>
        )}
        {children}
      </dl>
    </section>
  );
}

export function Row({ term, children }: { term: string; children: ReactNode }) {
  return (
    <div className="flex items-start justify-between gap-4 py-2">
      <dt className="text-on-surface-muted">{term}</dt>
      <dd className="text-right">{children}</dd>
    </div>
  );
}

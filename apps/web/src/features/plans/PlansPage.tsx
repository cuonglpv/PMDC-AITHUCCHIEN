"use client";

import Link from "next/link";
import { LoadError, Loading } from "@/components/states/PageState";
import { StatusPill } from "@/components/ui/StatusPill";
import { formatDate } from "@/lib/format";
import { usePlans } from "./queries";

/** SCR-41: benefit plans with the version in force today and any open draft. */
export function PlansPage() {
  const plans = usePlans();

  if (plans.isPending) {
    return <Loading label="Loading benefit plans" />;
  }
  if (plans.isError) {
    return <LoadError error={plans.error} />;
  }

  return (
    <div className="p-6">
      <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-primary-text uppercase">Policy</p>
      <h1 className="mt-1 font-display text-xl font-medium tracking-[-0.01em] uppercase">Benefit plans</h1>

      {plans.data.items.length === 0 ? (
        <p className="mt-6 rounded-sm border border-line bg-surface p-6">No benefit plans yet.</p>
      ) : (
        <table className="mt-6 w-full border border-line bg-surface text-[13px]">
          <thead className="bg-background text-left font-mono text-[11px] tracking-[0.08em] text-on-surface-muted uppercase">
            <tr className="h-8">
              <th className="px-3 font-medium">Plan</th>
              <th className="px-3 font-medium">Name</th>
              <th className="px-3 font-medium">Version in force</th>
              <th className="px-3 font-medium">Draft</th>
            </tr>
          </thead>
          <tbody>
            {plans.data.items.map((plan) => (
              <tr key={plan.id} className="h-9 border-t border-line">
                <td className="px-3 font-mono">
                  <Link className="underline" href={`/plans/${plan.id}/versions`}>
                    {plan.code}
                  </Link>
                </td>
                <td className="px-3">{plan.name}</td>
                <td className="px-3">
                  {plan.currentVersion
                    ? `v${plan.currentVersion.versionNo} · from ${formatDate(plan.currentVersion.effectiveFrom)}`
                    : "None in force"}
                </td>
                <td className="px-3">{plan.draftVersionId ? <StatusPill tone="warning">Open draft</StatusPill> : "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

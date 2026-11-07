"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { EmptyState } from "@/components/states/EmptyState";
import { Button } from "@/components/ui/Button";
import { StatusPill } from "@/components/ui/StatusPill";
import type { MemberSearchResult } from "@/lib/api/types";
import { formatDate, formatMoney, formatPeriod } from "@/lib/format";
import { POLICY_STATUS_LABEL, POLICY_STATUS_TONE } from "@/lib/labels/claims";
import { PolicyCard } from "../PolicyCard";
import { useMemberSearch } from "../queries";
import { selectionFromSearch, type Selection } from "./claim-form";

const searchSchema = z.object({
  query: z.string().trim().min(2, "Type at least 2 characters.").max(120, "Use at most 120 characters."),
});

/**
 * SCR-11: find the member and the policy year to claim against. Each result shows the masked national ID, the policy
 * status, the coverage period and the remaining balance (US02). With no match there is nothing to continue with.
 */
export function MemberStep({
  selection,
  onCancel,
  onContinue,
}: {
  selection: Selection | null;
  onCancel: () => void;
  onContinue: (selection: Selection) => void;
}) {
  const search = useMemberSearch();
  const [chosen, setChosen] = useState<Selection | null>(selection);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<z.infer<typeof searchSchema>>({ resolver: zodResolver(searchSchema), defaultValues: { query: "" } });

  const onSearch = handleSubmit(({ query }) => search.mutate(query.trim()));

  return (
    <div className="mt-6 grid grid-cols-1 gap-6 xl:grid-cols-[minmax(0,1fr)_320px]">
      <div className="min-w-0">
        <form role="search" onSubmit={onSearch} noValidate className="max-w-xl">
          <label htmlFor="member-query" className="block text-[13px] font-medium">
            Member name, national ID or policy number
          </label>
          <div className="mt-1 flex gap-2">
            <input
              id="member-query"
              autoComplete="off"
              className="h-9 min-w-0 flex-1 rounded-sm border border-control-border bg-surface px-3 text-[13px] aria-invalid:border-danger"
              aria-invalid={errors.query ? true : undefined}
              aria-describedby={errors.query ? "member-query-error" : undefined}
              {...register("query")}
            />
            <Button type="submit" disabled={search.isPending}>
              <span className="material-symbols-outlined text-[18px]" aria-hidden="true">
                search
              </span>
              Search
            </Button>
          </div>
          {errors.query && (
            <p id="member-query-error" className="mt-1 text-xs text-danger">
              {errors.query.message}
            </p>
          )}
        </form>

        <div className="mt-5" aria-live="polite">
          {search.isPending && <p className="text-[13px] text-on-surface-muted">Searching…</p>}
          {search.isError && (
            <p role="alert" className="rounded-sm bg-danger-container px-3 py-2 text-[13px] text-danger">
              The search did not run. Please try again.
            </p>
          )}
          {search.isSuccess &&
            (search.data.items.length === 0 ? (
              <EmptyState
                icon="person_search"
                title="No member found"
                description="Check the spelling, the national ID or the policy number. A claim can only be registered for a member."
              />
            ) : (
              <Results
                items={search.data.items}
                chosenPolicyId={chosen?.policyId}
                onChoose={(result) => setChosen(selectionFromSearch(result))}
              />
            ))}
        </div>
      </div>

      <aside className="space-y-3">
        {chosen ? (
          <PolicyCard key={chosen.policyId} selection={chosen} />
        ) : (
          <p className="rounded-sm border border-line bg-surface p-4 text-[13px] text-on-surface-muted">
            Choose a member and policy to see the coverage and the remaining balance.
          </p>
        )}
      </aside>

      <div className="flex justify-end gap-2 xl:col-span-2">
        <Button onClick={onCancel}>Cancel</Button>
        <Button variant="primary" disabled={!chosen} onClick={() => chosen && onContinue(chosen)}>
          Continue
        </Button>
      </div>
    </div>
  );
}

function Results({
  items,
  chosenPolicyId,
  onChoose,
}: {
  items: MemberSearchResult[];
  chosenPolicyId: string | undefined;
  onChoose: (result: MemberSearchResult) => void;
}) {
  return (
    <table className="w-full border border-line bg-surface text-[13px]">
      <caption className="sr-only">Members found</caption>
      <thead className="bg-background text-left font-mono text-[11px] tracking-[0.08em] text-on-surface-muted uppercase">
        <tr className="h-8">
          <th className="w-10 px-3 font-medium">
            <span className="sr-only">Choose</span>
          </th>
          <th className="px-3 font-medium">Member</th>
          <th className="px-3 font-medium">National ID</th>
          <th className="px-3 font-medium">Policy</th>
          <th className="px-3 font-medium">Status</th>
          <th className="px-3 font-medium">Coverage</th>
          <th className="px-3 text-right font-medium">Remaining</th>
        </tr>
      </thead>
      <tbody>
        {items.map((result) => {
          const chosen = result.policy.policyId === chosenPolicyId;
          return (
            <tr
              key={`${result.memberId}-${result.policy.policyId}`}
              className={`h-11 cursor-pointer border-t border-line ${
                chosen ? "bg-row-selected shadow-[inset_2px_0_0_var(--color-primary)]" : "hover:bg-background"
              }`}
              onClick={() => onChoose(result)}
            >
              <td className="px-3">
                <input
                  type="radio"
                  name="member-policy"
                  checked={chosen}
                  onChange={() => onChoose(result)}
                  aria-label={`Choose ${result.fullName}, policy ${result.policy.policyNumber}`}
                />
              </td>
              <td className="px-3">
                <span className="block font-medium">{result.fullName}</span>
                <span className="block text-xs text-on-surface-muted">
                  {result.memberCode} · born {formatDate(result.dateOfBirth)}
                </span>
              </td>
              <td className="px-3 font-mono whitespace-nowrap">{result.nationalIdMasked}</td>
              <td className="px-3">
                <span className="block font-mono whitespace-nowrap">{result.policy.policyNumber}</span>
                <span className="block text-xs text-on-surface-muted">{result.policy.planName}</span>
              </td>
              <td className="px-3">
                <StatusPill tone={POLICY_STATUS_TONE[result.policy.status]}>
                  {POLICY_STATUS_LABEL[result.policy.status]}
                </StatusPill>
              </td>
              <td className="px-3 font-mono whitespace-nowrap">
                {formatPeriod(result.policy.coverageStart, result.policy.coverageEnd)}
              </td>
              <td className="px-3 text-right font-mono whitespace-nowrap">
                {formatMoney(result.policy.remainingBalance)}
              </td>
            </tr>
          );
        })}
      </tbody>
    </table>
  );
}

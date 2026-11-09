"use client";

import { Button } from "@/components/ui/Button";
import { useStartReview } from "@/lib/api/claim-queries";
import { ApiProblem } from "@/lib/api/problem";
import { fieldsToVerifyLabel, START_REVIEW_FAILED, START_REVIEW_PROBLEM_LABEL } from "@/lib/labels/claims";
import { useWorkspace } from "./workspace-context";

/**
 * The sticky action bar: progress on the left, and on the right the one primary action `allowedActions` opens. The API
 * decides what is allowed and what is left; this only shows it.
 */
export function ActionBar() {
  const { claim, canEdit, mustStartReview, verifyNextRef } = useWorkspace();
  const start = useStartReview();
  const toVerify = claim.counts?.fieldsToVerify;
  const startProblem =
    start.error instanceof ApiProblem && start.error.code ? START_REVIEW_PROBLEM_LABEL[start.error.code] : undefined;

  return (
    <section
      aria-label="Progress and actions"
      className="flex h-16 shrink-0 items-center justify-between gap-4 border-t border-line bg-surface px-6"
    >
      <div className="min-w-0 text-[13px]">
        {toVerify !== undefined && <p className="font-semibold">{fieldsToVerifyLabel(toVerify)}</p>}
        {claim.blockers.map((blocker) => (
          <p key={blocker.code} className="truncate text-xs text-on-surface-muted" title={blocker.message}>
            {blocker.message}
          </p>
        ))}
      </div>
      <div className="flex shrink-0 items-center gap-3">
        {start.isError && (
          <p role="alert" className="text-[13px] text-danger">
            {startProblem ?? START_REVIEW_FAILED}
          </p>
        )}
        {mustStartReview ? (
          <Button variant="primary" disabled={start.isPending} onClick={() => start.mutate(claim.id)}>
            Start review
          </Button>
        ) : (
          canEdit &&
          Boolean(toVerify) && (
            <Button variant="primary" aria-keyshortcuts="N" onClick={() => verifyNextRef.current?.()}>
              Verify next field
              <span className="font-mono text-[11px]" aria-hidden="true">
                N
              </span>
            </Button>
          )
        )}
      </div>
    </section>
  );
}

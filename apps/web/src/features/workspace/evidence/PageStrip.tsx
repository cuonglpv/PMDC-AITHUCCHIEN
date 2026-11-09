"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/Button";
import { PageTypeSelect } from "@/components/ui/PageTypeSelect";
import { ApiProblem } from "@/lib/api/problem";
import type { DocumentPage } from "@/lib/api/types";
import { PAGE_TYPE_LABEL, PAGE_TYPE_NOT_SAVED } from "@/lib/labels/documents";
import { EVIDENCE_PROBLEM_LABEL, RERUN_NOT_STARTED } from "@/lib/labels/fields";
import { useWorkspace } from "../shell/workspace-context";
import { isRereadActive, reloadAfterReread, useRerunExtraction, useReviewerReclassify } from "../queries";

/**
 * What can be done to the page in the viewer: change a misread type, and "Re-run AI on this page", which never touches
 * the rest of the claim (docs/04-information-architecture.md, Re-run scope). The poll follows the run, and once it ends
 * the fields reload, so the reviewer sees what it changed.
 */
export function PageStrip({ page }: { page: DocumentPage }) {
  const { claimId, canEdit, snapshot } = useWorkspace();
  const queryClient = useQueryClient();
  const rerun = useRerunExtraction(claimId);
  const reclassify = useReviewerReclassify(claimId);
  const [watched, setWatched] = useState<{ runId: string; pageNo: number; requestedAt: string } | null>(null);

  const run = watched ? snapshot?.aiRuns.find((item) => item.id === watched.runId) : undefined;
  const anyActive = snapshot?.aiRuns.some(isRereadActive) ?? false;
  // The poll lists only the latest run of each page, so a newer run can replace this one. A run simply not listed yet
  // has not ended: the poll in hand may be older than the request.
  const replaced =
    watched !== null &&
    !run &&
    (snapshot?.aiRuns.some(
      (item) => item.task === "PAGE_REREAD" && Date.parse(item.requestedAt) > Date.parse(watched.requestedAt),
    ) ??
      false);
  const ended = watched !== null && (run ? !isRereadActive(run) : replaced);
  const running = anyActive || rerun.isPending;

  const reloaded = useRef<string | null>(null);
  useEffect(() => {
    if (ended && watched && reloaded.current !== watched.runId) {
      reloaded.current = watched.runId;
      reloadAfterReread(queryClient, claimId);
    }
  }, [ended, watched, claimId, queryClient]);

  const rerunProblem =
    rerun.error instanceof ApiProblem && rerun.error.code ? EVIDENCE_PROBLEM_LABEL[rerun.error.code] : undefined;
  const typeProblem =
    reclassify.error instanceof ApiProblem && reclassify.error.code
      ? EVIDENCE_PROBLEM_LABEL[reclassify.error.code]
      : undefined;

  return (
    <div className="border-b border-line bg-surface px-4 py-2">
      <div className="flex flex-wrap items-center gap-3">
        <p className="text-[13px]">
          <span className="font-mono">p{page.claimPageNo}</span> {PAGE_TYPE_LABEL[page.pageType]}
        </p>
        {canEdit && (
          <div className="ml-auto flex items-center gap-2">
            {page.pageType !== "UNCLASSIFIED" && (
              <div className="w-32">
                <PageTypeSelect
                  tone="neutral"
                  label={`Change type of page ${page.claimPageNo}`}
                  placeholder="Change type"
                  pendingType={reclassify.isPending ? reclassify.variables.pageType : undefined}
                  onChoose={(pageType) => reclassify.mutate({ pageId: page.id, pageType })}
                />
              </div>
            )}
            <Button
              className="h-7 px-3 text-xs"
              disabled={running}
              onClick={() =>
                rerun.mutate(page.id, {
                  onSuccess: (ref) => setWatched({ runId: ref.id, pageNo: page.claimPageNo, requestedAt: ref.requestedAt }),
                })
              }
            >
              <span className="material-symbols-outlined text-[15px]" aria-hidden="true">
                refresh
              </span>
              Re-run AI on this page
            </Button>
          </div>
        )}
      </div>
      {running && !rerun.isPending && (
        <p role="status" className="mt-1 text-xs text-info">
          AI is reading the page again. Fields a person resolved keep their values.
        </p>
      )}
      {ended && run?.status === "SUCCEEDED" && (
        <p role="status" className="mt-1 text-xs text-success">
          AI read p{watched.pageNo} again. Fields a person resolved kept their values.
        </p>
      )}
      {ended && run?.status === "FAILED" && (
        <p role="status" className="mt-1 text-xs text-warning">
          AI could not read p{watched.pageNo}. Enter its fields by hand.
        </p>
      )}
      {rerun.isError && (
        <p role="alert" className="mt-1 text-xs text-danger">
          {rerunProblem ?? RERUN_NOT_STARTED}
        </p>
      )}
      {reclassify.isError && (
        <p role="alert" className="mt-1 text-xs text-danger">
          {typeProblem ?? PAGE_TYPE_NOT_SAVED}
        </p>
      )}
    </div>
  );
}

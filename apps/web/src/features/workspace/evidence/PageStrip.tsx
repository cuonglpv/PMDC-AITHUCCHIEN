"use client";

import { Button } from "@/components/ui/Button";
import { PageTypeSelect } from "@/components/ui/PageTypeSelect";
import { ApiProblem } from "@/lib/api/problem";
import type { DocumentPage } from "@/lib/api/types";
import { PAGE_TYPE_LABEL, PAGE_TYPE_NOT_SAVED } from "@/lib/labels/documents";
import { EVIDENCE_PROBLEM_LABEL, RERUN_NOT_STARTED } from "@/lib/labels/fields";
import { useWorkspace } from "../shell/workspace-context";
import { useRerunExtraction, useReviewerReclassify } from "../queries";

/**
 * What can be done to the page in the viewer: change a misread type, and "Re-run AI on this page", which never touches
 * the rest of the claim (docs/04-information-architecture.md, Re-run scope). The workspace follows the run, and what it
 * says here names the page it was about, because the viewer may be on another one by then.
 */
export function PageStrip({ page }: { page: DocumentPage }) {
  const { claimId, canEdit, reread, followReread } = useWorkspace();
  const rerun = useRerunExtraction(claimId, followReread);
  const reclassify = useReviewerReclassify(claimId);
  const running = reread.running || rerun.isPending;
  const rereadPage = reread.pageNo === null ? "the page" : `p${reread.pageNo}`;

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
              onClick={() => rerun.mutate({ pageId: page.id, pageNo: page.claimPageNo })}
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
          AI is reading {rereadPage} again. Fields a person resolved keep their values.
        </p>
      )}
      {reread.outcome === "SUCCEEDED" && (
        <p role="status" className="mt-1 text-xs text-success">
          AI read {rereadPage} again. Fields a person resolved kept their values.
        </p>
      )}
      {reread.outcome === "FAILED" && (
        <p role="status" className="mt-1 text-xs text-warning">
          AI could not read {rereadPage}. Enter its fields by hand.
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

"use client";

import { AiMarker } from "@/components/ai/AiMarker";
import { ConfidenceBadge } from "@/components/ai/ConfidenceBadge";
import { PersonMarker } from "@/components/ai/PersonMarker";
import { PageTypeSelect } from "@/components/ui/PageTypeSelect";
import { ApiProblem } from "@/lib/api/problem";
import type { Document, DocumentPage, PageType } from "@/lib/api/types";
import { CHOOSE_PAGE_TYPE_LABEL, PAGE_AI_RAIL_LABEL } from "@/lib/labels/ai";
import { PAGE_QUALITY_LABEL, PAGE_TYPE_LABEL, PAGE_TYPE_NOT_SAVED } from "@/lib/labels/documents";
import { EVIDENCE_PROBLEM_LABEL } from "@/lib/labels/fields";
import { useReviewerReclassify } from "../queries";
import { PageImage } from "../viewer/PageImage";
import { useWorkspace } from "./workspace-context";

/** A document is the type of its first classified page (docs/03-use-cases.md, Field catalogue). */
export function documentType(document: Document): PageType {
  return document.pages.find((page) => page.pageType !== "UNCLASSIFIED")?.pageType ?? "UNCLASSIFIED";
}

/** Zone A: every page by document, with its type, quality and AI status. Selecting one moves zone C there. */
export function DocumentRail() {
  const { documents, documentsLoading, mustStartReview, pages } = useWorkspace();
  return (
    <aside aria-label="Documents" className="relative w-[200px] shrink-0 overflow-y-auto border-r border-line bg-surface p-3">
      {mustStartReview ? (
        <p className="text-xs text-on-surface-muted">Documents appear after you start review.</p>
      ) : documentsLoading ? (
        <div role="status" aria-busy="true" aria-label="Loading documents" className="h-24 animate-pulse rounded-sm bg-surface-sunken" />
      ) : (
        <>
          <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-on-surface-muted uppercase">
            Pages ({pages.length})
          </p>
          <ul className="mt-3 space-y-4">
            {documents.map((document) => (
              <li key={document.id}>
                <p className="text-[13px] font-semibold">{PAGE_TYPE_LABEL[documentType(document)]}</p>
                <p className="truncate text-xs text-on-surface-muted" title={document.originalFilename}>
                  {document.originalFilename}
                </p>
                <ul className="mt-2 space-y-2">
                  {document.pages.map((page) => (
                    <RailPage key={page.id} page={page} fileName={document.originalFilename} />
                  ))}
                </ul>
              </li>
            ))}
          </ul>
        </>
      )}
    </aside>
  );
}

function RailPage({ page, fileName }: { page: DocumentPage; fileName: string }) {
  const { claimId, canEdit, viewedPage, viewPage } = useWorkspace();
  const name = `p${page.claimPageNo}`;
  const active = viewedPage?.id === page.id;
  const setBy = page.pageTypeSource === "USER" ? page.pageTypeSetBy : null;
  const suggestion = page.pageTypeSource === "USER" ? null : page.ai;
  const unclassified = page.pageType === "UNCLASSIFIED";
  const waiting = page.aiStatus === "QUEUED" || page.aiStatus === "READING";

  return (
    <li aria-label={`Page ${page.claimPageNo}`} className="space-y-1">
      <button
        type="button"
        aria-current={active ? "true" : undefined}
        onClick={() => viewPage(page.id)}
        className={`flex w-full gap-2 rounded-sm p-1 text-left transition-colors duration-150 hover:bg-background ${
          active ? "outline-2 -outline-offset-2 outline-ink" : ""
        }`}
      >
        <span className="block h-14 w-10 shrink-0 border border-line bg-background">
          <PageImage
            claimId={claimId}
            pageId={page.id}
            variant="thumbnail"
            alt={`${name}, page ${page.pageNo} of ${fileName}`}
            className="h-full w-full object-contain"
          />
        </span>
        <span className="min-w-0 flex-1 space-y-0.5">
          <span className="block text-xs font-medium">
            <span className="font-mono">{name}</span> {unclassified && waiting ? "Type not set" : PAGE_TYPE_LABEL[page.pageType]}
          </span>
          <span className="block text-[11px] leading-4 text-on-surface-muted">{PAGE_QUALITY_LABEL[page.quality]}</span>
          <span className="block text-[11px] leading-4 text-on-surface-muted">{PAGE_AI_RAIL_LABEL[page.aiStatus]}</span>
        </span>
      </button>
      {(setBy || suggestion) && (
        <div className="flex flex-wrap items-center gap-1 pl-1">
          {setBy ? (
            <PersonMarker person={setBy} />
          ) : (
            suggestion && (
              <>
                <AiMarker />
                <ConfidenceBadge level={suggestion.level} confidence={suggestion.confidence} />
              </>
            )
          )}
        </div>
      )}
      {unclassified && !waiting && canEdit && <ChooseType page={page} />}
    </li>
  );
}

function ChooseType({ page }: { page: DocumentPage }) {
  const { claimId } = useWorkspace();
  const reclassify = useReviewerReclassify(claimId);
  const problem =
    reclassify.error instanceof ApiProblem && reclassify.error.code
      ? EVIDENCE_PROBLEM_LABEL[reclassify.error.code]
      : undefined;
  return (
    <PageTypeSelect
      label={`${CHOOSE_PAGE_TYPE_LABEL} for page ${page.claimPageNo}`}
      placeholder={CHOOSE_PAGE_TYPE_LABEL}
      pendingType={reclassify.isPending ? reclassify.variables.pageType : undefined}
      error={reclassify.isError ? (problem ?? PAGE_TYPE_NOT_SAVED) : undefined}
      onChoose={(pageType) => reclassify.mutate({ pageId: page.id, pageType })}
    />
  );
}

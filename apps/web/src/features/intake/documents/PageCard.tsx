"use client";

import { useId } from "react";
import { AiMarker } from "@/components/ai/AiMarker";
import { ConfidenceBadge } from "@/components/ai/ConfidenceBadge";
import { PersonMarker } from "@/components/ai/PersonMarker";
import { ApiProblem } from "@/lib/api/problem";
import type { ChosenPageType, DocumentPage } from "@/lib/api/types";
import { CHOOSE_PAGE_TYPE_LABEL, PAGE_AI_STATUS_LABEL } from "@/lib/labels/ai";
import { CHOSEN_PAGE_TYPES, PAGE_QUALITY_LABEL, PAGE_TYPE_LABEL, PAGE_TYPE_NOT_SAVED } from "@/lib/labels/documents";
import { isWaitingForAi } from "../processing";
import { useReclassifyPage } from "../queries";
import { problemMessage } from "./FileRows";
import { PageThumbnail } from "./PageThumbnail";

/**
 * One page of the claim (p1 to p30) with its thumbnail and what the AI page check said about it (US04): the type with
 * the AI marker and the level the API computed, or the officer's initials once they chose the type. An Unclassified
 * page offers "Choose type" while the officer may still change the claim (`canChooseType`).
 */
export function PageCard({
  claimId,
  page,
  fileName,
  canChooseType,
}: {
  claimId: string;
  page: DocumentPage;
  fileName: string;
  canChooseType: boolean;
}) {
  const name = `p${page.claimPageNo}`;
  const setBy = page.pageTypeSource === "USER" ? page.pageTypeSetBy : null;
  // What the AI said stands until a person sets the type; the API sends the level, the web only shows it.
  const suggestion = page.pageTypeSource === "USER" ? null : page.ai;
  const fromAi = page.pageTypeSource !== "USER" && (page.pageTypeSource === "AI" || page.ai !== null);
  const waiting = isWaitingForAi(page);
  const unclassified = page.pageType === "UNCLASSIFIED";

  return (
    <li className="flex flex-col gap-2 rounded-sm border border-line bg-surface p-3" aria-label={`Page ${page.claimPageNo}`}>
      <div className="flex h-5 items-center justify-between">
        <span className="font-mono text-xs text-on-surface-muted">{name}</span>
        {setBy ? <PersonMarker person={setBy} /> : fromAi && <AiMarker />}
      </div>
      <PageThumbnail claimId={claimId} pageId={page.id} label={`${name}, page ${page.pageNo} of ${fileName}`} />
      <div className="flex flex-wrap items-center gap-1.5">
        <p className="text-[13px] font-medium">
          {!unclassified ? (
            PAGE_TYPE_LABEL[page.pageType]
          ) : (
            <span className="text-on-surface-muted">{waiting ? "Type not set" : PAGE_TYPE_LABEL.UNCLASSIFIED}</span>
          )}
        </p>
        {suggestion && <ConfidenceBadge level={suggestion.level} confidence={suggestion.confidence} />}
      </div>
      {unclassified && !waiting && canChooseType && <ChooseType claimId={claimId} page={page} />}
      <PageCheck page={page} />
    </li>
  );
}

/** A labelled native select, so it works with the keyboard and reads well to a screen reader. */
function ChooseType({ claimId, page }: { claimId: string; page: DocumentPage }) {
  const id = useId();
  const reclassify = useReclassifyPage(claimId);
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="sr-only">
        {`${CHOOSE_PAGE_TYPE_LABEL} for page ${page.claimPageNo}`}
      </label>
      <div className="relative">
        <select
          id={id}
          value={reclassify.isPending ? reclassify.variables.pageType : ""}
          disabled={reclassify.isPending}
          aria-describedby={reclassify.isError ? `${id}-error` : undefined}
          onChange={(event) => {
            if (event.target.value) {
              reclassify.mutate({ pageId: page.id, pageType: event.target.value as ChosenPageType });
            }
          }}
          className="min-h-7 w-full cursor-pointer appearance-none truncate rounded-sm border border-warning/40 bg-warning-container py-1 pr-6 pl-2 text-xs font-medium text-warning disabled:cursor-wait disabled:opacity-70"
        >
          <option value="" disabled>
            {CHOOSE_PAGE_TYPE_LABEL}
          </option>
          {CHOSEN_PAGE_TYPES.map((type) => (
            <option key={type} value={type}>
              {PAGE_TYPE_LABEL[type]}
            </option>
          ))}
        </select>
        <span
          className="material-symbols-outlined pointer-events-none absolute top-1/2 right-1 -translate-y-1/2 text-[14px] text-warning"
          aria-hidden="true"
        >
          expand_more
        </span>
      </div>
      {reclassify.isError && (
        <p id={`${id}-error`} role="alert" className="text-xs text-danger">
          {problemMessage(
            reclassify.error instanceof ApiProblem ? reclassify.error : new ApiProblem(0, null),
            PAGE_TYPE_NOT_SAVED,
          )}
        </p>
      )}
    </div>
  );
}

function PageCheck({ page }: { page: DocumentPage }) {
  if (isWaitingForAi(page)) {
    return (
      <p className="flex items-center gap-1 text-xs text-info">
        <span className="material-symbols-outlined text-[14px]" aria-hidden="true">
          hourglass_top
        </span>
        {PAGE_AI_STATUS_LABEL[page.aiStatus]}
      </p>
    );
  }
  if (page.aiStatus === "FAILED") {
    return (
      <p className="flex items-center gap-1 text-xs text-on-surface-muted">
        <span className="material-symbols-outlined text-[14px]" aria-hidden="true">
          error
        </span>
        {PAGE_AI_STATUS_LABEL.FAILED}
      </p>
    );
  }
  if (page.quality === "LOW_QUALITY") {
    return (
      <p className="flex items-start gap-1 rounded-sm bg-warning-container px-2 py-1 text-xs text-warning">
        <span className="material-symbols-outlined text-[14px]" aria-hidden="true">
          warning
        </span>
        {PAGE_QUALITY_LABEL.LOW_QUALITY}
      </p>
    );
  }
  if (page.quality === "READABLE") {
    return <p className="text-xs font-medium text-success">{PAGE_QUALITY_LABEL.READABLE}</p>;
  }
  return <p className="text-xs text-on-surface-muted">{PAGE_QUALITY_LABEL.NOT_CHECKED}</p>;
}

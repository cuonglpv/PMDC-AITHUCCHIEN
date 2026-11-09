"use client";

import { AiMarker } from "@/components/ai/AiMarker";
import { ConfidenceBadge } from "@/components/ai/ConfidenceBadge";
import { PersonMarker } from "@/components/ai/PersonMarker";
import { PageTypeSelect } from "@/components/ui/PageTypeSelect";
import { ApiProblem } from "@/lib/api/problem";
import type { DocumentPage } from "@/lib/api/types";
import { CHOOSE_PAGE_TYPE_LABEL, PAGE_AI_STATUS_LABEL } from "@/lib/labels/ai";
import { PAGE_QUALITY_LABEL, PAGE_TYPE_LABEL, PAGE_TYPE_NOT_SAVED } from "@/lib/labels/documents";
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

function ChooseType({ claimId, page }: { claimId: string; page: DocumentPage }) {
  const reclassify = useReclassifyPage(claimId);
  return (
    <PageTypeSelect
      label={`${CHOOSE_PAGE_TYPE_LABEL} for page ${page.claimPageNo}`}
      placeholder={CHOOSE_PAGE_TYPE_LABEL}
      pendingType={reclassify.isPending ? reclassify.variables.pageType : undefined}
      error={
        reclassify.isError
          ? problemMessage(
              reclassify.error instanceof ApiProblem ? reclassify.error : new ApiProblem(0, null),
              PAGE_TYPE_NOT_SAVED,
            )
          : undefined
      }
      onChoose={(pageType) => reclassify.mutate({ pageId: page.id, pageType })}
    />
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

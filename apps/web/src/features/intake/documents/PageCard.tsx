"use client";

import { AiMarker } from "@/components/ai/AiMarker";
import type { DocumentPage } from "@/lib/api/types";
import { PAGE_QUALITY_LABEL, PAGE_TYPE_LABEL } from "@/lib/labels/documents";
import { isWaitingForAi } from "../processing";
import { PageThumbnail } from "./PageThumbnail";

/** One page of the claim (p1 to p30) with its thumbnail and what the AI page check said about it. */
export function PageCard({ claimId, page, fileName }: { claimId: string; page: DocumentPage; fileName: string }) {
  const name = `p${page.claimPageNo}`;
  return (
    <li className="flex flex-col gap-2 rounded-sm border border-line bg-surface p-3" aria-label={`Page ${page.claimPageNo}`}>
      <div className="flex h-5 items-center justify-between">
        <span className="font-mono text-xs text-on-surface-muted">{name}</span>
        {page.pageTypeSource === "AI" && page.pageType !== "UNCLASSIFIED" && <AiMarker />}
      </div>
      <PageThumbnail claimId={claimId} pageId={page.id} label={`${name}, page ${page.pageNo} of ${fileName}`} />
      <p className="text-[13px] font-medium">
        {page.pageType === "UNCLASSIFIED" ? (
          <span className="text-on-surface-muted">Type not set</span>
        ) : (
          PAGE_TYPE_LABEL[page.pageType]
        )}
      </p>
      <PageCheck page={page} />
    </li>
  );
}

function PageCheck({ page }: { page: DocumentPage }) {
  if (isWaitingForAi(page)) {
    return (
      <p className="flex items-center gap-1 text-xs text-info">
        <span className="material-symbols-outlined text-[14px]" aria-hidden="true">
          hourglass_top
        </span>
        Waiting for AI check
      </p>
    );
  }
  if (page.aiStatus === "FAILED") {
    return <p className="text-xs text-on-surface-muted">AI check not run</p>;
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

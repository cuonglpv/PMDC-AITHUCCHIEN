"use client";

import { useState } from "react";
import { Button } from "@/components/ui/Button";
import type { Bbox } from "@/lib/api/types";
import { PAGE_TYPE_LABEL } from "@/lib/labels/documents";
import { PageImage } from "../viewer/PageImage";
import { RegionDraw } from "../viewer/RegionDraw";
import { RegionOverlay } from "../viewer/RegionOverlay";
import { useWorkspace } from "./workspace-context";

const ZOOMS = [100, 125, 150, 200] as const;

/**
 * Zone C: the page with the selected source region. It keeps 480 px from 1440 px up and narrows to 400 px at the 1280 px
 * minimum, so zone B has the room its badges and buttons need (docs/07-api-contract.md notes the old cramped layout).
 */
export function ContextRail() {
  const { claimId, pages, viewedPage, viewPage, highlight, drawing, stopDrawing } = useWorkspace();
  const [zoomIndex, setZoomIndex] = useState(0);
  const zoom = ZOOMS[zoomIndex];
  const position = viewedPage ? pages.indexOf(viewedPage) : -1;
  const previous = pages[position - 1];
  const next = pages[position + 1];
  const shown = highlight && viewedPage && highlight.pageId === viewedPage.id ? highlight : null;

  const finishDrawing = (bbox: Bbox) => {
    if (drawing && viewedPage) {
      stopDrawing();
      drawing.onDrawn({ pageId: viewedPage.id, bbox });
    }
  };

  return (
    <aside
      aria-label="Page viewer"
      className="flex w-[400px] shrink-0 flex-col border-l border-line bg-surface-sunken min-[1440px]:w-[480px]"
    >
      <div className="flex h-10 shrink-0 items-center justify-between gap-2 border-b border-line bg-surface px-3">
        <span className="min-w-0 truncate text-xs font-medium">
          {viewedPage ? PAGE_TYPE_LABEL[viewedPage.pageType] : "No page"}
        </span>
        {viewedPage && (
          <span className="flex shrink-0 items-center gap-2">
            <span className="flex items-center">
              <IconButton
                icon="chevron_left"
                label="Previous page"
                disabled={!previous}
                onClick={() => previous && viewPage(previous.id)}
              />
              <span className="px-1 font-mono text-xs" aria-live="polite">
                p{viewedPage.claimPageNo} / {pages.length}
              </span>
              <IconButton
                icon="chevron_right"
                label="Next page"
                disabled={!next}
                onClick={() => next && viewPage(next.id)}
              />
            </span>
            <span className="flex items-center">
              <IconButton icon="remove" label="Zoom out" disabled={zoomIndex === 0} onClick={() => setZoomIndex(zoomIndex - 1)} />
              <span className="w-10 text-center font-mono text-xs">{zoom}%</span>
              <IconButton
                icon="add"
                label="Zoom in"
                disabled={zoomIndex === ZOOMS.length - 1}
                onClick={() => setZoomIndex(zoomIndex + 1)}
              />
            </span>
          </span>
        )}
      </div>

      {drawing && (
        <div className="flex shrink-0 items-center justify-between gap-2 border-b border-line bg-surface px-3 py-2 text-[13px]">
          <span className="min-w-0">
            Drag over the page to mark where <strong className="font-semibold">{drawing.fieldName}</strong> came from.
          </span>
          <Button variant="ghost" className="h-8 px-2" onClick={stopDrawing}>
            Cancel
          </Button>
        </div>
      )}

      <div className="relative min-h-0 flex-1 overflow-auto p-4 motion-safe:scroll-smooth">
        {viewedPage && (
          <div
            className="relative mx-auto bg-surface"
            style={{ width: `${zoom}%`, aspectRatio: `${viewedPage.widthPx} / ${viewedPage.heightPx}` }}
          >
            <PageImage
              key={viewedPage.id}
              claimId={claimId}
              pageId={viewedPage.id}
              variant="full"
              alt={`Page ${viewedPage.claimPageNo}, ${PAGE_TYPE_LABEL[viewedPage.pageType]}`}
              className="absolute inset-0 h-full w-full"
            />
            {shown && !drawing && <RegionOverlay bbox={shown.bbox} label={shown.label} tone={shown.tone} />}
            {drawing && (
              <RegionDraw key={`draw-${viewedPage.id}`} fieldName={drawing.fieldName} onDraw={finishDrawing} onCancel={stopDrawing} />
            )}
          </div>
        )}
      </div>
    </aside>
  );
}

function IconButton({
  icon,
  label,
  disabled,
  onClick,
}: {
  icon: string;
  label: string;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
      className="flex size-7 items-center justify-center rounded-sm text-ink transition-colors duration-150 hover:bg-background disabled:cursor-not-allowed disabled:opacity-30"
    >
      <span className="material-symbols-outlined text-[16px]" aria-hidden="true">
        {icon}
      </span>
    </button>
  );
}

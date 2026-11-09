"use client";

import { useEffect, useRef } from "react";
import type { Bbox } from "@/lib/api/types";

export type RegionTone = "ai" | "person";

const LOOK: Record<RegionTone, { rect: string; tag: string }> = {
  // DESIGN.md source highlight: violet is only for what the AI produced.
  ai: { rect: "fill-source-highlight-fill stroke-source-highlight", tag: "bg-source-highlight text-on-primary" },
  person: { rect: "fill-ink/10 stroke-ink", tag: "bg-ink text-on-primary" },
};

/**
 * The source region over a page image: an SVG in page coordinates (0 to 1), so it stays on the same spot at any zoom. It
 * scrolls into view when it moves, smoothly unless the person asked for reduced motion (the scroll area sets that).
 */
export function RegionOverlay({ bbox, label, tone }: { bbox: Bbox; label: string; tone: RegionTone }) {
  const anchor = useRef<HTMLDivElement>(null);
  const { x, y, w, h } = bbox;

  useEffect(() => {
    anchor.current?.scrollIntoView?.({ block: "center", inline: "nearest" });
  }, [x, y, w, h]);

  return (
    <div className="pointer-events-none absolute inset-0">
      <svg
        role="img"
        aria-label={`Source region: ${label}`}
        viewBox="0 0 1 1"
        preserveAspectRatio="none"
        className="absolute inset-0 h-full w-full"
      >
        <rect x={x} y={y} width={w} height={h} strokeWidth={2} vectorEffect="non-scaling-stroke" className={LOOK[tone].rect} />
      </svg>
      <div ref={anchor} className="absolute" style={{ left: `${x * 100}%`, top: `${(y + h / 2) * 100}%` }} />
      <span
        className={`absolute -translate-y-full px-1.5 font-mono text-[11px] leading-4 whitespace-nowrap ${LOOK[tone].tag}`}
        style={{ left: `${x * 100}%`, top: `${y * 100}%` }}
      >
        {label}
      </span>
    </div>
  );
}

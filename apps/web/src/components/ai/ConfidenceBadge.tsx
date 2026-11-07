import type { ConfidenceLevel } from "@/lib/api/types";
import { CONFIDENCE_LEVEL_LABEL } from "@/lib/labels/ai";

const LOOK: Record<ConfidenceLevel, { icon: string; className: string }> = {
  HIGH: { icon: "check_circle", className: "bg-success-container text-confidence-high" },
  CHECK: { icon: "visibility", className: "bg-warning-container text-confidence-check" },
  MISSING: { icon: "radio_button_unchecked", className: "bg-danger-container text-confidence-missing" },
};

/**
 * DESIGN.md confidence badge: icon and word, never colour alone, with the raw score in the tooltip. The level is the
 * one the API computed against BR-03; the web never compares a score with a threshold.
 */
export function ConfidenceBadge({ level, confidence }: { level: ConfidenceLevel; confidence?: number | null }) {
  const { icon, className } = LOOK[level];
  const score = confidence === undefined || confidence === null ? undefined : `Confidence ${confidence.toFixed(2)}`;
  return (
    <span
      title={score}
      className={`inline-flex h-[22px] items-center gap-1 rounded-sm px-2 text-[13px] font-medium ${className}`}
    >
      <span className="material-symbols-outlined text-[14px]" aria-hidden="true">
        {icon}
      </span>
      <span className="sr-only">Confidence: </span>
      <span>{CONFIDENCE_LEVEL_LABEL[level]}</span>
    </span>
  );
}

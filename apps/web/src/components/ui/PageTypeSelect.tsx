"use client";

import { useId } from "react";
import type { ChosenPageType } from "@/lib/api/types";
import { CHOSEN_PAGE_TYPES, PAGE_TYPE_LABEL } from "@/lib/labels/documents";

/**
 * The page type menu: "Choose type" on an Unclassified page (SCR-13, SCR-31), "Change type" on a misread one (SCR-31).
 * A labelled native select, so it works with the keyboard and reads well to a screen reader. The caller owns the request.
 */
export function PageTypeSelect({
  label,
  placeholder,
  pendingType,
  error,
  tone = "warning",
  onChoose,
}: {
  /** The accessible name, which says which page this is for. */
  label: string;
  placeholder: string;
  pendingType?: ChosenPageType;
  error?: string;
  tone?: "warning" | "neutral";
  onChoose: (pageType: ChosenPageType) => void;
}) {
  const id = useId();
  const look =
    tone === "warning"
      ? "border-warning/40 bg-warning-container text-warning"
      : "border-control-border bg-surface text-ink";
  return (
    <div className="flex flex-col gap-1">
      <label htmlFor={id} className="sr-only">
        {label}
      </label>
      <div className="relative">
        <select
          id={id}
          value={pendingType ?? ""}
          disabled={pendingType !== undefined}
          aria-describedby={error ? `${id}-error` : undefined}
          onChange={(event) => {
            if (event.target.value) {
              onChoose(event.target.value as ChosenPageType);
            }
          }}
          className={`min-h-7 w-full cursor-pointer appearance-none truncate rounded-sm border py-1 pr-6 pl-2 text-xs font-medium disabled:cursor-wait disabled:opacity-70 ${look}`}
        >
          <option value="" disabled>
            {placeholder}
          </option>
          {CHOSEN_PAGE_TYPES.map((type) => (
            <option key={type} value={type}>
              {PAGE_TYPE_LABEL[type]}
            </option>
          ))}
        </select>
        <span
          className={`material-symbols-outlined pointer-events-none absolute top-1/2 right-1 -translate-y-1/2 text-[14px] ${
            tone === "warning" ? "text-warning" : "text-ink"
          }`}
          aria-hidden="true"
        >
          expand_more
        </span>
      </div>
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-danger">
          {error}
        </p>
      )}
    </div>
  );
}

"use client";

import { useId, useState, type DragEvent, type ReactNode } from "react";

/**
 * A dashed area that takes dropped files, or opens the file picker when clicked or focused with the keyboard.
 * It only hands the files over: what is accepted is the API's answer (413, 415), shown next to each file.
 */
export function Dropzone({
  onFiles,
  accept,
  label,
  hint,
  disabled = false,
}: {
  onFiles: (files: File[]) => void;
  accept?: string;
  /** Names the file input, such as "Add files". */
  label: string;
  hint?: ReactNode;
  disabled?: boolean;
}) {
  const [over, setOver] = useState(false);
  const hintId = useId();

  const drag = (event: DragEvent, entering: boolean) => {
    event.preventDefault();
    if (!disabled) {
      setOver(entering);
    }
  };

  return (
    <label
      onDragEnter={(event) => drag(event, true)}
      onDragOver={(event) => drag(event, true)}
      onDragLeave={(event) => drag(event, false)}
      onDrop={(event) => {
        drag(event, false);
        if (!disabled && event.dataTransfer.files.length > 0) {
          onFiles(Array.from(event.dataTransfer.files));
        }
      }}
      className={`flex flex-col items-center justify-center gap-2 rounded-sm border border-dashed px-6 py-10 text-center transition-colors duration-150 focus-within:outline-2 focus-within:outline-focus-ring ${
        disabled
          ? "cursor-not-allowed border-line bg-background text-on-surface-muted"
          : over
            ? "cursor-copy border-ink bg-row-selected"
            : "cursor-pointer border-control-border bg-surface hover:bg-background"
      }`}
    >
      <input
        type="file"
        multiple
        accept={accept}
        disabled={disabled}
        aria-label={label}
        aria-describedby={hint ? hintId : undefined}
        className="sr-only"
        onChange={(event) => {
          const files = Array.from(event.target.files ?? []);
          // Clearing the input lets the same file be picked again after it was refused or removed.
          event.target.value = "";
          if (files.length > 0) {
            onFiles(files);
          }
        }}
      />
      <span className="material-symbols-outlined text-[24px]" aria-hidden="true">
        upload
      </span>
      <span className="text-sm font-medium">
        Drop files here or <span className="underline">browse</span>
      </span>
      {hint && (
        <span id={hintId} className="text-[13px] text-on-surface-muted">
          {hint}
        </span>
      )}
    </label>
  );
}

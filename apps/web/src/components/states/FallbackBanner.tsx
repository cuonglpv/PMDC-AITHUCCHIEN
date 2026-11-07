/**
 * DESIGN.md fallback banner: a full-width Cream Wash strip shown when an AI run failed. The work continues by hand,
 * so it informs and never blocks.
 */
export function FallbackBanner({ children = "AI assistance unavailable. Fields require manual entry." }: { children?: string }) {
  return (
    <p role="status" className="flex items-center gap-2 bg-warning-container px-6 py-2.5 text-[13px] text-warning">
      <span className="material-symbols-outlined text-[16px]" aria-hidden="true">
        warning
      </span>
      {children}
    </p>
  );
}

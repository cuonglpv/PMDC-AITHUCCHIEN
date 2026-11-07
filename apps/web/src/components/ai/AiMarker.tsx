/** DESIGN.md AI suggestion marker: a sparkle and "AI" in mono on Violet Wash, until a person sets the value. */
export function AiMarker({ label = "Suggested by AI" }: { label?: string }) {
  return (
    <span
      title={label}
      className="inline-flex h-5 items-center gap-0.5 rounded-sm border border-ai-line bg-ai-container px-1.5 font-mono text-[11px] font-semibold text-ai-strong"
    >
      <span className="material-symbols-outlined text-[13px]" aria-hidden="true">
        auto_awesome
      </span>
      AI
    </span>
  );
}

/**
 * The source pin (docs/04-information-architecture.md): where a value came from. Clicking it moves zone C to that page
 * and draws the region. A value with no source says so in words, because a person has to draw one (BR-02).
 */
export function SourcePin({
  pageNo,
  fieldName,
  onClick,
}: {
  pageNo: number | null;
  fieldName: string;
  onClick: () => void;
}) {
  if (pageNo === null) {
    return <span className="text-xs whitespace-nowrap text-on-surface-muted">No source</span>;
  }
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={`Show source of ${fieldName} on page ${pageNo}`}
      title={`Show source on page ${pageNo}`}
      className="inline-flex h-6 items-center gap-0.5 rounded-sm px-1 font-mono text-xs whitespace-nowrap text-ink transition-colors duration-150 hover:bg-surface-sunken"
    >
      <span className="material-symbols-outlined text-[14px]" aria-hidden="true">
        location_on
      </span>
      p{pageNo}
    </button>
  );
}

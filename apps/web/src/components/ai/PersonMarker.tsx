import type { StaffRef } from "@/lib/api/types";

/**
 * DESIGN.md: once a person sets a value, the AI marker gives way to their initials in a neutral chip, which is also an
 * audit cue. It is never violet, because the value is no longer AI output.
 */
export function PersonMarker({ person }: { person: StaffRef }) {
  return (
    <span
      title={`Set by ${person.fullName}`}
      className="inline-flex h-5 items-center rounded-sm bg-surface-sunken px-1.5 font-mono text-[11px] font-semibold text-ink"
    >
      <span className="sr-only">Set by </span>
      {person.initials}
    </span>
  );
}

import { ApiProblem } from "@/lib/api/problem";

/** Loading and error states from the UI state contract (docs/04-information-architecture.md). */
export function Loading({ label }: { label: string }) {
  return (
    <div role="status" aria-busy="true" aria-label={label} className="space-y-2 p-6">
      <div className="h-6 w-48 animate-pulse rounded-sm bg-surface-sunken" />
      <div className="h-32 animate-pulse rounded-sm bg-surface-sunken" />
    </div>
  );
}

export function LoadError({ error }: { error: unknown }) {
  const status = error instanceof ApiProblem ? error.status : undefined;
  const [title, detail] =
    status === 404
      ? ["This item does not exist", "It may have been removed, or the link is wrong."]
      : status === 403
        ? ["You cannot open this", "It belongs to another role or to someone else's work."]
        : ["This page could not be loaded", "Please reload the page to try again."];
  return (
    <div role="alert" className="m-6 rounded-sm border border-line bg-surface p-6">
      <p className="font-semibold">{title}</p>
      <p className="mt-1 text-[13px] text-on-surface-muted">{detail}</p>
    </div>
  );
}

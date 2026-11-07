import { ApiProblem } from "@/lib/api/problem";

/** Loading and error states from the UI state contract (docs/04-information-architecture.md). */
export function Loading({ label }: { label: string }) {
  return (
    <div aria-busy="true" aria-label={label} className="space-y-2 p-6">
      <div className="h-6 w-48 animate-pulse rounded-sm bg-surface-sunken" />
      <div className="h-32 animate-pulse rounded-sm bg-surface-sunken" />
    </div>
  );
}

export function LoadError({ error }: { error: unknown }) {
  const status = error instanceof ApiProblem ? error.status : undefined;
  return (
    <div role="alert" className="m-6 rounded-sm border border-line bg-surface p-6">
      <p className="font-semibold">{status === 404 ? "This item does not exist" : "This page could not be loaded"}</p>
      <p className="mt-1 text-[13px] text-on-surface-muted">
        {status === 404 ? "It may have been removed, or the link is wrong." : "Please reload the page to try again."}
      </p>
    </div>
  );
}

import type { ReactNode } from "react";

/**
 * DESIGN.md empty state: an icon in a 48 px hairline square, a short title, one sentence and the permitted action.
 * Say which empty it is ("No drafts" is not "No results for these filters").
 */
export function EmptyState({
  icon,
  title,
  description,
  action,
}: {
  icon: string;
  title: string;
  description: string;
  action?: ReactNode;
}) {
  return (
    <div className="flex flex-col items-start gap-3 rounded-sm border border-line bg-surface p-6">
      <span className="flex size-12 items-center justify-center border border-line" aria-hidden="true">
        <span className="material-symbols-outlined text-[20px] text-on-surface-muted">{icon}</span>
      </span>
      <div>
        <p className="text-[13px] font-semibold">{title}</p>
        <p className="mt-1 text-[13px] text-on-surface-muted">{description}</p>
      </div>
      {action}
    </div>
  );
}

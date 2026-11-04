/** Stands in for a role's home until its feature is built. Every home is in the Must release (US01). */
export function HomePlaceholder({ title, screen, stories }: { title: string; screen: string; stories: string }) {
  return (
    <div className="p-6">
      <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-primary-text uppercase">{screen}</p>
      <h1 className="mt-1 font-display text-xl font-medium tracking-[-0.01em] uppercase">{title}</h1>
      <div className="mt-6 rounded-sm border border-line bg-surface p-6">
        <p className="font-semibold">This screen is not built yet</p>
        <p className="mt-1 text-[13px] text-on-surface-muted">It arrives with {stories}.</p>
      </div>
    </div>
  );
}

type Tone = "neutral" | "success" | "warning" | "info" | "danger";

const TONE: Record<Tone, string> = {
  neutral: "bg-surface-sunken text-ink",
  success: "bg-success-container text-success",
  warning: "bg-warning-container text-warning",
  info: "bg-info-container text-info",
  danger: "bg-danger-container text-danger",
};

/** Status is never colour alone: the pill always carries its word (WCAG AA). */
export function StatusPill({ tone, children }: { tone: Tone; children: string }) {
  return (
    <span className={`inline-flex h-[22px] items-center rounded-full px-2.5 text-[13px] font-medium ${TONE[tone]}`}>
      {children}
    </span>
  );
}

"use client";

export type IntakeStep = 1 | 2 | 3;

const STEPS: { step: IntakeStep; label: string }[] = [
  { step: 1, label: "Member & policy" },
  { step: 2, label: "Claim details" },
  { step: 3, label: "Documents" },
];

/**
 * The New claim stepper. A finished step shows a check, and is a button when the officer may go back to it.
 * Mono numbering per DESIGN.md; the current step is underlined in ink.
 */
export function IntakeStepper({
  current,
  done,
  onStep,
}: {
  current: IntakeStep;
  done: readonly IntakeStep[];
  onStep?: (step: IntakeStep) => void;
}) {
  return (
    <nav aria-label="Intake progress" className="mt-4">
      <ol className="flex gap-8">
        {STEPS.map(({ step, label }) => {
          const isCurrent = step === current;
          const isDone = done.includes(step);
          const content = (
            <>
              <span className="font-mono">{step}</span>
              <span>{label}</span>
              {isDone && (
                <>
                  <span className="material-symbols-outlined text-[16px] text-success" aria-hidden="true">
                    check
                  </span>
                  <span className="sr-only">(done)</span>
                </>
              )}
            </>
          );
          const className = `flex h-8 items-center gap-2 border-b-2 text-sm font-medium ${
            isCurrent ? "border-ink text-ink" : "border-transparent text-on-surface-muted"
          }`;
          return (
            <li key={step} aria-current={isCurrent ? "step" : undefined}>
              {onStep && isDone && !isCurrent ? (
                <button type="button" className={`${className} hover:text-ink`} onClick={() => onStep(step)}>
                  {content}
                </button>
              ) : (
                <span className={className}>{content}</span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

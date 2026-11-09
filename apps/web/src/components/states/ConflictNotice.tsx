import { Button } from "@/components/ui/Button";

/**
 * The Conflict state (docs/04-information-architecture.md): someone else changed this first. What the person typed stays
 * where it is; this shows what is on the server now and offers a reload.
 */
export function ConflictNotice({ current, onReload }: { current: string | null; onReload: () => void }) {
  return (
    <div role="alert" className="flex flex-wrap items-center gap-3 rounded-sm bg-warning-container px-3 py-2 text-[13px] text-warning">
      <span className="min-w-0 flex-1">
        Someone else changed this first.
        {current !== null && (
          <>
            {" "}
            The value is now <strong className="font-semibold">{current}</strong>.
          </>
        )}{" "}
        What you typed is kept.
      </span>
      <Button className="h-8" onClick={onReload}>
        Reload
      </Button>
    </div>
  );
}

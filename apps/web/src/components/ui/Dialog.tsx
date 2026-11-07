"use client";

import { Dialog as Base } from "@base-ui/react/dialog";
import type { ReactNode } from "react";

/** A modal on Base UI, styled from DESIGN.md: square corners, the one soft shadow the system allows. */
export function Dialog({
  open,
  onOpenChange,
  title,
  description,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <Base.Root open={open} onOpenChange={onOpenChange}>
      <Base.Portal>
        <Base.Backdrop className="fixed inset-0 bg-scrim" />
        <Base.Popup className="fixed top-1/2 left-1/2 w-[440px] max-w-[calc(100vw-32px)] -translate-x-1/2 -translate-y-1/2 border border-line bg-surface p-6 shadow-2xl">
          <Base.Title className="text-base font-semibold">{title}</Base.Title>
          {description && (
            <Base.Description className="mt-1 text-[13px] text-on-surface-muted">{description}</Base.Description>
          )}
          <div className="mt-5">{children}</div>
        </Base.Popup>
      </Base.Portal>
    </Base.Root>
  );
}

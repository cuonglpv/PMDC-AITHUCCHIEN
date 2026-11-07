"use client";

import { Tabs as Base } from "@base-ui/react/tabs";
import type { ReactNode } from "react";

/**
 * Tabs on Base UI, styled from DESIGN.md: 40 px, label-md, muted until active, then ink with a 2 px underline.
 * Base UI gives the roles, the arrow-key path and the links between each tab and its panel.
 */
export function Tabs<Value extends string>({
  value,
  onValueChange,
  children,
  className = "",
}: {
  value: Value;
  onValueChange: (value: Value) => void;
  children: ReactNode;
  className?: string;
}) {
  return (
    <Base.Root value={value} onValueChange={(next) => onValueChange(next as Value)} className={className}>
      {children}
    </Base.Root>
  );
}

export function TabList({ label, children }: { label: string; children: ReactNode }) {
  return (
    <Base.List aria-label={label} className="flex border-b border-line">
      {children}
    </Base.List>
  );
}

/** A tab with an optional count badge in mono, such as "Drafts 3". */
export function Tab({ value, count, children }: { value: string; count?: number; children: ReactNode }) {
  return (
    <Base.Tab
      value={value}
      className="-mb-px inline-flex h-10 items-center gap-2 border-b-2 border-transparent px-3 text-[13px] font-medium whitespace-nowrap text-on-surface-muted transition-colors duration-150 hover:text-ink data-[active]:border-ink data-[active]:text-ink"
    >
      {children}
      {/* The space keeps the accessible name readable ("Drafts 2"); flex layout does not draw it. */}
      {count !== undefined && " "}
      {count !== undefined && (
        <span className="rounded-sm bg-surface-sunken px-1.5 font-mono text-[11px] leading-[18px] text-ink">{count}</span>
      )}
    </Base.Tab>
  );
}

export function TabPanel({ value, children, className = "" }: { value: string; children: ReactNode; className?: string }) {
  return (
    <Base.Panel value={value} className={className}>
      {children}
    </Base.Panel>
  );
}

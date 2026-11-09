"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useWorkspace } from "./workspace-context";

/** Only the tabs that exist: the IA never shows a disabled item. The others arrive with their slices. */
const TABS = [{ segment: "evidence", label: "Evidence" }] as const;

export function TabBar() {
  const { claimId } = useWorkspace();
  const pathname = usePathname();
  return (
    <nav aria-label="Claim sections" className="flex border-y border-line bg-surface px-3">
      {TABS.map((tab) => {
        const href = `/claims/${claimId}/${tab.segment}`;
        const active = pathname === href;
        return (
          <Link
            key={tab.segment}
            href={href}
            aria-current={active ? "page" : undefined}
            className={`-mb-px inline-flex h-10 items-center border-b-2 px-3 text-[13px] font-medium whitespace-nowrap transition-colors duration-150 hover:text-ink ${
              active ? "border-ink text-ink" : "border-transparent text-on-surface-muted"
            }`}
          >
            {tab.label}
          </Link>
        );
      })}
    </nav>
  );
}

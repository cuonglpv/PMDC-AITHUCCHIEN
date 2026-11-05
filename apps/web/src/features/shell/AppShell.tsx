"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import type { ReactNode } from "react";
import { useSession } from "@/features/auth/use-session";
import { api } from "@/lib/api/client";
import { session } from "@/lib/api/session";
import { navFor, type NavItem } from "@/lib/auth/roles";
import { ROLE_LABEL } from "@/lib/labels/roles";

const GROUPS: NavItem["group"][] = ["Work", "Policy", "Insights"];

/** Left sidebar, top bar, content area (docs/04-information-architecture.md, Navigation model). */
export function AppShell({ children }: { children: ReactNode }) {
  const state = useSession();
  const pathname = usePathname();
  const router = useRouter();

  if (state.status !== "signedIn") {
    return null;
  }

  const { user } = state;
  const items = navFor(user.role);

  const signOut = async () => {
    await api.POST("/auth/logout").catch(() => undefined);
    session.end();
    router.replace("/sign-in");
  };

  return (
    <div className="flex min-h-screen">
      <aside className="flex w-sidebar shrink-0 flex-col bg-shell text-on-shell-muted">
        <div className="flex h-top-bar items-center gap-2.5 px-4">
          <span className="inline-block size-2.5 rounded-[1px] bg-primary" aria-hidden="true" />
          <span className="text-base font-bold tracking-tight text-on-shell">ClaimLens</span>
        </div>

        <nav aria-label="Main" className="flex-1 space-y-6 py-4">
          {GROUPS.map((group) => {
            const links = items.filter((item) => item.group === group);
            if (links.length === 0) {
              return null;
            }
            return (
              <div key={group}>
                <p className="mb-1 px-4 font-mono text-[11px] font-medium tracking-[0.08em] text-shell-accent uppercase">
                  {group}
                </p>
                <ul>
                  {links.map((item) => {
                    const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
                    return (
                      <li key={item.href}>
                        <Link
                          href={item.href}
                          aria-current={active ? "page" : undefined}
                          className={`flex h-9 items-center gap-3 border-l-2 px-4 text-[13px] font-medium transition-colors duration-150 ${
                            active
                              ? "border-primary bg-shell-raised text-on-shell"
                              : "border-transparent hover:text-on-shell"
                          }`}
                        >
                          <span className="material-symbols-outlined text-[18px]" aria-hidden="true">
                            {item.icon}
                          </span>
                          {item.label}
                        </Link>
                      </li>
                    );
                  })}
                </ul>
              </div>
            );
          })}
        </nav>

        <div className="border-t border-shell-raised p-4">
          <Link href="/profile" className="block hover:text-on-shell">
            <span className="block text-[13px] font-medium text-on-shell">{user.fullName}</span>
            <span className="block text-xs">{ROLE_LABEL[user.role]}</span>
          </Link>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-top-bar items-center justify-end gap-4 border-b border-line bg-surface px-6">
          <span
            aria-hidden="true"
            className="flex size-8 items-center justify-center rounded-full bg-surface-sunken text-xs font-semibold"
          >
            {user.initials}
          </span>
          <button type="button" className="text-[13px] font-medium underline" onClick={() => void signOut()}>
            Sign out
          </button>
        </header>
        <main className="flex-1">{children}</main>
      </div>
    </div>
  );
}

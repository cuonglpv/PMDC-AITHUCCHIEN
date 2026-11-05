"use client";

import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { session } from "@/lib/api/session";
import { canOpen } from "@/lib/auth/roles";
import { useSession } from "./use-session";

/**
 * Guards every signed-in page. It runs in the browser because only the browser has a session: the access token is in
 * memory and the refresh cookie goes only to /api/v1/auth, so the Next.js server never knows who is signed in.
 * Reloading a page clears memory, so the gate refreshes first (docs/07-api-contract.md, step 2).
 */
export function AuthGate({ children }: { children: ReactNode }) {
  const state = useSession();
  const pathname = usePathname();
  const router = useRouter();

  useEffect(() => {
    if (state.status === "unknown") {
      void session.refresh();
    }
    if (state.status === "signedOut") {
      router.replace(`/sign-in?next=${encodeURIComponent(pathname)}`);
    }
    if (state.status === "signedIn" && !canOpen(state.user.role, pathname)) {
      router.replace("/403");
    }
  }, [state, pathname, router]);

  if (state.status !== "signedIn" || !canOpen(state.user.role, pathname)) {
    return <div aria-busy="true" className="flex min-h-screen bg-shell" />;
  }

  return children;
}

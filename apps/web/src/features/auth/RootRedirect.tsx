"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { session } from "@/lib/api/session";
import { ROLE_HOME } from "@/lib/auth/roles";
import { useSession } from "./use-session";

/** `/` is not a page of its own: each role goes to its home (US01). */
export function RootRedirect() {
  const state = useSession();
  const router = useRouter();

  useEffect(() => {
    if (state.status === "unknown") {
      void session.refresh();
    }
    if (state.status === "signedOut") {
      router.replace("/sign-in");
    }
    if (state.status === "signedIn") {
      router.replace(ROLE_HOME[state.user.role]);
    }
  }, [state, router]);

  return <div aria-busy="true" className="min-h-screen bg-shell" />;
}

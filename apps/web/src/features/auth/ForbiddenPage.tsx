"use client";

import Link from "next/link";
import { ROLE_HOME } from "@/lib/auth/roles";
import { useSession } from "./use-session";

export function ForbiddenPage() {
  const state = useSession();
  const home = state.status === "signedIn" ? ROLE_HOME[state.user.role] : "/";

  return (
    <div className="flex flex-col items-start gap-4 p-6">
      <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-primary-text uppercase">403</p>
      <h1 className="text-base font-semibold">You do not have access to this page</h1>
      <Link className="underline" href={home}>
        Back to your home page
      </Link>
    </div>
  );
}

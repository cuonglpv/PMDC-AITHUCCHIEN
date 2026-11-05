"use client";

import { useSession } from "@/features/auth/use-session";
import { ROLE_LABEL } from "@/lib/labels/roles";

export function ProfilePage() {
  const state = useSession();
  if (state.status !== "signedIn") {
    return null;
  }

  const { user } = state;
  return (
    <div className="p-6">
      <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-primary-text uppercase">SCR-70</p>
      <h1 className="mt-1 font-display text-xl font-medium tracking-[-0.01em] uppercase">Profile</h1>
      <dl className="mt-6 grid max-w-md grid-cols-[120px_1fr] gap-y-2 rounded-sm border border-line bg-surface p-6 text-[13px]">
        <dt className="text-on-surface-muted">Name</dt>
        <dd>{user.fullName}</dd>
        <dt className="text-on-surface-muted">Email</dt>
        <dd>{user.email}</dd>
        <dt className="text-on-surface-muted">Role</dt>
        <dd>{ROLE_LABEL[user.role]}</dd>
      </dl>
    </div>
  );
}

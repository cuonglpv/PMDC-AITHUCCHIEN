import type { ReactNode } from "react";
import { AuthGate } from "@/features/auth/AuthGate";
import { AppShell } from "@/features/shell/AppShell";

export default function SignedInLayout({ children }: { children: ReactNode }) {
  return (
    <AuthGate>
      <AppShell>{children}</AppShell>
    </AuthGate>
  );
}

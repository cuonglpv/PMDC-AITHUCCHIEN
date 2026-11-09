import type { ReactNode } from "react";
import { ClaimWorkspace } from "@/features/workspace/shell/ClaimWorkspace";

export default async function ClaimLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  return <ClaimWorkspace claimId={id}>{children}</ClaimWorkspace>;
}

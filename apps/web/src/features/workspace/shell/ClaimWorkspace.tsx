"use client";

import type { ReactNode } from "react";
import { FallbackBanner } from "@/components/states/FallbackBanner";
import { LoadError, Loading } from "@/components/states/PageState";
import { ForbiddenPage } from "@/features/auth/ForbiddenPage";
import { useClaim } from "@/lib/api/claim-queries";
import { ApiProblem } from "@/lib/api/problem";
import { ActionBar } from "./ActionBar";
import { CaseHeader } from "./CaseHeader";
import { ContextRail } from "./ContextRail";
import { DocumentRail } from "./DocumentRail";
import { TabBar } from "./TabBar";
import { useWorkspace, WorkspaceProvider } from "./workspace-context";

/**
 * The Claim workspace (docs/04-information-architecture.md): case header and tabs on top, the three zones in the middle,
 * the sticky action bar below. Each zone scrolls on its own, so the header and action bar stay on screen.
 */
export function ClaimWorkspace({ claimId, children }: { claimId: string; children: ReactNode }) {
  const claim = useClaim(claimId);

  if (claim.isPending) {
    return <Loading label="Loading the claim" />;
  }
  if (claim.isError) {
    return claim.error instanceof ApiProblem && claim.error.status === 403 ? (
      <ForbiddenPage />
    ) : (
      <LoadError error={claim.error} />
    );
  }

  return (
    <WorkspaceProvider claim={claim.data}>
      <div className="flex h-[calc(100dvh-var(--spacing-top-bar))] min-w-[1100px] flex-col">
        <CaseHeader />
        <TabBar />
        <Fallback />
        <div className="flex min-h-0 flex-1">
          <DocumentRail />
          <div className="relative min-w-0 flex-1 overflow-y-auto">{children}</div>
          <ContextRail />
        </div>
        <ActionBar />
      </div>
    </WorkspaceProvider>
  );
}

function Fallback() {
  const { claim, snapshot } = useWorkspace();
  return claim.ai.fallbackActive || snapshot?.fallbackActive ? <FallbackBanner /> : null;
}

"use client";

import { keepPreviousData, useMutation, useQuery } from "@tanstack/react-query";
import { claimKeys } from "@/lib/api/claim-queries";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/problem";
import type { QueueView, Warning } from "@/lib/api/types";
import { isProcessing, POLL_INTERVAL_MS } from "./processing";

export { useClaim, useDocuments, usePageImage, useReclassifyPage } from "@/lib/api/claim-queries";

export const intakeKeys = {
  ...claimKeys,
  queue: (view: QueueView, page: number) => ["claims", "queue", view, page] as const,
  warnings: (claimId: string) => ["claims", "warnings", claimId] as const,
  policy: (policyId: string, serviceDate: string | undefined) =>
    ["policies", policyId, "summary", serviceDate ?? "today"] as const,
};

export const QUEUE_PAGE_SIZE = 25;

/** SCR-10 tabs. The previous page stays on screen while the next loads, so the tab badges do not flicker. */
export function useQueue(view: QueueView, page: number) {
  return useQuery({
    queryKey: intakeKeys.queue(view, page),
    queryFn: () => unwrap(api.GET("/claims", { params: { query: { view, page, pageSize: QUEUE_PAGE_SIZE } } })),
    placeholderData: keepPreviousData,
  });
}

/** `searchMembers` is a POST so a citizen ID never reaches a URL; it is a mutation because each search is asked for. */
export function useMemberSearch() {
  return useMutation({
    mutationFn: (query: string) => unwrap(api.POST("/members/search", { body: { query } })),
  });
}

/** The remaining balance comes from the rule-set version effective on `serviceDate`, or today when it is left out. */
export function usePolicySummary(policyId: string | undefined, serviceDate?: string) {
  return useQuery({
    queryKey: intakeKeys.policy(policyId ?? "", serviceDate),
    queryFn: () =>
      unwrap(
        api.GET("/policies/{policyId}/summary", {
          params: { path: { policyId: policyId! }, query: serviceDate ? { serviceDate } : {} },
        }),
      ),
    enabled: Boolean(policyId),
  });
}

/**
 * The one poll (docs/10-code-structure.md, Staff Web App rule 2): every 3 seconds while a file converts, a page check or
 * the field extraction is queued or running, or the submitted claim is still processing; not at all otherwise.
 */
export function useClaimStatus(claimId: string) {
  return useQuery({
    queryKey: intakeKeys.status(claimId),
    queryFn: () => unwrap(api.GET("/claims/{claimId}/status", { params: { path: { claimId } } })),
    refetchInterval: (query) => (query.state.data && isProcessing(query.state.data) ? POLL_INTERVAL_MS : false),
  });
}

/**
 * Warnings from `createClaim` or `updateClaim`, handed to the documents screen through the cache. They are shown once,
 * where the officer lands; nothing fetches them.
 */
export function useClaimWarnings(claimId: string) {
  return useQuery<Warning[]>({
    queryKey: intakeKeys.warnings(claimId),
    queryFn: () => [],
    enabled: false,
    staleTime: Infinity,
  });
}

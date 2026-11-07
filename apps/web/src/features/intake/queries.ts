"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/problem";
import type { ChosenPageType, ClaimStatusSnapshot, Document, DocumentPage, QueueView, Warning } from "@/lib/api/types";
import { isProcessing, POLL_INTERVAL_MS } from "./processing";

/** Separate roots, so invalidating one claim's detail never refetches its status poll or its documents. */
export const intakeKeys = {
  queues: ["claims", "queue"] as const,
  queue: (view: QueueView, page: number) => ["claims", "queue", view, page] as const,
  claim: (claimId: string) => ["claims", "detail", claimId] as const,
  status: (claimId: string) => ["claims", "status", claimId] as const,
  documents: (claimId: string) => ["claims", "documents", claimId] as const,
  warnings: (claimId: string) => ["claims", "warnings", claimId] as const,
  page: (claimId: string, pageId: string, variant: "full" | "thumbnail") =>
    ["claims", "page-image", claimId, pageId, variant] as const,
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

export function useClaim(claimId: string) {
  return useQuery({
    queryKey: intakeKeys.claim(claimId),
    queryFn: () => unwrap(api.GET("/claims/{claimId}", { params: { path: { claimId } } })),
  });
}

export function useDocuments(claimId: string) {
  return useQuery({
    queryKey: intakeKeys.documents(claimId),
    queryFn: () => unwrap(api.GET("/claims/{claimId}/documents", { params: { path: { claimId } } })),
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
 * `reclassifyPage` from SCR-13: the officer chooses the type of an Unclassified page before submission. The answer is
 * the page, now set by the officer (`USER`); it replaces the page in the list and in the last poll, so the card does
 * not flash back to Unclassified before the next poll arrives.
 */
export function useReclassifyPage(claimId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ pageId, pageType }: { pageId: string; pageType: ChosenPageType }) =>
      unwrap(
        api.PUT("/claims/{claimId}/pages/{pageId}/type", { params: { path: { claimId, pageId } }, body: { pageType } }),
      ),
    onSuccess: (updated: DocumentPage) => {
      queryClient.setQueryData(intakeKeys.documents(claimId), (list: { items: Document[] } | undefined) =>
        list
          ? {
              items: list.items.map((document) =>
                document.id === updated.documentId
                  ? { ...document, pages: document.pages.map((page) => (page.id === updated.id ? updated : page)) }
                  : document,
              ),
            }
          : list,
      );
      queryClient.setQueryData(intakeKeys.status(claimId), (snapshot: ClaimStatusSnapshot | undefined) =>
        snapshot
          ? {
              ...snapshot,
              documents: snapshot.documents.map((document) => ({
                ...document,
                pages: document.pages.map((page) =>
                  page.id === updated.id
                    ? { ...page, pageType: updated.pageType, quality: updated.quality, aiStatus: updated.aiStatus }
                    : page,
                ),
              })),
            }
          : snapshot,
      );
      void queryClient.invalidateQueries({ queryKey: intakeKeys.status(claimId) });
    },
    // A 409 means the claim moved on (it was submitted elsewhere): reload what the screen shows.
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: intakeKeys.claim(claimId) });
      void queryClient.invalidateQueries({ queryKey: intakeKeys.status(claimId) });
    },
  });
}

/** A page image as a blob: bearer tokens cannot ride on an `<img>` tag, so the image comes through the API client. */
export function usePageImage(claimId: string, pageId: string, variant: "full" | "thumbnail") {
  return useQuery({
    queryKey: intakeKeys.page(claimId, pageId, variant),
    queryFn: () =>
      unwrap(
        api.GET("/claims/{claimId}/pages/{pageId}/image", {
          params: { path: { claimId, pageId }, query: { variant } },
          parseAs: "blob",
        }),
      ),
    staleTime: Infinity,
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

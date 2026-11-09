"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./client";
import { unwrap } from "./problem";
import type { ChosenPageType, ClaimDetail, ClaimStatusSnapshot, Document, DocumentPage } from "./types";

/**
 * What the Intake screens and the Claim workspace read about the same claim, under one set of keys. Separate roots, so
 * invalidating one claim's detail never refetches its status poll or its documents.
 */
export const claimKeys = {
  queues: ["claims", "queue"] as const,
  claim: (claimId: string) => ["claims", "detail", claimId] as const,
  status: (claimId: string) => ["claims", "status", claimId] as const,
  documents: (claimId: string) => ["claims", "documents", claimId] as const,
  page: (claimId: string, pageId: string, variant: "full" | "thumbnail") =>
    ["claims", "page-image", claimId, pageId, variant] as const,
};

export function useClaim(claimId: string) {
  return useQuery({
    queryKey: claimKeys.claim(claimId),
    queryFn: () => unwrap(api.GET("/claims/{claimId}", { params: { path: { claimId } } })),
  });
}

export function useDocuments(claimId: string, { enabled = true }: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: claimKeys.documents(claimId),
    queryFn: () => unwrap(api.GET("/claims/{claimId}/documents", { params: { path: { claimId } } })),
    enabled,
  });
}

/** A page image as a blob: bearer tokens cannot ride on an `<img>` tag, so the image comes through the API client. */
export function usePageImage(claimId: string, pageId: string, variant: "full" | "thumbnail") {
  return useQuery({
    queryKey: claimKeys.page(claimId, pageId, variant),
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
 * `reclassifyPage`, for the officer on SCR-13 and the reviewer on SCR-31. The answer is the page, now set by a person
 * (`USER`); it replaces the page in the list and in the last poll, so the page does not flash back to Unclassified
 * before the next poll arrives. `onSaved` lets a screen reload what else the new type changed.
 */
export function useReclassifyPage(claimId: string, onSaved?: () => void) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ pageId, pageType }: { pageId: string; pageType: ChosenPageType }) =>
      unwrap(
        api.PUT("/claims/{claimId}/pages/{pageId}/type", { params: { path: { claimId, pageId } }, body: { pageType } }),
      ),
    onSuccess: (updated: DocumentPage) => {
      queryClient.setQueryData(claimKeys.documents(claimId), (list: { items: Document[] } | undefined) =>
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
      queryClient.setQueryData(claimKeys.status(claimId), (snapshot: ClaimStatusSnapshot | undefined) =>
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
      void queryClient.invalidateQueries({ queryKey: claimKeys.status(claimId) });
      onSaved?.();
    },
    // A 409 means the claim moved on: reload what the screen shows.
    onError: () => {
      void queryClient.invalidateQueries({ queryKey: claimKeys.claim(claimId) });
      void queryClient.invalidateQueries({ queryKey: claimKeys.status(claimId) });
    },
  });
}

/**
 * `startReview`, from a queue row, "Open next claim" or the workspace. The answer is the claim in full, so the workspace
 * opens without asking again. A refusal means the queue is out of date, so the lists reload either way.
 */
export function useStartReview() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (claimId: string) =>
      unwrap(api.POST("/claims/{claimId}/start-review", { params: { path: { claimId } } })),
    onSuccess: (claim: ClaimDetail) => {
      queryClient.setQueryData(claimKeys.claim(claim.id), claim);
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: claimKeys.queues });
    },
  });
}

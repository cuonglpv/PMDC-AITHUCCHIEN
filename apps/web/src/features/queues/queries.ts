"use client";

import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { claimKeys } from "@/lib/api/claim-queries";
import { api } from "@/lib/api/client";
import { ApiProblem, unwrap } from "@/lib/api/problem";
import type { ClaimDetail, QueueView } from "@/lib/api/types";

export const QUEUE_PAGE_SIZE = 25;

/** How many claims "Open next claim" tries before it stops: each try has lost a race, so this is already unusual. */
const OPEN_NEXT_ATTEMPTS = 5;

/** The same root as the Intake queues, so a change to a claim reloads every list. */
const reviewQueueKey = (view: QueueView, page: number, claimNumber: string) =>
  [...claimKeys.queues, "review", view, page, claimNumber] as const;

/** SCR-20 tabs. The previous page stays while the next loads, so the tab badges do not flicker. */
export function useReviewQueue(view: QueueView, page: number, claimNumber: string) {
  return useQuery({
    queryKey: reviewQueueKey(view, page, claimNumber),
    queryFn: () =>
      unwrap(
        api.GET("/claims", {
          params: {
            query: { view, page, pageSize: QUEUE_PAGE_SIZE, ...(claimNumber ? { claimNumber } : {}) },
          },
        }),
      ),
    placeholderData: keepPreviousData,
  });
}

/**
 * "Open next claim" (docs/07-api-contract.md, Taking a claim): `listClaims` with `pageSize=1`, which is the first claim
 * in the API's priority order, then `startReview`. Someone else may take that claim first, and a 409 sends it to the
 * next one. Null when no claim is available.
 */
export function useOpenNextClaim() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (): Promise<ClaimDetail | null> => {
      let lost: ApiProblem | null = null;
      for (let attempt = 0; attempt < OPEN_NEXT_ATTEMPTS; attempt += 1) {
        const queue = await unwrap(
          api.GET("/claims", { params: { query: { view: "AVAILABLE", page: 1, pageSize: 1 } } }),
        );
        const next = queue.items[0];
        if (!next) {
          return null;
        }
        try {
          return await unwrap(api.POST("/claims/{claimId}/start-review", { params: { path: { claimId: next.id } } }));
        } catch (error) {
          if (!(error instanceof ApiProblem) || error.status !== 409) {
            throw error;
          }
          lost = error;
        }
      }
      throw lost;
    },
    onSuccess: (claim) => {
      if (claim) {
        queryClient.setQueryData(claimKeys.claim(claim.id), claim);
      }
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: claimKeys.queues });
    },
  });
}

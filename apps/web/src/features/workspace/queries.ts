"use client";

import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { isProcessing, POLL_INTERVAL_MS } from "@/features/intake/processing";
import { claimKeys, useReclassifyPage } from "@/lib/api/claim-queries";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/problem";
import type {
  AiRunState,
  Bbox,
  ClaimDetail,
  ClaimField,
  ClaimProgress,
  ClaimStatusSnapshot,
  FieldList,
  UpdateFieldRequest,
} from "@/lib/api/types";

export const workspaceKeys = {
  fields: (claimId: string) => ["claims", "fields", claimId] as const,
};

/** A region a person drew: a page and a box on it, normalised to the page. */
export type PageRegion = { pageId: string; bbox: Bbox };

export const isRereadActive = (run: AiRunState) =>
  run.task === "PAGE_REREAD" && (run.status === "QUEUED" || run.status === "RUNNING");

/** The workspace polls while intake's processing runs, and while "Re-run AI on this page" is queued or running. */
export function needsPolling(snapshot: ClaimStatusSnapshot): boolean {
  return isProcessing(snapshot) || snapshot.aiRuns.some(isRereadActive);
}

export function useWorkspaceStatus(claimId: string, enabled: boolean) {
  return useQuery({
    queryKey: claimKeys.status(claimId),
    queryFn: () => unwrap(api.GET("/claims/{claimId}/status", { params: { path: { claimId } } })),
    enabled,
    refetchInterval: (query) => (query.state.data && needsPolling(query.state.data) ? POLL_INTERVAL_MS : false),
  });
}

export function useFields(claimId: string, enabled: boolean) {
  return useQuery({
    queryKey: workspaceKeys.fields(claimId),
    queryFn: () => unwrap(api.GET("/claims/{claimId}/fields", { params: { path: { claimId } } })),
    enabled,
  });
}

const NO_COUNTS: NonNullable<ClaimDetail["counts"]> = {
  fieldsToVerify: 0,
  blockingAlerts: 0,
  warningAlerts: 0,
  mappingsToConfirm: 0,
  openFlags: 0,
  keptFlags: 0,
};

/**
 * What every change answers with. It feeds the action bar and the Evidence counter straight away; when the claim moved
 * to another status (to In review, once the last required field is resolved), the claim reloads for what only it says,
 * such as `allowedActions`.
 */
export function applyProgress(queryClient: QueryClient, claimId: string, progress: ClaimProgress) {
  const claim = queryClient.getQueryData<ClaimDetail>(claimKeys.claim(claimId));
  if (claim) {
    queryClient.setQueryData<ClaimDetail>(claimKeys.claim(claimId), {
      ...claim,
      rowVersion: progress.rowVersion,
      counts: {
        ...(claim.counts ?? NO_COUNTS),
        fieldsToVerify: progress.fieldsToVerify,
        blockingAlerts: progress.blockingAlerts,
        warningAlerts: progress.warningAlerts,
        mappingsToConfirm: progress.mappingsToConfirm,
      },
    });
  }
  queryClient.setQueryData<FieldList>(workspaceKeys.fields(claimId), (list) =>
    list ? { ...list, remainingToVerify: progress.fieldsToVerify } : list,
  );
  if (!claim || claim.status !== progress.status || claim.isReturned !== progress.isReturned) {
    void queryClient.invalidateQueries({ queryKey: claimKeys.claim(claimId) });
  }
  void queryClient.invalidateQueries({ queryKey: claimKeys.queues });
}

/** The saved field replaces the old one where it stands: rows do not jump while the reviewer works down the list. */
function applyField(queryClient: QueryClient, claimId: string, field: ClaimField) {
  queryClient.setQueryData<FieldList>(workspaceKeys.fields(claimId), (list) =>
    list ? { ...list, items: list.items.map((item) => (item.id === field.id ? field : item)) } : list,
  );
}

export function useAcceptField(claimId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ field, region }: { field: ClaimField; region: PageRegion | null }) =>
      unwrap(
        api.POST("/claims/{claimId}/fields/{fieldId}/accept", {
          params: { path: { claimId, fieldId: field.id } },
          body: {
            rowVersion: field.rowVersion,
            ...(region && { sourcePageId: region.pageId, sourceBbox: region.bbox }),
          },
        }),
      ),
    onSuccess: ({ field, progress }) => {
      applyField(queryClient, claimId, field);
      applyProgress(queryClient, claimId, progress);
    },
  });
}

export function useUpdateField(claimId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({
      field,
      change,
      region,
    }: {
      field: ClaimField;
      change: Pick<UpdateFieldRequest, "action" | "valueText" | "valueAmount" | "valueDate">;
      region: PageRegion | null;
    }) =>
      unwrap(
        api.PATCH("/claims/{claimId}/fields/{fieldId}", {
          params: { path: { claimId, fieldId: field.id } },
          body: {
            rowVersion: field.rowVersion,
            ...change,
            ...(region && { sourcePageId: region.pageId, sourceBbox: region.bbox }),
          },
        }),
      ),
    onSuccess: ({ field, progress }) => {
      applyField(queryClient, claimId, field);
      applyProgress(queryClient, claimId, progress);
    },
  });
}

/** A new line brings its empty field slots, so the fields reload before the caller goes on to the first of them. */
export function useAddExpenseLine(claimId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (documentId: string) =>
      unwrap(api.POST("/claims/{claimId}/expense-lines", { params: { path: { claimId } }, body: { documentId } })),
    onSuccess: async ({ progress }) => {
      applyProgress(queryClient, claimId, progress);
      await queryClient.invalidateQueries({ queryKey: workspaceKeys.fields(claimId) });
    },
  });
}

export function useRemoveExpenseLine(claimId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (lineId: string) =>
      unwrap(api.DELETE("/claims/{claimId}/expense-lines/{lineId}", { params: { path: { claimId, lineId } } })),
    onSuccess: async (progress) => {
      applyProgress(queryClient, claimId, progress);
      await queryClient.invalidateQueries({ queryKey: workspaceKeys.fields(claimId) });
    },
  });
}

/** Queues a run for one page. The poll is refreshed before this resolves, so the screen sees the run as soon as it does. */
export function useRerunExtraction(claimId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (pageId: string) =>
      unwrap(api.POST("/claims/{claimId}/pages/{pageId}/rerun-extraction", { params: { path: { claimId, pageId } } })),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: claimKeys.status(claimId) }),
  });
}

/** Everything a re-read may have changed. */
export function reloadAfterReread(queryClient: QueryClient, claimId: string) {
  void queryClient.invalidateQueries({ queryKey: workspaceKeys.fields(claimId) });
  void queryClient.invalidateQueries({ queryKey: claimKeys.documents(claimId) });
  void queryClient.invalidateQueries({ queryKey: claimKeys.claim(claimId) });
}

/** A reviewer's new page type creates empty field slots (US06), which also changes the count of fields to verify. */
export function useReviewerReclassify(claimId: string) {
  const queryClient = useQueryClient();
  return useReclassifyPage(claimId, () => {
    void queryClient.invalidateQueries({ queryKey: workspaceKeys.fields(claimId) });
    void queryClient.invalidateQueries({ queryKey: claimKeys.claim(claimId) });
  });
}

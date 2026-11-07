"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api/client";
import { unwrap } from "@/lib/api/problem";

export const planKeys = {
  plans: ["plans"] as const,
  versions: (planId: string) => ["plans", planId, "versions"] as const,
  version: (planId: string, versionId: string) => ["plans", planId, "versions", versionId] as const,
  categories: ["benefit-categories"] as const,
};

export function usePlans() {
  return useQuery({ queryKey: planKeys.plans, queryFn: () => unwrap(api.GET("/plans")) });
}

export function useVersions(planId: string) {
  return useQuery({
    queryKey: planKeys.versions(planId),
    queryFn: () => unwrap(api.GET("/plans/{planId}/versions", { params: { path: { planId } } })),
  });
}

export function useVersion(planId: string, versionId: string) {
  return useQuery({
    queryKey: planKeys.version(planId, versionId),
    queryFn: () =>
      unwrap(api.GET("/plans/{planId}/versions/{versionId}", { params: { path: { planId, versionId } } })),
  });
}

export function useCategories() {
  return useQuery({
    queryKey: planKeys.categories,
    queryFn: () => unwrap(api.GET("/benefit-categories")),
    staleTime: Infinity,
  });
}

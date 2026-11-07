"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { LoadError, Loading } from "@/components/states/PageState";
import { Button } from "@/components/ui/Button";
import { useSession } from "@/features/auth/use-session";
import { api } from "@/lib/api/client";
import { ApiProblem, unwrap } from "@/lib/api/problem";
import { formatDate, formatDateTime } from "@/lib/format";
import { planKeys, usePlans, useVersions } from "./queries";
import { VersionStatus } from "./VersionStatus";

/** SCR-41: one plan's versions, newest first. Only the Administrator creates drafts; the others read (US19). */
export function VersionsPage({ planId }: { planId: string }) {
  const state = useSession();
  const router = useRouter();
  const queryClient = useQueryClient();
  const plans = usePlans();
  const versions = useVersions(planId);

  const createDraft = useMutation({
    mutationFn: (basedOnVersionId: string | undefined) =>
      unwrap(
        api.POST("/plans/{planId}/versions", {
          params: { path: { planId } },
          body: basedOnVersionId ? { basedOnVersionId } : {},
        }),
      ),
    onSuccess: async (draft) => {
      await queryClient.invalidateQueries({ queryKey: planKeys.plans });
      router.push(`/plans/${planId}/versions/${draft.id}`);
    },
  });

  if (versions.isPending || plans.isPending) {
    return <Loading label="Loading versions" />;
  }
  if (versions.isError) {
    return <LoadError error={versions.error} />;
  }

  const plan = plans.data?.items.find((item) => item.id === planId);
  const items = versions.data.items;
  const draft = items.find((version) => version.status === "DRAFT");
  const latestPublished = items.find((version) => version.status === "PUBLISHED");
  const isAdmin = state.status === "signedIn" && state.user.role === "ADMINISTRATOR";

  return (
    <div className="p-6">
      <Link href="/plans" className="text-[13px] underline">
        Benefit plans
      </Link>
      <div className="mt-2 flex items-end justify-between gap-4">
        <div>
          <p className="font-mono text-[11px] font-medium tracking-[0.08em] text-primary-text uppercase">{plan?.code}</p>
          <h1 className="mt-1 font-display text-xl font-medium tracking-[-0.01em] uppercase">{plan?.name ?? "Plan"}</h1>
        </div>
        {isAdmin &&
          (draft ? (
            <Link
              href={`/plans/${planId}/versions/${draft.id}`}
              className="inline-flex h-9 items-center rounded-sm bg-primary px-4 text-[13px] font-medium text-on-primary hover:bg-primary-hover"
            >
              Open draft v{draft.versionNo}
            </Link>
          ) : (
            <Button
              variant="primary"
              disabled={createDraft.isPending}
              onClick={() => createDraft.mutate(latestPublished?.id)}
            >
              {latestPublished ? `New draft from v${latestPublished.versionNo}` : "New draft"}
            </Button>
          ))}
      </div>

      {createDraft.error && (
        <p role="alert" className="mt-4 rounded-sm bg-danger-container px-3 py-2 text-[13px] text-danger">
          {createDraft.error instanceof ApiProblem && createDraft.error.code === "DRAFT_ALREADY_EXISTS"
            ? "This plan already has a draft. Reload the page to open it."
            : "The draft could not be created. Please try again."}
        </p>
      )}

      {items.length === 0 ? (
        <p className="mt-6 rounded-sm border border-line bg-surface p-6">This plan has no rule-set versions yet.</p>
      ) : (
        <table className="mt-6 w-full border border-line bg-surface text-[13px]">
          <thead className="bg-background text-left font-mono text-[11px] tracking-[0.08em] text-on-surface-muted uppercase">
            <tr className="h-8">
              <th className="px-3 font-medium">Version</th>
              <th className="px-3 font-medium">Status</th>
              <th className="px-3 font-medium">Effective from</th>
              <th className="px-3 font-medium">Change note</th>
              <th className="px-3 font-medium">Created</th>
              <th className="px-3 font-medium">Published</th>
            </tr>
          </thead>
          <tbody>
            {items.map((version) => (
              <tr key={version.id} className="h-9 border-t border-line">
                <td className="px-3 font-mono">
                  <Link className="underline" href={`/plans/${planId}/versions/${version.id}`}>
                    v{version.versionNo}
                  </Link>
                </td>
                <td className="px-3">
                  <VersionStatus status={version.status} />
                </td>
                <td className="px-3 font-mono">{formatDate(version.effectiveFrom)}</td>
                <td className="max-w-[320px] truncate px-3">{version.changeNote ?? "—"}</td>
                <td className="px-3">
                  {version.createdBy.fullName} · {formatDateTime(version.createdAt)}
                </td>
                <td className="px-3">
                  {version.publishedBy ? `${version.publishedBy.fullName} · ${formatDateTime(version.publishedAt)}` : "—"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

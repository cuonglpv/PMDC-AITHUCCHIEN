"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { LoadError, Loading } from "@/components/states/PageState";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { useSession } from "@/features/auth/use-session";
import { api } from "@/lib/api/client";
import { ApiProblem, unwrap } from "@/lib/api/problem";
import type { components } from "@/lib/api/schema";
import { formatDate } from "@/lib/format";
import { planKeys, useCategories, usePlans, useVersion } from "../queries";
import { VersionStatus } from "../VersionStatus";
import { RuleTable } from "./RuleTable";
import { errorsByRow, fromRules, newRow, TABS, toInput, unconfiguredCategories, type RuleRow } from "./rule-rows";

type VersionDetail = components["schemas"]["RuleSetVersionDetail"];

/** SCR-42. A draft is edited as one list and saved at once; a published version is read only (US19). */
export function RuleSetEditor({ planId, versionId }: { planId: string; versionId: string }) {
  const version = useVersion(planId, versionId);
  const categories = useCategories();
  const plans = usePlans();

  if (version.isPending || categories.isPending) {
    return <Loading label="Loading the rule set" />;
  }
  if (version.isError) {
    return <LoadError error={version.error} />;
  }
  if (categories.isError) {
    return <LoadError error={categories.error} />;
  }

  return (
    <EditorBody
      key={version.data.id + version.data.status}
      planId={planId}
      version={version.data}
      categories={categories.data.items}
      planName={plans.data?.items.find((plan) => plan.id === planId)?.name}
    />
  );
}

function EditorBody({
  planId,
  version,
  categories,
  planName,
}: {
  planId: string;
  version: VersionDetail;
  categories: components["schemas"]["BenefitCategory"][];
  planName: string | undefined;
}) {
  const state = useSession();
  const router = useRouter();
  const queryClient = useQueryClient();
  const isAdmin = state.status === "signedIn" && state.user.role === "ADMINISTRATOR";
  const editable = isAdmin && version.status === "DRAFT";

  const [rows, setRows] = useState<RuleRow[]>(() => fromRules(version.rules));
  const [changeNote, setChangeNote] = useState(version.changeNote ?? "");
  const [dirty, setDirty] = useState(false);
  const [tab, setTab] = useState(TABS[0]!.id);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [saved, setSaved] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [discarding, setDiscarding] = useState(false);

  // Leaving with unsaved rules would lose them; the browser asks first.
  useEffect(() => {
    if (!dirty) {
      return;
    }
    const warn = (event: BeforeUnloadEvent) => event.preventDefault();
    window.addEventListener("beforeunload", warn);
    return () => window.removeEventListener("beforeunload", warn);
  }, [dirty]);

  const refresh = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: planKeys.version(planId, version.id) }),
      queryClient.invalidateQueries({ queryKey: planKeys.versions(planId) }),
      queryClient.invalidateQueries({ queryKey: planKeys.plans }),
    ]);

  const save = useMutation({
    mutationFn: () =>
      unwrap(
        api.PUT("/plans/{planId}/versions/{versionId}/rules", {
          params: { path: { planId, versionId: version.id } },
          body: { rules: rows.map(toInput), changeNote },
        }),
      ),
    onMutate: () => {
      setProblem(null);
      setSaved(false);
    },
    onSuccess: () => {
      setDirty(false);
      setSaved(true);
    },
    onError: (error) => setProblem(error instanceof ApiProblem ? error : new ApiProblem(0, null)),
  });

  const discard = useMutation({
    mutationFn: () =>
      unwrap(
        api.POST("/plans/{planId}/versions/{versionId}/discard", {
          params: { path: { planId, versionId: version.id } },
        }),
      ),
    onSuccess: async () => {
      await refresh();
      router.push(`/plans/${planId}/versions`);
    },
  });

  const change = (key: string, field: keyof RuleRow, value: string) => {
    setRows((current) => current.map((row) => (row.key === key ? { ...row, [field]: value } : row)));
    setDirty(true);
    setSaved(false);
  };

  const activeTab = TABS.find((item) => item.id === tab)!;
  const tabRows = rows.filter((row) => row.ruleType === activeTab.type);
  const { byRow, general } = errorsByRow(rows, problem?.errors ?? []);
  const missing = unconfiguredCategories(rows, categories);

  return (
    <div className="p-6 pb-24">
      <Link href={`/plans/${planId}/versions`} className="text-[13px] underline">
        {planName ?? "Plan"} versions
      </Link>
      <div className="mt-2 flex items-center gap-3">
        <h1 className="font-display text-xl font-medium tracking-[-0.01em] uppercase">Rule set v{version.versionNo}</h1>
        <VersionStatus status={version.status} />
        {version.effectiveFrom && (
          <span className="font-mono text-[13px] text-on-surface-muted">
            Effective from {formatDate(version.effectiveFrom)}
          </span>
        )}
      </div>

      {!editable && (
        <p className="mt-3 text-[13px] text-on-surface-muted">
          {version.status === "PUBLISHED"
            ? "A published version cannot be changed. Create a new draft to supersede it."
            : version.status === "DISCARDED"
              ? "This draft was discarded."
              : "Only an Administrator edits rule sets."}
        </p>
      )}

      {editable && (
        <label className="mt-5 block max-w-xl text-xs font-medium text-on-surface-muted">
          Change note
          <textarea
            className="mt-1 block w-full rounded-sm border border-control-border bg-surface px-2 py-1 text-[13px] text-ink"
            rows={2}
            value={changeNote}
            onChange={(event) => {
              setChangeNote(event.target.value);
              setDirty(true);
            }}
          />
        </label>
      )}

      {editable && missing.length > 0 && (
        <p className="mt-4 rounded-sm bg-warning-container px-3 py-2 text-[13px] text-warning">
          Not covered or excluded yet: {missing.map((category) => category.name).join(", ")}. Publishing needs a rule for each.
        </p>
      )}

      {problem && (
        <div role="alert" className="mt-4 rounded-sm bg-danger-container px-3 py-2 text-[13px] text-danger">
          <p className="font-medium">{problem.problem?.title ?? "The rules could not be saved. Please try again."}</p>
          {general.map((message) => (
            <p key={message}>{message}</p>
          ))}
        </div>
      )}

      <div role="tablist" aria-label="Rule kinds" className="mt-6 flex border-b border-line">
        {TABS.map((item) => {
          const count = rows.filter((row) => row.ruleType === item.type).length;
          const hasError = rows.some((row) => row.ruleType === item.type && byRow.has(row.key));
          return (
            <button
              key={item.id}
              type="button"
              role="tab"
              id={`tab-${item.id}`}
              aria-selected={tab === item.id}
              aria-controls={`panel-${item.id}`}
              className={`-mb-px h-10 border-b-2 px-3 text-[13px] font-medium ${
                tab === item.id ? "border-primary text-ink" : "border-transparent text-on-surface-muted hover:text-ink"
              }`}
              onClick={() => setTab(item.id)}
            >
              {item.label} ({count}){hasError && <span className="ml-1 text-danger">· check</span>}
            </button>
          );
        })}
      </div>

      <div role="tabpanel" id={`panel-${activeTab.id}`} aria-labelledby={`tab-${activeTab.id}`} className="mt-4 space-y-3">
        <RuleTable
          type={activeTab.type}
          rows={tabRows}
          categories={categories}
          errors={byRow}
          editable={editable}
          onChange={change}
          onRemove={(key) => {
            setRows((current) => current.filter((row) => row.key !== key));
            setDirty(true);
          }}
        />
        {editable && !(activeTab.single && tabRows.length > 0) && (
          <Button
            onClick={() => {
              setRows((current) => [...current, newRow(activeTab.type)]);
              setDirty(true);
            }}
          >
            {activeTab.addLabel}
          </Button>
        )}
      </div>

      {editable && (
        <div className="fixed right-0 bottom-0 left-[240px] flex h-16 items-center justify-between gap-3 border-t border-line bg-surface px-6">
          <p className="text-[13px] text-on-surface-muted" aria-live="polite">
            {save.isPending ? "Saving…" : dirty ? "Unsaved changes" : saved ? "Saved" : "No changes"}
          </p>
          <div className="flex gap-2">
            <Button variant="ghost" onClick={() => setDiscarding(true)}>
              Discard draft
            </Button>
            <Button disabled={!dirty || save.isPending} onClick={() => save.mutate()}>
              Save draft
            </Button>
            <Button
              variant="primary"
              disabled={dirty}
              title={dirty ? "Save the draft before publishing" : undefined}
              onClick={() => setPublishing(true)}
            >
              Publish
            </Button>
          </div>
        </div>
      )}

      <PublishDialog
        open={publishing}
        onOpenChange={setPublishing}
        planId={planId}
        version={version}
        onPublished={async () => {
          setPublishing(false);
          await refresh();
        }}
      />

      <Dialog
        open={discarding}
        onOpenChange={setDiscarding}
        title={`Discard draft v${version.versionNo}?`}
        description="Its rules are kept for the record, and the plan can have a new draft."
      >
        {discard.isError && (
          <p role="alert" className="mb-3 text-[13px] text-danger">
            The draft could not be discarded. Please try again.
          </p>
        )}
        <div className="flex justify-end gap-2">
          <Button onClick={() => setDiscarding(false)}>Keep editing</Button>
          <Button variant="danger" disabled={discard.isPending} onClick={() => discard.mutate()}>
            Discard draft
          </Button>
        </div>
      </Dialog>
    </div>
  );
}

function PublishDialog({
  open,
  onOpenChange,
  planId,
  version,
  onPublished,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  planId: string;
  version: VersionDetail;
  onPublished: () => Promise<void>;
}) {
  const [effectiveFrom, setEffectiveFrom] = useState("");

  const publish = useMutation({
    mutationFn: () =>
      unwrap(
        api.POST("/plans/{planId}/versions/{versionId}/publish", {
          params: { path: { planId, versionId: version.id } },
          body: { effectiveFrom },
        }),
      ),
    onSuccess: onPublished,
  });

  const problem = publish.error instanceof ApiProblem ? publish.error : null;
  const messages = problem?.errors.map((error) => error.message) ?? [];

  return (
    <Dialog
      open={open}
      onOpenChange={onOpenChange}
      title={`Publish v${version.versionNo}`}
      description="Claims with a service date from this day use this version. A published version cannot be changed."
    >
      <form
        onSubmit={(event) => {
          event.preventDefault();
          publish.mutate();
        }}
      >
        <label className="block text-xs font-medium text-on-surface-muted" htmlFor="effective-from">
          Effective from
        </label>
        <input
          id="effective-from"
          type="date"
          required
          className="mt-1 h-9 w-full rounded-sm border border-control-border bg-surface px-2 text-[13px]"
          value={effectiveFrom}
          aria-invalid={problem ? true : undefined}
          aria-describedby={problem ? "publish-error" : undefined}
          onChange={(event) => setEffectiveFrom(event.target.value)}
        />
        {publish.isError && (
          <div id="publish-error" role="alert" className="mt-3 text-[13px] text-danger">
            <p className="font-medium">{problem?.problem?.title ?? "The version could not be published. Please try again."}</p>
            {messages.map((message) => (
              <p key={message}>{message}</p>
            ))}
          </div>
        )}
        <div className="mt-5 flex justify-end gap-2">
          <Button onClick={() => onOpenChange(false)}>Cancel</Button>
          <Button type="submit" variant="primary" disabled={!effectiveFrom || publish.isPending}>
            Publish v{version.versionNo}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

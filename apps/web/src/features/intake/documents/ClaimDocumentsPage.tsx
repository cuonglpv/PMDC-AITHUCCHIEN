"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";
import { FallbackBanner } from "@/components/states/FallbackBanner";
import { LoadError, Loading } from "@/components/states/PageState";
import { Button } from "@/components/ui/Button";
import { Dropzone } from "@/components/ui/Dropzone";
import { StatusPill } from "@/components/ui/StatusPill";
import { api } from "@/lib/api/client";
import { ApiProblem, expectNoContent, unwrap } from "@/lib/api/problem";
import type { ClaimDetail, Document, Warning } from "@/lib/api/types";
import { formatDate, formatDateTime, formatMoney } from "@/lib/format";
import { INTAKE_FALLBACK_LABEL } from "@/lib/labels/ai";
import { CHANNEL_LABEL, CLAIM_STATUS_LABEL, CLAIM_STATUS_TONE, WARNING_LABEL } from "@/lib/labels/claims";
import { UPLOAD_ACCEPT } from "@/lib/labels/documents";
import { IntakeStepper } from "../IntakeStepper";
import { selectionFromClaim } from "../new-claim/claim-form";
import { PolicyCard, Row } from "../PolicyCard";
import {
  isFallbackActive,
  isListBehind,
  isWaitingForAi,
  submitBlock,
  SUBMIT_BLOCK_LABEL,
  withLiveState,
} from "../processing";
import { intakeKeys, useClaim, useClaimStatus, useClaimWarnings, useDocuments } from "../queries";
import { DocumentRow, problemMessage, UploadRow } from "./FileRows";
import { PageCard } from "./PageCard";
import { useUploads } from "./use-uploads";

/**
 * SCR-13: step 3 of New claim while the claim is a draft (upload, page check, submit, US03), and a read-only view of
 * the claim and its documents once submitted. What the officer may do comes from `allowedActions`.
 */
export function ClaimDocumentsPage({ claimId }: { claimId: string }) {
  const claim = useClaim(claimId);

  if (claim.isPending) {
    return <Loading label="Loading the claim" />;
  }
  if (claim.isError) {
    return <LoadError error={claim.error} />;
  }
  return <ClaimDocuments claim={claim.data} />;
}

function ClaimDocuments({ claim }: { claim: ClaimDetail }) {
  const claimId = claim.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const documents = useDocuments(claimId);
  const status = useClaimStatus(claimId);
  const warnings = useClaimWarnings(claimId);
  const [removeErrors, setRemoveErrors] = useState<Record<string, ApiProblem>>({});

  const snapshot = status.data;
  const claimStatus = snapshot?.status ?? claim.status;
  const draft = claimStatus === "DRAFT";
  const canUpload = draft && claim.allowedActions.includes("UPLOAD_DOCUMENT");
  // Submit is on offer when the API allows it, or names only why it is not open yet (a file still converting, nothing
  // ready). The detail is not polled, so the button must not wait for it to change; its disabled state follows the poll.
  const canSubmit =
    draft &&
    (claim.allowedActions.includes("SUBMIT_CLAIM") ||
      claim.blockers.some((blocker) => blocker.code === "UPLOAD_IN_PROGRESS" || blocker.code === "NO_READY_DOCUMENT"));
  const canEdit = draft && claim.allowedActions.includes("EDIT_CLAIM");

  // The poll is the fresher source: when it knows of a converted file, new pages or a new claim status, reload those.
  // After submission that is how the read-only view learns the extraction finished (Needs verification or Ready for
  // review) and whether it fell back to manual work.
  useEffect(() => {
    if (!snapshot) {
      return;
    }
    if (documents.data && isListBehind(documents.data.items, snapshot)) {
      void queryClient.invalidateQueries({ queryKey: intakeKeys.documents(claimId) });
    }
    if (snapshot.status !== claim.status) {
      void queryClient.invalidateQueries({ queryKey: intakeKeys.claim(claimId) });
    }
  }, [snapshot, documents.data, claim.status, claimId, queryClient]);

  const onStored = useCallback(
    (document: Document) => {
      queryClient.setQueryData(intakeKeys.documents(claimId), (list: { items: Document[] } | undefined) =>
        list ? { items: [...list.items.filter((item) => item.id !== document.id), document] } : list,
      );
      // The stored file is converting: the status poll starts and runs until it is ready or rejected.
      void queryClient.invalidateQueries({ queryKey: intakeKeys.status(claimId) });
    },
    [claimId, queryClient],
  );
  const uploads = useUploads(claimId, onStored);
  // Once the claim is submitted, a file the API refused earlier is no longer worth showing.
  const pending = canUpload ? uploads.uploads : [];

  const remove = useMutation({
    mutationFn: (documentId: string) =>
      expectNoContent(
        api.DELETE("/claims/{claimId}/documents/{documentId}", { params: { path: { claimId, documentId } } }),
      ),
    onMutate: (documentId) =>
      setRemoveErrors((current) => {
        const next = { ...current };
        delete next[documentId];
        return next;
      }),
    onSuccess: (_, documentId) => {
      queryClient.setQueryData(intakeKeys.documents(claimId), (list: { items: Document[] } | undefined) =>
        list ? { items: list.items.filter((item) => item.id !== documentId) } : list,
      );
      void queryClient.invalidateQueries({ queryKey: intakeKeys.status(claimId) });
    },
    onError: (error, documentId) =>
      setRemoveErrors((current) => ({
        ...current,
        [documentId]: error instanceof ApiProblem ? error : new ApiProblem(0, null),
      })),
  });

  const submit = useMutation({
    mutationFn: () => unwrap(api.POST("/claims/{claimId}/submit", { params: { path: { claimId } } })),
    onSuccess: (next) => {
      queryClient.setQueryData(intakeKeys.status(claimId), next);
      queryClient.setQueryData<Warning[]>(intakeKeys.warnings(claimId), []);
      void queryClient.invalidateQueries({ queryKey: intakeKeys.claim(claimId) });
      void queryClient.invalidateQueries({ queryKey: intakeKeys.documents(claimId) });
      void queryClient.invalidateQueries({ queryKey: intakeKeys.queues });
    },
    // A 409 means the screen was behind the server: refresh what it shows.
    onError: () => void queryClient.invalidateQueries({ queryKey: intakeKeys.status(claimId) }),
  });

  const files = withLiveState(documents.data?.items ?? [], snapshot);
  const pages = files
    .filter((document) => document.status === "READY")
    .flatMap((document) => document.pages.map((page) => ({ page, fileName: document.originalFilename })))
    .sort((a, b) => a.page.claimPageNo - b.page.claimPageNo);
  const block = submitBlock(uploads.running, files);
  const converting = files.filter((document) => document.status === "UPLOADING" || document.status === "CONVERTING");
  const waiting = pages.filter(({ page }) => isWaitingForAi(page)).length;
  const editHref = `/intake/new?claimId=${claimId}`;

  return (
    <div className={draft ? "pb-24" : ""}>
      <header className="px-6 pt-6">
        {draft ? (
          <>
            <div className="flex flex-wrap items-baseline gap-x-4 gap-y-1">
              <h1 className="font-display text-xl font-medium tracking-[0.08em] uppercase">New claim</h1>
              <span className="font-mono text-[13px] text-on-surface-muted">{claim.claimNumber} · Draft</span>
            </div>
            <IntakeStepper current={3} done={[1, 2]} onStep={canEdit ? () => router.push(editHref) : undefined} />
          </>
        ) : (
          <>
            <Link href="/intake?tab=submitted" className="text-[13px] underline">
              My submissions
            </Link>
            <div className="mt-2 flex flex-wrap items-center gap-3">
              <h1 className="font-mono text-xl font-medium">{claim.claimNumber}</h1>
              <StatusPill tone={CLAIM_STATUS_TONE[claimStatus]}>{CLAIM_STATUS_LABEL[claimStatus]}</StatusPill>
            </div>
            <p className="mt-1 text-[13px] text-on-surface-muted">
              {claim.submittedAt ? `Submitted ${formatDateTime(claim.submittedAt)}. ` : ""}
              This claim can no longer be changed here.
            </p>
          </>
        )}
      </header>

      {isFallbackActive(claim, snapshot) && (
        <div className="mt-4">
          <FallbackBanner>{draft ? INTAKE_FALLBACK_LABEL.DRAFT : INTAKE_FALLBACK_LABEL.SUBMITTED}</FallbackBanner>
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 p-6 xl:grid-cols-[minmax(0,1fr)_320px]">
        <div className="min-w-0 space-y-6">
          {draft && warnings.data && warnings.data.length > 0 && (
            <div role="status" className="rounded-sm bg-warning-container px-4 py-3 text-[13px] text-warning">
              <ul className="space-y-1">
                {warnings.data.map((warning) => (
                  <li key={warning.code} className="flex items-start gap-2">
                    <span className="material-symbols-outlined text-[16px]" aria-hidden="true">
                      warning
                    </span>
                    {WARNING_LABEL[warning.code] ?? warning.message}
                  </li>
                ))}
              </ul>
              <button
                type="button"
                className="mt-2 underline"
                onClick={() => queryClient.setQueryData<Warning[]>(intakeKeys.warnings(claimId), [])}
              >
                Dismiss
              </button>
            </div>
          )}

          {submit.isSuccess && (
            <p role="status" className="rounded-sm bg-success-container px-4 py-3 text-[13px] text-success">
              {claimStatus === "PROCESSING" ? "Claim submitted. It is now processing." : "Claim submitted."}
            </p>
          )}

          {canUpload && (
            <Dropzone
              label="Add files"
              accept={UPLOAD_ACCEPT}
              onFiles={uploads.add}
              hint={
                <>
                  JPG, PNG, PDF, DOCX or XLSX · up to <span className="font-mono">20 MB</span> per file ·{" "}
                  <span className="font-mono">30</span> pages per claim
                </>
              }
            />
          )}

          <section aria-labelledby="files-heading">
            <h2 id="files-heading" className="mb-2 text-base font-semibold">
              Files
            </h2>
            {documents.isPending ? (
              <Loading label="Loading the files" />
            ) : documents.isError ? (
              <LoadError error={documents.error} />
            ) : pending.length === 0 && files.length === 0 ? (
              <p className="rounded-sm border border-line bg-surface p-4 text-[13px] text-on-surface-muted">
                {canUpload ? "No files yet. Add the claim's documents above." : "No documents."}
              </p>
            ) : (
              <ul className="rounded-sm border border-line bg-surface">
                {files.map((document) => (
                  <DocumentRow
                    key={document.id}
                    document={document}
                    removable={canUpload}
                    removing={remove.isPending && remove.variables === document.id}
                    removeError={removeErrors[document.id]}
                    onRemove={() => remove.mutate(document.id)}
                  />
                ))}
                {pending.map((upload) => (
                  <UploadRow
                    key={upload.key}
                    upload={upload}
                    onCancel={() => uploads.cancel(upload.key)}
                    onRetry={() => uploads.retry(upload.key)}
                    onDismiss={() => uploads.dismiss(upload.key)}
                  />
                ))}
              </ul>
            )}
          </section>

          {pages.length > 0 && (
            <section aria-labelledby="pages-heading">
              <h2 id="pages-heading" className="mb-2 text-base font-semibold">
                Page check
              </h2>
              <ul className="grid grid-cols-[repeat(auto-fill,minmax(140px,1fr))] gap-3">
                {pages.map(({ page, fileName }) => (
                  <PageCard key={page.id} claimId={claimId} page={page} fileName={fileName} canChooseType={canUpload} />
                ))}
              </ul>
            </section>
          )}
        </div>

        <aside>
          <PolicyCard selection={selectionFromClaim(claim)} serviceDate={claim.serviceDate} showBalance={draft}>
            <Row term="Service date">
              <span className="font-mono">{formatDate(claim.serviceDate)}</span>
            </Row>
            <Row term="Provider">{claim.providerName}</Row>
            <Row term="Claimed">
              <span className="font-mono">{formatMoney(claim.claimedTotal)}</span>
            </Row>
            <Row term="Channel">{CHANNEL_LABEL[claim.channel]}</Row>
          </PolicyCard>
        </aside>
      </div>

      {draft && (canSubmit || canEdit) && (
        <div className="fixed right-0 bottom-0 left-[240px] flex h-16 items-center justify-between gap-4 border-t border-line bg-surface px-6">
          <div className="min-w-0 text-[13px]" aria-live="polite">
            {submit.isError ? (
              <p role="alert" className="text-danger">
                {problemMessage(
                  submit.error instanceof ApiProblem ? submit.error : new ApiProblem(0, null),
                  "The claim could not be submitted. Please try again.",
                )}
              </p>
            ) : (
              <p id="submit-state" className="flex items-center gap-2 text-on-surface-muted">
                <span
                  className={`inline-block size-2 rounded-full ${block ? "bg-warning" : "bg-success"}`}
                  aria-hidden="true"
                />
                {progressText(uploads.running, converting.length, waiting, block)}
              </p>
            )}
          </div>
          <div className="flex shrink-0 gap-2">
            {canEdit && <Button onClick={() => router.push(editHref)}>Back</Button>}
            {canSubmit && (
              <Button
                variant="primary"
                disabled={block !== null || submit.isPending}
                aria-describedby="submit-state"
                onClick={() => submit.mutate()}
              >
                {submit.isPending ? "Submitting…" : "Submit"}
              </Button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function progressText(uploading: number, converting: number, waiting: number, block: keyof typeof SUBMIT_BLOCK_LABEL | null) {
  const parts: string[] = [];
  if (uploading > 0) {
    parts.push(`Uploading ${uploading} ${uploading === 1 ? "file" : "files"}`);
  }
  if (converting > 0) {
    parts.push(`${converting} ${converting === 1 ? "file" : "files"} converting`);
  }
  if (waiting > 0) {
    parts.push(`${waiting} ${waiting === 1 ? "page" : "pages"} waiting for AI check`);
  }
  const reason = block ? SUBMIT_BLOCK_LABEL[block] : "Ready to submit";
  return parts.length > 0 ? `${parts.join(" · ")}. ${reason}.` : `${reason}.`;
}

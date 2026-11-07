import type { AiRunState, AiTask, ClaimDetail, ClaimStatusSnapshot, Document, DocumentPage } from "@/lib/api/types";

/** Typed as a number, not the literal 3000, so a test can poll faster. */
export const POLL_INTERVAL_MS: number = 3_000;

const pageWaiting = (page: { aiStatus: DocumentPage["aiStatus"] }) => page.aiStatus === "QUEUED" || page.aiStatus === "READING";
const fileBusy = (document: { status: Document["status"] }) =>
  document.status === "UPLOADING" || document.status === "CONVERTING";

/** The AI work that SCR-13 waits for: the page check before submission and the field extraction after it. */
const INTAKE_TASKS: readonly AiTask[] = ["PAGE_CHECK", "FIELD_EXTRACTION"];
const runActive = (run: AiRunState) => run.status === "QUEUED" || run.status === "RUNNING";

/**
 * Whether to keep polling: a file is still converting, a page check or the field extraction is queued or running, or
 * the submitted claim is still `PROCESSING`. Polling stops once nothing runs.
 */
export function isProcessing(snapshot: ClaimStatusSnapshot): boolean {
  return (
    snapshot.status === "PROCESSING" ||
    snapshot.documents.some((document) => fileBusy(document) || document.pages.some(pageWaiting)) ||
    snapshot.aiRuns.some((run) => INTAKE_TASKS.includes(run.task) && runActive(run))
  );
}

/** The most recently requested run of a task in the poll, or undefined when there is none. */
export function latestRun(snapshot: ClaimStatusSnapshot | undefined, task: AiTask): AiRunState | undefined {
  return snapshot?.aiRuns
    .filter((run) => run.task === task)
    .reduce<AiRunState | undefined>(
      (latest, run) => (!latest || Date.parse(run.requestedAt) > Date.parse(latest.requestedAt) ? run : latest),
      undefined,
    );
}

/**
 * The fallback banner (US04): the API says an AI run failed (`fallbackActive` on the claim or the poll), or the latest
 * page check failed. The work continues by hand and nothing is blocked.
 */
export function isFallbackActive(claim: ClaimDetail, snapshot: ClaimStatusSnapshot | undefined): boolean {
  return (
    claim.ai.fallbackActive ||
    Boolean(snapshot?.fallbackActive) ||
    latestRun(snapshot, "PAGE_CHECK")?.status === "FAILED"
  );
}

/** A page whose AI check has not finished, shown as "Waiting for AI check". */
export function isWaitingForAi(page: DocumentPage): boolean {
  return pageWaiting(page);
}

/**
 * The documents as listed, with the fresher status from the poll laid over them: file status, rejection reason, and
 * each page's type, quality and AI status. Removed files are left out.
 */
export function withLiveState(documents: Document[], snapshot: ClaimStatusSnapshot | undefined): Document[] {
  const live = new Map(snapshot?.documents.map((state) => [state.id, state]));
  return documents
    .map((document) => {
      const state = live.get(document.id);
      if (!state) {
        return document;
      }
      const pages = new Map(state.pages.map((page) => [page.id, page]));
      return {
        ...document,
        status: state.status,
        rejectionReason: state.rejectionReason,
        pages: document.pages.map((page) => {
          const pageState = pages.get(page.id);
          return pageState
            ? { ...page, pageType: pageState.pageType, quality: pageState.quality, aiStatus: pageState.aiStatus }
            : page;
        }),
      };
    })
    .filter((document) => document.status !== "REMOVED");
}

/**
 * True when the poll knows something the document list does not yet: a file finished converting or gained pages, or
 * a page check came back. The list then reloads for what only it carries, such as who set a page type.
 */
export function isListBehind(documents: Document[], snapshot: ClaimStatusSnapshot): boolean {
  const listed = new Map(documents.map((document) => [document.id, document]));
  return snapshot.documents.some((state) => {
    const document = listed.get(state.id);
    if (!document) {
      return state.status !== "REMOVED";
    }
    if (document.status !== state.status || document.pages.length !== state.pages.length) {
      return true;
    }
    const pages = new Map(document.pages.map((page) => [page.id, page]));
    return state.pages.some((pageState) => {
      const page = pages.get(pageState.id);
      return (
        !page ||
        page.aiStatus !== pageState.aiStatus ||
        page.quality !== pageState.quality ||
        page.pageType !== pageState.pageType
      );
    });
  });
}

export type SubmitBlock = "UPLOADING" | "CONVERTING" | "NO_READY_DOCUMENT";

/**
 * Why Submit is not open yet, so the button can say so. It mirrors the API's own 409 answers (`UPLOAD_IN_PROGRESS`,
 * `NO_READY_DOCUMENT`), which stay the real check. A page check still running does not block submission.
 */
export function submitBlock(uploadsRunning: number, documents: Document[]): SubmitBlock | null {
  if (uploadsRunning > 0) {
    return "UPLOADING";
  }
  if (documents.some(fileBusy)) {
    return "CONVERTING";
  }
  if (!documents.some((document) => document.status === "READY")) {
    return "NO_READY_DOCUMENT";
  }
  return null;
}

export const SUBMIT_BLOCK_LABEL: Record<SubmitBlock, string> = {
  UPLOADING: "Wait for the uploads to finish",
  CONVERTING: "Wait for the files to finish converting",
  NO_READY_DOCUMENT: "Add at least one document that converts",
};

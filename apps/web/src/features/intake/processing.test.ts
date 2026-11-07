import { describe, expect, it } from "vitest";
import { aiRun, claimDetail, document, page, snapshotOf } from "@/test/intake";
import { isFallbackActive, isListBehind, isProcessing, latestRun, submitBlock, withLiveState } from "./processing";

const ready = document({ pages: [page({ aiStatus: "DONE", quality: "READABLE" })] });
const converting = document({ id: "d2", status: "CONVERTING", pageCount: null, pages: [] });

describe("status polling", () => {
  it("polls while a file converts", () => {
    expect(isProcessing(snapshotOf([ready, converting]))).toBe(true);
  });

  it("polls while a page check is queued or running", () => {
    expect(isProcessing(snapshotOf([document({ pages: [page({ aiStatus: "QUEUED" })] })]))).toBe(true);
    expect(isProcessing(snapshotOf([document({ pages: [page({ aiStatus: "READING" })] })]))).toBe(true);
    expect(
      isProcessing(
        snapshotOf([ready], {
          aiRuns: [
            {
              id: "r",
              task: "PAGE_CHECK",
              status: "RUNNING",
              errorCode: null,
              modelName: "m",
              modelVersion: "1",
              requestedAt: "2026-10-13T09:00:00Z",
              startedAt: null,
              finishedAt: null,
            },
          ],
        }),
      ),
    ).toBe(true);
  });

  it("polls while the submitted claim is processing or its field extraction is queued or running", () => {
    expect(isProcessing(snapshotOf([ready], { status: "PROCESSING" }))).toBe(true);
    for (const status of ["QUEUED", "RUNNING"] as const) {
      const extraction = aiRun({ task: "FIELD_EXTRACTION", status });
      expect(isProcessing(snapshotOf([ready], { status: "NEEDS_VERIFICATION", aiRuns: [extraction] }))).toBe(true);
    }
  });

  it("stops once the extraction is over and the claim has left Processing", () => {
    for (const [status, run] of [
      ["NEEDS_VERIFICATION", "SUCCEEDED"],
      ["READY_FOR_REVIEW", "SUCCEEDED"],
      ["NEEDS_VERIFICATION", "FAILED"],
    ] as const) {
      const extraction = aiRun({ task: "FIELD_EXTRACTION", status: run });
      expect(isProcessing(snapshotOf([ready], { status, aiRuns: [aiRun(), extraction] }))).toBe(false);
    }
  });

  it("does not poll for AI work that is not part of intake", () => {
    expect(isProcessing(snapshotOf([ready], { aiRuns: [aiRun({ task: "BENEFIT_MAPPING", status: "QUEUED" })] }))).toBe(
      false,
    );
  });

  it("stops once every file is ready or rejected and every page check is over", () => {
    const rejected = document({ id: "d3", status: "REJECTED", rejectionReason: "More than 30 pages", pages: [] });
    const failed = document({ id: "d4", pages: [page({ id: "p4", aiStatus: "FAILED" })] });
    expect(isProcessing(snapshotOf([ready, rejected, failed]))).toBe(false);
    expect(isProcessing(snapshotOf([]))).toBe(false);
  });
});

describe("documents with the polled state", () => {
  it("takes file and page status from the poll and leaves removed files out", () => {
    const listed = [converting, document({ id: "d5" })];
    const snapshot = snapshotOf([
      document({ id: "d2", status: "REJECTED", rejectionReason: "The file could not be converted", pages: [] }),
      document({ id: "d5", status: "REMOVED" }),
    ]);

    const files = withLiveState(listed, snapshot);

    expect(files).toHaveLength(1);
    expect(files[0]).toMatchObject({ id: "d2", status: "REJECTED", rejectionReason: "The file could not be converted" });
  });

  it("knows when the list is behind the poll", () => {
    const nowReady = document({ id: "d2", status: "READY", pageCount: 2, pages: [page(), page({ id: "p2" })] });
    expect(isListBehind([converting], snapshotOf([nowReady]))).toBe(true);
    expect(isListBehind([nowReady], snapshotOf([nowReady]))).toBe(false);
    expect(isListBehind([], snapshotOf([document({ status: "REMOVED" })]))).toBe(false);
    const checked = document({ pages: [page({ aiStatus: "DONE", quality: "LOW_QUALITY", pageType: "RECEIPT" })] });
    expect(isListBehind([document()], snapshotOf([checked]))).toBe(true);
  });
});

describe("submit", () => {
  it("waits for uploads, then conversions, and needs one ready document", () => {
    expect(submitBlock(1, [ready])).toBe("UPLOADING");
    expect(submitBlock(0, [ready, converting])).toBe("CONVERTING");
    expect(submitBlock(0, [document({ status: "REJECTED", rejectionReason: "x", pages: [] })])).toBe("NO_READY_DOCUMENT");
    expect(submitBlock(0, [])).toBe("NO_READY_DOCUMENT");
  });

  it("does not wait for a page check", () => {
    expect(submitBlock(0, [document({ pages: [page({ aiStatus: "QUEUED" })] })])).toBeNull();
  });
});

describe("fallback", () => {
  const failed = aiRun({ id: "r1", status: "FAILED", errorCode: "MODEL_ERROR", requestedAt: "2026-10-13T09:00:00Z" });
  const later = aiRun({ id: "r2", status: "SUCCEEDED", requestedAt: "2026-10-13T09:05:00Z" });

  it("finds the latest run of a task", () => {
    const snapshot = snapshotOf([], { aiRuns: [later, failed, aiRun({ id: "r3", task: "FIELD_EXTRACTION" })] });
    expect(latestRun(snapshot, "PAGE_CHECK")?.id).toBe("r2");
    expect(latestRun(snapshot, "FIELD_EXTRACTION")?.id).toBe("r3");
    expect(latestRun(snapshot, "SUMMARY_DRAFT")).toBeUndefined();
    expect(latestRun(undefined, "PAGE_CHECK")).toBeUndefined();
  });

  it("is on when the claim or the poll says so, or when the latest page check failed", () => {
    const claim = claimDetail();
    expect(isFallbackActive(claim, snapshotOf([]))).toBe(false);
    expect(isFallbackActive(claim, undefined)).toBe(false);
    expect(isFallbackActive(claimDetail({ ai: { fallbackActive: true, clinicalCheck: "NOT_RUN" } }), undefined)).toBe(true);
    expect(isFallbackActive(claim, snapshotOf([], { fallbackActive: true }))).toBe(true);
    expect(isFallbackActive(claim, snapshotOf([], { aiRuns: [failed] }))).toBe(true);
    expect(isFallbackActive(claim, snapshotOf([], { aiRuns: [failed, later] }))).toBe(false);
  });
});

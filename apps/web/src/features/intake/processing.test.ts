import { describe, expect, it } from "vitest";
import { document, page, snapshotOf } from "@/test/intake";
import { isListBehind, isProcessing, submitBlock, withLiveState } from "./processing";

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

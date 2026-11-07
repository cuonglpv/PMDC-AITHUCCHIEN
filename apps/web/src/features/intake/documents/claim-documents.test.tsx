import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import type { ClaimDetail, ClaimStatusSnapshot, Document, Warning } from "@/lib/api/types";
import { CLAIM_ID, claimDetail, document, page, policySummary, POLICY_ID, snapshotOf } from "@/test/intake";
import { API, authSession, problem, server } from "@/test/server";
import { intakeKeys } from "../queries";
import { ClaimDocumentsPage } from "./ClaimDocumentsPage";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push, replace: vi.fn() }) }));

const CLAIM = `${API}/claims/${CLAIM_ID}`;

const readyInvoice = document({
  pages: [page({ pageType: "INVOICE", pageTypeSource: "AI", quality: "READABLE", aiStatus: "DONE" })],
});

/** The server side of one claim: its detail, documents and poll answer, changeable during a test. */
const state: { claim: ClaimDetail; documents: Document[]; snapshot: () => ClaimStatusSnapshot; statusCalls: number } = {
  claim: claimDetail(),
  documents: [],
  snapshot: () => snapshotOf(state.documents),
  statusCalls: 0,
};

const created: string[] = [];
const revoked: string[] = [];

beforeEach(() => {
  push.mockClear();
  state.claim = claimDetail();
  state.documents = [readyInvoice];
  state.snapshot = () => snapshotOf(state.documents, { status: state.claim.status });
  state.statusCalls = 0;
  created.length = 0;
  revoked.length = 0;
  let next = 0;
  // jsdom has no blob URLs; these record what the thumbnails create and revoke.
  Object.assign(URL, {
    createObjectURL: () => {
      next += 1;
      created.push(`blob:page-${next}`);
      return `blob:page-${next}`;
    },
    revokeObjectURL: (url: string) => revoked.push(url),
  });
  server.use(
    http.get(CLAIM, () => HttpResponse.json(state.claim)),
    http.get(`${CLAIM}/documents`, () => HttpResponse.json({ items: state.documents })),
    http.get(`${CLAIM}/status`, () => {
      state.statusCalls += 1;
      return HttpResponse.json(state.snapshot());
    }),
    http.get(`${CLAIM}/pages/:pageId/image`, () =>
      HttpResponse.arrayBuffer(new Uint8Array([137, 80, 78, 71]).buffer, { headers: { "Content-Type": "image/png" } }),
    ),
    http.get(`${API}/policies/${POLICY_ID}/summary`, () => HttpResponse.json(policySummary({ asOfDate: "2026-10-12" }))),
  );
});

function renderPage(warnings?: Warning[]) {
  session.start(authSession("INTAKE_OFFICER"));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  if (warnings) {
    client.setQueryData(intakeKeys.warnings(CLAIM_ID), warnings);
  }
  const view = render(
    <QueryClientProvider client={client}>
      <ClaimDocumentsPage claimId={CLAIM_ID} />
    </QueryClientProvider>,
  );
  return { client, ...view };
}

const submitButton = () => screen.getByRole("button", { name: "Submit" });

describe("Documents and submit (SCR-13)", () => {
  it("shows a skeleton while the claim loads", async () => {
    renderPage();
    expect(screen.getByLabelText("Loading the claim")).toBeTruthy();
    await screen.findByRole("listitem", { name: "hoa-don.pdf" });
  });

  it("shows the draft's files, its pages with thumbnails, and the claim beside them", async () => {
    renderPage();

    const file = await screen.findByRole("listitem", { name: "hoa-don.pdf" });
    expect(within(file).getByText("1 page")).toBeTruthy();
    const card = screen.getByRole("listitem", { name: "Page 1" });
    expect(within(card).getByText("Invoice")).toBeTruthy();
    expect(within(card).getByText("AI")).toBeTruthy();
    expect(within(card).getByText("Readable")).toBeTruthy();
    await waitFor(() => expect(within(card).getByRole("img").getAttribute("src")).toBe("blob:page-1"));

    const aside = screen.getByRole("region", { name: "Member and policy" });
    expect(within(aside).getByText("Nguyễn Văn An")).toBeTruthy();
    expect(within(aside).getByText("1,400,000 ₫")).toBeTruthy();
    expect(await within(aside).findByText("800,000 ₫")).toBeTruthy();
    expect(screen.getByText("Ready to submit.")).toBeTruthy();
    expect(submitButton()).toHaveProperty("disabled", false);
  });

  it("revokes each thumbnail's blob URL when it leaves the screen", async () => {
    const { unmount } = renderPage();
    await waitFor(() => expect(created).toEqual(["blob:page-1"]));

    unmount();

    expect(revoked).toEqual(["blob:page-1"]);
  });

  it("shows each upload's progress, then its conversion, and polls until the pages are ready", async () => {
    state.documents = [];
    let release!: () => void;
    const stored = new Promise<void>((resolve) => (release = resolve));
    const converting = document({
      id: "d-new",
      originalFilename: "don-thuoc.png",
      fileFormat: "PNG",
      status: "CONVERTING",
      pageCount: null,
      pages: [],
    });
    server.use(
      http.post(`${CLAIM}/documents`, async () => {
        await stored;
        state.documents = [converting];
        return HttpResponse.json(converting, { status: 201 });
      }),
    );
    renderPage();
    const user = userEvent.setup();

    await user.upload(
      await screen.findByLabelText("Add files"),
      new File(["png"], "don-thuoc.png", { type: "image/png" }),
    );

    expect(await screen.findByRole("progressbar", { name: "Uploading don-thuoc.png" })).toBeTruthy();
    expect(submitButton()).toHaveProperty("disabled", true);
    expect(screen.getByText(/Wait for the uploads to finish/)).toBeTruthy();

    release();
    await waitFor(() => expect(screen.queryByRole("progressbar")).toBeNull());
    expect(within(screen.getByRole("listitem", { name: "don-thuoc.png" })).getByText("Converting…")).toBeTruthy();
    expect(await screen.findByText(/Wait for the files to finish converting/)).toBeTruthy();
    expect(submitButton()).toHaveProperty("disabled", true);

    // The next poll, 3 seconds later, finds the file converted and its page waiting for the AI check.
    state.documents = [
      { ...converting, status: "READY", pageCount: 1, pages: [page({ id: "p-new", documentId: "d-new" })] },
    ];
    const card = await screen.findByRole("listitem", { name: "Page 1" }, { timeout: 5_000 });
    expect(within(card).getByText("Waiting for AI check")).toBeTruthy();
    expect(screen.getByText(/1 page waiting for AI check. Ready to submit./)).toBeTruthy();
    expect(submitButton()).toHaveProperty("disabled", false);
    expect(state.statusCalls).toBeGreaterThanOrEqual(2);
  }, 10_000);

  it.each([
    [413, "FILE_TOO_LARGE", "This file is larger than 20 MB. Compress or split it, then upload it again."],
    [415, "UNSUPPORTED_FILE_TYPE", "This file type is not accepted. Use JPG, PNG, PDF, DOCX or XLSX."],
  ] as const)("shows a %s refusal next to the file", async (status, code, message) => {
    server.use(http.post(`${CLAIM}/documents`, () => problem(status, code)));
    renderPage();
    const user = userEvent.setup({ applyAccept: false });

    await user.upload(await screen.findByLabelText("Add files"), new File(["x"], "ghi-chu.pdf", { type: "application/pdf" }));

    const row = await screen.findByRole("listitem", { name: "ghi-chu.pdf" });
    expect(await within(row).findByText(message)).toBeTruthy();
    expect(within(row).queryByRole("button", { name: "Retry ghi-chu.pdf" })).toBeNull();
    await user.click(within(row).getByRole("button", { name: "Dismiss ghi-chu.pdf" }));
    expect(screen.queryByRole("listitem", { name: "ghi-chu.pdf" })).toBeNull();
  });

  it.each([
    ["a file over 20 MB", "scan-lon.pdf", 20 * 1024 * 1024 + 1, "This file is larger than 20 MB. Compress or split it, then upload it again."],
    ["a file type the API never takes", "ghi-chu.txt", 10, "This file type is not accepted. Use JPG, PNG, PDF, DOCX or XLSX."],
  ])("refuses %s in the browser, without sending it", async (_, name, size, message) => {
    let sent = 0;
    server.use(
      http.post(`${CLAIM}/documents`, () => {
        sent += 1;
        return HttpResponse.json(document(), { status: 201 });
      }),
    );
    renderPage();
    const user = userEvent.setup({ applyAccept: false });
    const big = new File(["x"], name);
    Object.defineProperty(big, "size", { value: size });

    await user.upload(await screen.findByLabelText("Add files"), big);

    const row = await screen.findByRole("listitem", { name });
    expect(within(row).getByText(message)).toBeTruthy();
    expect(within(row).queryByRole("progressbar")).toBeNull();
    expect(within(row).queryByRole("button", { name: `Retry ${name}` })).toBeNull();
    expect(sent).toBe(0);
  });

  it("does not offer Retry for a bare 413 from the proxy", async () => {
    server.use(http.post(`${CLAIM}/documents`, () => new HttpResponse(null, { status: 413 })));
    renderPage();
    const user = userEvent.setup();

    await user.upload(await screen.findByLabelText("Add files"), new File(["x"], "hoa-don-2.pdf", { type: "application/pdf" }));

    const row = await screen.findByRole("listitem", { name: "hoa-don-2.pdf" });
    expect(await within(row).findByText("This file is larger than 20 MB. Compress or split it, then upload it again.")).toBeTruthy();
    expect(within(row).queryByRole("button", { name: "Retry hoa-don-2.pdf" })).toBeNull();
  });

  it("takes files dropped on the drop area", async () => {
    let uploaded = 0;
    server.use(
      http.post(`${CLAIM}/documents`, () => {
        uploaded += 1;
        return HttpResponse.json(document({ id: `d${uploaded}`, status: "CONVERTING", pages: [] }), { status: 201 });
      }),
    );
    renderPage();
    const zone = (await screen.findByLabelText("Add files")).closest("label")!;

    fireEvent.drop(zone, {
      dataTransfer: { files: [new File(["a"], "a.pdf", { type: "application/pdf" }), new File(["b"], "b.pdf")] },
    });

    await waitFor(() => expect(uploaded).toBe(2));
  });

  it("shows why a file was rejected, and Submit names what is missing", async () => {
    state.documents = [
      document({
        status: "REJECTED",
        rejectionReason: "The file has more than 30 pages.",
        pageCount: null,
        pages: [],
      }),
    ];
    renderPage();

    const row = await screen.findByRole("listitem", { name: "hoa-don.pdf" });
    expect(within(row).getByText("Rejected")).toBeTruthy();
    expect(within(row).getByText("The file has more than 30 pages.")).toBeTruthy();
    expect(submitButton()).toHaveProperty("disabled", true);
    expect(screen.getByText("Add at least one document that converts.")).toBeTruthy();
  });

  it("offers Submit, disabled, when the API names only why it is not open yet", async () => {
    state.documents = [];
    state.claim = claimDetail({
      allowedActions: ["EDIT_CLAIM", "UPLOAD_DOCUMENT"],
      blockers: [{ code: "NO_READY_DOCUMENT", message: "Upload a document first." }],
    });
    renderPage();

    expect(await screen.findByText("No files yet. Add the claim's documents above.")).toBeTruthy();
    expect(submitButton()).toHaveProperty("disabled", true);
    expect(screen.getByText("Add at least one document that converts.")).toBeTruthy();
  });

  it("removes a file before submission", async () => {
    let removed = false;
    server.use(
      http.delete(`${CLAIM}/documents/${readyInvoice.id}`, () => {
        removed = true;
        state.documents = [];
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Remove hoa-don.pdf" }));

    await waitFor(() => expect(screen.queryByRole("listitem", { name: "hoa-don.pdf" })).toBeNull());
    expect(removed).toBe(true);
    expect(screen.getByText("No files yet. Add the claim's documents above.")).toBeTruthy();
  });

  it("shows the registration warnings without blocking anything", async () => {
    renderPage([{ code: "COVERAGE_OUTSIDE_PERIOD", message: "Outside." }, { code: "POLICY_NOT_ACTIVE", message: "Lapsed." }]);

    expect(await screen.findByText(/outside the policy's coverage period/)).toBeTruthy();
    expect(screen.getByText("The policy is not active. The claim is registered anyway.")).toBeTruthy();
    await screen.findByRole("listitem", { name: "hoa-don.pdf" });
    expect(submitButton()).toHaveProperty("disabled", false);

    await userEvent.setup().click(screen.getByRole("button", { name: "Dismiss" }));
    expect(screen.queryByText(/outside the policy's coverage period/)).toBeNull();
  });

  it("shows the fallback banner when the AI is unavailable, and submission stays open", async () => {
    state.snapshot = () => snapshotOf(state.documents, { fallbackActive: true });
    renderPage();

    expect(await screen.findByText(/AI assistance unavailable/)).toBeTruthy();
    expect(submitButton()).toHaveProperty("disabled", false);
  });

  it("submits, then shows the claim read only", async () => {
    server.use(
      http.post(`${CLAIM}/documents`, () => problem(415, "UNSUPPORTED_FILE_TYPE")),
      http.post(`${CLAIM}/submit`, () => {
        state.claim = claimDetail({ status: "PROCESSING", submittedAt: "2026-10-13T09:05:00Z", allowedActions: [] });
        return HttpResponse.json(snapshotOf(state.documents, { status: "PROCESSING" }), { status: 202 });
      }),
    );
    renderPage();
    const user = userEvent.setup({ applyAccept: false });
    await user.upload(await screen.findByLabelText("Add files"), new File(["x"], "ghi-chu.txt", { type: "text/plain" }));
    await screen.findByText("This file type is not accepted. Use JPG, PNG, PDF, DOCX or XLSX.");

    await user.click(screen.getByRole("button", { name: "Submit" }));

    expect(await screen.findByText("Claim submitted. It is now processing.")).toBeTruthy();
    expect(await screen.findByText("Processing")).toBeTruthy();
    expect(screen.queryByLabelText("Add files")).toBeNull();
    expect(screen.queryByRole("button", { name: /Remove/ })).toBeNull();
    expect(screen.queryByRole("button", { name: "Submit" })).toBeNull();
    expect(screen.getByRole("listitem", { name: "hoa-don.pdf" })).toBeTruthy();
    expect(screen.queryByRole("listitem", { name: "ghi-chu.txt" })).toBeNull();
  });

  it("explains a 409 from Submit", async () => {
    server.use(http.post(`${CLAIM}/submit`, () => problem(409, "UPLOAD_IN_PROGRESS")));
    renderPage();
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Submit" }));

    expect(await screen.findByText("A file is still uploading or converting. Wait for it to finish, then submit.")).toBeTruthy();
  });

  it("opens a submitted claim read only, with its header and documents", async () => {
    state.claim = claimDetail({ status: "NEEDS_VERIFICATION", submittedAt: "2026-10-13T09:05:00Z", allowedActions: [] });
    renderPage();

    expect(await screen.findByRole("heading", { name: "CLM-2026-000184" })).toBeTruthy();
    expect(screen.getByText("Needs verification")).toBeTruthy();
    expect(screen.getByText(/Submitted 13\/10\/2026 16:05/)).toBeTruthy();
    expect(await screen.findByRole("listitem", { name: "hoa-don.pdf" })).toBeTruthy();
    expect(screen.queryByLabelText("Add files")).toBeNull();
    expect(screen.queryByRole("button", { name: /Remove|Submit|Back/ })).toBeNull();
    expect(screen.queryByText("Remaining balance")).toBeNull();
  });

  it("says when the claim cannot be opened", async () => {
    server.use(http.get(CLAIM, () => problem(403, "CLAIM_OUT_OF_SCOPE")));
    renderPage();

    expect(await screen.findByText("You cannot open this")).toBeTruthy();
  });
});

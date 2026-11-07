import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import type { ClaimDetail, ClaimStatusSnapshot, Document, DocumentPage, PageAiSuggestion } from "@/lib/api/types";
import { aiRun, CLAIM_ID, claimDetail, document, OFFICER, page, policySummary, POLICY_ID, snapshotOf } from "@/test/intake";
import { API, authSession, problem, server } from "@/test/server";
import { intakeKeys } from "../queries";
import { ClaimDocumentsPage } from "./ClaimDocumentsPage";

// The real poll waits 3 seconds; these tests walk through several polls, so they wait 50 ms instead.
vi.mock(import("../processing"), async (importOriginal) => ({ ...(await importOriginal()), POLL_INTERVAL_MS: 50 }));
vi.mock("next/navigation", () => ({ useRouter: () => ({ push: vi.fn(), replace: vi.fn() }) }));

const CLAIM = `${API}/claims/${CLAIM_ID}`;
const RUN_ID = "50000000-0000-4000-8000-000000000001";

function suggestion(overrides: Partial<PageAiSuggestion>): PageAiSuggestion {
  return { pageType: "INVOICE", confidence: 0.97, level: "HIGH", qualityScore: 0.91, runId: RUN_ID, ...overrides };
}

/** A page the AI checked: the type and level are what the API decided, the web only shows them. */
function checked(overrides: Partial<DocumentPage> = {}): DocumentPage {
  return page({
    pageType: "INVOICE",
    pageTypeSource: "AI",
    quality: "READABLE",
    aiStatus: "DONE",
    ai: suggestion({}),
    ...overrides,
  });
}

/** Unclassified because the API found the type below its threshold; the web is not told the AI's guess as a type. */
const unsure = () =>
  checked({ pageType: "UNCLASSIFIED", pageTypeSource: null, ai: suggestion({ pageType: "RECEIPT", confidence: 0.62, level: "CHECK" }) });

const state: {
  claim: ClaimDetail;
  documents: Document[];
  snapshot: () => ClaimStatusSnapshot;
  statusCalls: number;
} = {
  claim: claimDetail(),
  documents: [],
  snapshot: () => snapshotOf(state.documents),
  statusCalls: 0,
};

beforeEach(() => {
  state.claim = claimDetail();
  state.documents = [document({ pages: [checked()] })];
  state.snapshot = () => snapshotOf(state.documents, { status: state.claim.status, aiRuns: [aiRun()] });
  state.statusCalls = 0;
  Object.assign(URL, { createObjectURL: () => "blob:page", revokeObjectURL: () => {} });
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
    http.get(`${API}/policies/${POLICY_ID}/summary`, () => HttpResponse.json(policySummary())),
  );
});

function renderPage() {
  session.start(authSession("INTAKE_OFFICER"));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <ClaimDocumentsPage claimId={CLAIM_ID} />
    </QueryClientProvider>,
  );
  return client;
}

const card = (n = 1) => screen.findByRole("listitem", { name: `Page ${n}` });
const typeControl = (n = 1) => screen.queryByRole("combobox", { name: `Choose type for page ${n}` });

describe("AI page check on SCR-13 (US04)", () => {
  it("shows a type the AI set with the AI marker and the High level, the score on hover", async () => {
    renderPage();

    const page1 = await card();
    expect(within(page1).getByText("Invoice")).toBeTruthy();
    expect(within(page1).getByText("AI")).toBeTruthy();
    const level = within(page1).getByText("High");
    expect(level.parentElement?.getAttribute("title")).toBe("Confidence 0.97");
    expect(within(page1).getByText("Readable")).toBeTruthy();
    expect(typeControl()).toBeNull();
    expect(screen.queryByText(/AI check unavailable/)).toBeNull();
  });

  it("shows the level the API sent, without comparing the score itself", async () => {
    // A score the web might call high, with the level the API decided: the card shows the API's word.
    state.documents = [document({ pages: [checked({ ai: suggestion({ confidence: 0.99, level: "CHECK" }) })] })];
    renderPage();

    const page1 = await card();
    expect(within(page1).getByText("Check")).toBeTruthy();
    expect(within(page1).queryByText("High")).toBeNull();
  });

  it("shows an Unclassified page with the Check level and offers a labelled Choose type", async () => {
    state.documents = [document({ pages: [unsure()] })];
    renderPage();

    const page1 = await card();
    expect(within(page1).getByText("Unclassified")).toBeTruthy();
    expect(within(page1).getByText("Check")).toBeTruthy();
    expect(within(page1).getByText("AI")).toBeTruthy();
    const control = await screen.findByRole("combobox", { name: "Choose type for page 1" });
    expect((control as HTMLSelectElement).value).toBe("");
    expect(within(control).getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Choose type",
      "Invoice",
      "Receipt",
      "Prescription",
      "Visit summary",
      "Lab result",
      "Other",
    ]);
  });

  it("sets the type the officer chooses, then shows it as theirs, without the AI marker", async () => {
    const sent: unknown[] = [];
    const before = unsure();
    state.documents = [document({ pages: [before] })];
    server.use(
      http.put(`${CLAIM}/pages/:pageId/type`, async ({ params, request }) => {
        expect(params.pageId).toBe(before.id);
        sent.push(await request.json());
        const after: DocumentPage = {
          ...before,
          pageType: "RECEIPT",
          pageTypeSource: "USER",
          pageTypeSetBy: OFFICER,
        };
        state.documents = [document({ pages: [after] })];
        return HttpResponse.json(after);
      }),
    );
    renderPage();
    const user = userEvent.setup();

    await user.selectOptions(await screen.findByRole("combobox", { name: "Choose type for page 1" }), "Receipt");

    await waitFor(() => expect(typeControl()).toBeNull());
    const page1 = await card();
    expect(within(page1).getByText("Receipt")).toBeTruthy();
    expect(sent).toEqual([{ pageType: "RECEIPT" }]);
    expect(within(page1).getByText("PB").getAttribute("title")).toBe("Set by Phạm Gia Bảo");
    expect(within(page1).queryByText("AI")).toBeNull();
    expect(within(page1).queryByText("Check")).toBeNull();
    expect(within(page1).queryByText("Unclassified")).toBeNull();
    expect(typeControl()).toBeNull();
    // The next poll agrees with what the officer set, so the card stays as it is.
    await waitFor(() => expect(state.statusCalls).toBeGreaterThanOrEqual(2));
    expect(within(await card()).getByText("Receipt")).toBeTruthy();
  });

  it("says why a type could not be set, and offers the choice again", async () => {
    state.documents = [document({ pages: [unsure()] })];
    server.use(http.put(`${CLAIM}/pages/:pageId/type`, () => problem(409, "CLAIM_STATE_CONFLICT")));
    renderPage();
    const user = userEvent.setup();

    await user.selectOptions(await screen.findByRole("combobox", { name: "Choose type for page 1" }), "Invoice");

    expect(await screen.findByRole("alert")).toHaveProperty(
      "textContent",
      "This claim has already been submitted, so it can no longer change.",
    );
    const control = screen.getByRole("combobox", { name: "Choose type for page 1" }) as HTMLSelectElement;
    expect(control.value).toBe("");
    expect(control.getAttribute("aria-describedby")).toBe(screen.getByRole("alert").id);
  });

  it("does not offer Choose type once the claim is submitted", async () => {
    state.claim = claimDetail({ status: "NEEDS_VERIFICATION", submittedAt: "2026-10-13T09:05:00Z", allowedActions: [] });
    state.documents = [document({ pages: [unsure()] })];
    renderPage();

    expect(within(await card()).getByText("Unclassified")).toBeTruthy();
    expect(typeControl()).toBeNull();
  });

  it("does not offer Choose type without UPLOAD_DOCUMENT", async () => {
    state.claim = claimDetail({ allowedActions: ["SUBMIT_CLAIM"] });
    state.documents = [document({ pages: [unsure()] })];
    renderPage();

    expect(within(await card()).getByText("Unclassified")).toBeTruthy();
    expect(typeControl()).toBeNull();
  });

  it("asks for a re-capture when the API marks a page low quality, and Submit stays open", async () => {
    state.documents = [
      document({ pages: [checked({ pageType: "RECEIPT", quality: "LOW_QUALITY", ai: suggestion({ pageType: "RECEIPT" }) })] }),
    ];
    renderPage();

    const page1 = await card();
    expect(within(page1).getByText("Receipt")).toBeTruthy();
    expect(within(page1).getByText("Low quality, re-capture")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Submit" })).toHaveProperty("disabled", false);
  });

  it("keeps a page waiting while its check is queued or reading, without Choose type", async () => {
    state.documents = [
      document({
        pageCount: 2,
        pages: [page({ aiStatus: "QUEUED" }), page({ id: "p2", pageNo: 2, claimPageNo: 2, aiStatus: "READING" })],
      }),
    ];
    // The check never ends in this test; the page stays as it is on every poll.
    renderPage();

    for (const n of [1, 2]) {
      const pageN = await card(n);
      expect(within(pageN).getByText("Waiting for AI check")).toBeTruthy();
      expect(within(pageN).getByText("Type not set")).toBeTruthy();
      expect(within(pageN).queryByText("AI")).toBeNull();
      expect(typeControl(n)).toBeNull();
    }
  });

  it("shows a failed page check, the fallback banner, and keeps Choose type and Submit open", async () => {
    state.documents = [document({ pages: [page({ aiStatus: "FAILED" })] })];
    state.snapshot = () =>
      snapshotOf(state.documents, { aiRuns: [aiRun({ status: "FAILED", errorCode: "MODEL_ERROR" })] });
    renderPage();

    const page1 = await card();
    expect(within(page1).getByText("AI check failed")).toBeTruthy();
    expect(within(page1).getByText("Unclassified")).toBeTruthy();
    expect(within(page1).queryByText("AI")).toBeNull();
    expect(await screen.findByText(
      "AI check unavailable. The work continues by hand, and the claim can still be submitted.",
    )).toBeTruthy();
    expect(typeControl()).toBeTruthy();
    expect(screen.getByRole("button", { name: "Submit" })).toHaveProperty("disabled", false);
  });

  it("shows the fallback banner when the claim says so, even if the poll does not", async () => {
    state.claim = claimDetail({ ai: { fallbackActive: true, clinicalCheck: "NOT_RUN" } });
    renderPage();

    await card();
    expect(screen.getByText(/AI check unavailable/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Submit" })).toHaveProperty("disabled", false);
  });

  it("tells the officer after submission that the fields will be entered by hand", async () => {
    state.claim = claimDetail({
      status: "NEEDS_VERIFICATION",
      submittedAt: "2026-10-13T09:05:00Z",
      allowedActions: [],
      ai: { fallbackActive: true, clinicalCheck: "NOT_RUN" },
    });
    renderPage();

    expect(await screen.findByText("AI assistance unavailable. The fields will be entered by hand.")).toBeTruthy();
  });

  it.each([
    ["NEEDS_VERIFICATION", "Needs verification"],
    ["READY_FOR_REVIEW", "Ready for review"],
  ] as const)(
    "polls through Processing after Submit until the claim is %s, reloads the claim, then stops",
    async (outcome, label) => {
      const extraction = (status: "QUEUED" | "RUNNING" | "SUCCEEDED") =>
        aiRun({ id: "r-extract", task: "FIELD_EXTRACTION", status, requestedAt: "2026-10-13T09:05:00Z" });
      // After Submit the extraction reads until the test lets it finish; then the claim has its new status.
      let finished = false;
      server.use(
        http.post(`${CLAIM}/submit`, () => {
          state.claim = claimDetail({ status: "PROCESSING", submittedAt: "2026-10-13T09:05:00Z", allowedActions: [] });
          state.snapshot = () => {
            if (!finished) {
              return snapshotOf(state.documents, { status: "PROCESSING", aiRuns: [aiRun(), extraction("RUNNING")] });
            }
            state.claim = claimDetail({ status: outcome, submittedAt: "2026-10-13T09:05:00Z", allowedActions: [] });
            return snapshotOf(state.documents, { status: outcome, aiRuns: [aiRun(), extraction("SUCCEEDED")] });
          };
          return HttpResponse.json(
            snapshotOf(state.documents, { status: "PROCESSING", aiRuns: [aiRun(), extraction("QUEUED")] }),
            { status: 202 },
          );
        }),
      );
      const client = renderPage();
      const user = userEvent.setup();
      await card();
      // Nothing runs on a checked draft, so it does not poll before Submit.
      const callsBeforeSubmit = state.statusCalls;
      await new Promise((resolve) => setTimeout(resolve, 200));
      expect(state.statusCalls).toBe(callsBeforeSubmit);

      await user.click(screen.getByRole("button", { name: "Submit" }));

      expect(await screen.findByText("Claim submitted. It is now processing.")).toBeTruthy();
      expect(screen.getByText("Processing")).toBeTruthy();
      // It keeps polling while the claim is processing.
      const processingFrom = state.statusCalls;
      await waitFor(() => expect(state.statusCalls).toBeGreaterThanOrEqual(processingFrom + 2));
      expect(screen.getByText("Processing")).toBeTruthy();

      finished = true;
      expect(await screen.findByText(label)).toBeTruthy();
      expect(screen.getByText("Claim submitted.")).toBeTruthy();
      await waitFor(() => expect(client.getQueryData<ClaimDetail>(intakeKeys.claim(CLAIM_ID))?.status).toBe(outcome));
      // The pages keep what the AI said about them in the read-only view.
      expect(within(await card()).getByText("High")).toBeTruthy();

      const settled = state.statusCalls;
      expect(settled).toBeGreaterThanOrEqual(callsBeforeSubmit + 2);
      await new Promise((resolve) => setTimeout(resolve, 300));
      expect(state.statusCalls).toBe(settled);
    },
  );
});

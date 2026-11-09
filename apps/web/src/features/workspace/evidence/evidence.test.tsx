import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import type { AiRunState, ClaimDetail, ClaimField, ClaimProgress, Document, ExpenseLine, StaffRole } from "@/lib/api/types";
import { aiRun, CLAIM_ID, document as doc, page, snapshotOf } from "@/test/intake";
import { API, authSession, problem, server } from "@/test/server";
import {
  autoAcceptedField,
  checkField,
  INVOICE_ID,
  missingField,
  PAGE_1,
  PAGE_2,
  progress,
  REGION,
  resolved,
  reviewerClaim,
  SUMMARY_ID,
} from "@/test/workspace";
import { ClaimWorkspace } from "../shell/ClaimWorkspace";
import { EvidencePage } from "./EvidencePage";

// The real poll waits 3 seconds; the re-run test walks through several polls.
vi.mock(import("@/features/intake/processing"), async (importOriginal) => ({ ...(await importOriginal()), POLL_INTERVAL_MS: 50 }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => `/claims/${CLAIM_ID}/evidence`,
}));

const CLAIM = `${API}/claims/${CLAIM_ID}`;
const RERUN_ID = "50000000-0000-4000-8000-000000000042";

const invoice = (): Document =>
  doc({
    id: INVOICE_ID,
    originalFilename: "hoa-don.pdf",
    pageCount: 2,
    pages: [
      page({ id: PAGE_1, documentId: INVOICE_ID, claimPageNo: 1, pageNo: 1, pageType: "INVOICE", pageTypeSource: "AI", quality: "READABLE", aiStatus: "DONE" }),
      page({ id: PAGE_2, documentId: INVOICE_ID, claimPageNo: 2, pageNo: 2, pageType: "INVOICE", pageTypeSource: "AI", quality: "READABLE", aiStatus: "DONE" }),
    ],
  });

const summary = (overrides: Partial<Document["pages"][number]> = {}): Document =>
  doc({
    id: SUMMARY_ID,
    originalFilename: "tom-tat.pdf",
    pages: [
      page({
        id: "41000000-0000-4000-8000-000000000003",
        documentId: SUMMARY_ID,
        claimPageNo: 3,
        pageType: "VISIT_SUMMARY",
        pageTypeSource: "AI",
        quality: "READABLE",
        aiStatus: "DONE",
        ...overrides,
      }),
    ],
  });

const state: {
  claim: ClaimDetail;
  documents: Document[];
  fields: ClaimField[];
  remaining: number;
  runs: () => AiRunState[];
  fallback: boolean;
} = { claim: reviewerClaim(), documents: [], fields: [], remaining: 3, runs: () => [], fallback: false };

beforeEach(() => {
  state.claim = reviewerClaim();
  state.documents = [invoice(), summary()];
  state.fields = [checkField(), missingField(), autoAcceptedField()];
  state.remaining = 3;
  state.runs = () => [];
  state.fallback = false;
  Object.assign(URL, { createObjectURL: () => "blob:page", revokeObjectURL: () => {} });
  server.use(
    http.get(CLAIM, () => HttpResponse.json(state.claim)),
    http.get(`${CLAIM}/documents`, () => HttpResponse.json({ items: state.documents })),
    http.get(`${CLAIM}/status`, () =>
      HttpResponse.json(
        snapshotOf(state.documents, { status: state.claim.status, fallbackActive: state.fallback, aiRuns: state.runs() }),
      ),
    ),
    http.get(`${CLAIM}/fields`, () => HttpResponse.json({ items: state.fields, remainingToVerify: state.remaining })),
    http.get(`${CLAIM}/pages/:pageId/image`, () =>
      HttpResponse.arrayBuffer(new Uint8Array([137, 80, 78, 71]).buffer, { headers: { "Content-Type": "image/png" } }),
    ),
  );
});

function renderPage(role: StaffRole = "CLAIMS_REVIEWER") {
  session.start(authSession(role));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <ClaimWorkspace claimId={CLAIM_ID}>
        <EvidencePage />
      </ClaimWorkspace>
    </QueryClientProvider>,
  );
  return client;
}

const row = (name: string) => screen.findByRole("listitem", { name });
const rowNow = (name: string) => screen.getByRole("listitem", { name });
const selectRow = async (name: string) => {
  const item = await row(name);
  await userEvent.click(within(item).getAllByRole("button")[0]!);
  return item;
};
const viewer = () => screen.getByRole("complementary", { name: "Page viewer" });
const actionBar = () => screen.getByRole("region", { name: "Progress and actions" });
const counter = () => screen.getByTestId("fields-counter");

/** Answers one change to the selected field, and returns what the request carried. */
function onAccept(answer: (fieldId: string, body: Record<string, unknown>) => Response) {
  const bodies: Record<string, unknown>[] = [];
  server.use(
    http.post(`${CLAIM}/fields/:fieldId/accept`, async ({ params, request }) => {
      const body = (await request.json()) as Record<string, unknown>;
      bodies.push(body);
      return answer(String(params.fieldId), body);
    }),
  );
  return bodies;
}

function onUpdate(answer: (fieldId: string, body: Record<string, unknown>) => Response) {
  const bodies: Record<string, unknown>[] = [];
  server.use(
    http.patch(`${CLAIM}/fields/:fieldId`, async ({ params, request }) => {
      const body = (await request.json()) as Record<string, unknown>;
      bodies.push(body);
      return answer(String(params.fieldId), body);
    }),
  );
  return bodies;
}

const changed = (field: ClaimField, moved: Partial<ClaimProgress> = {}) =>
  HttpResponse.json({ field, progress: progress(moved) });

describe("the workspace shell", () => {
  it("shows the claim in the header and only the tabs that exist", async () => {
    renderPage();
    const header = await screen.findByRole("banner", { name: "Claim" });

    expect(within(header).getByText("CLM-2026-000184")).toBeTruthy();
    expect(within(header).getByRole("heading", { name: "Nguyễn Văn An" })).toBeTruthy();
    expect(within(header).getByText("PL-2026-00731")).toBeTruthy();
    expect(within(header).getByText("12/10/2026")).toBeTruthy();
    expect(within(header).getByText("Needs verification")).toBeTruthy();
    expect(within(header).getByText("Assigned to Trần Thu Hà")).toBeTruthy();

    const tabs = within(screen.getByRole("navigation", { name: "Claim sections" })).getAllByRole("link");
    expect(tabs.map((tab) => tab.textContent)).toEqual(["Evidence"]);
    expect(tabs[0]?.getAttribute("aria-current")).toBe("page");
  });

  it("lists pages by document with type, quality and AI status", async () => {
    renderPage();
    const rail = await screen.findByRole("complementary", { name: "Documents" });

    expect(await within(rail).findByText("Pages (3)")).toBeTruthy();
    expect(within(rail).getByText("hoa-don.pdf")).toBeTruthy();
    const firstPage = within(rail).getByRole("listitem", { name: "Page 1" });
    expect(firstPage.textContent).toContain("Invoice");
    expect(firstPage.textContent).toContain("Readable");
    expect(firstPage.textContent).toContain("AI done");
  });

  it("shows the forbidden state for a claim out of scope", async () => {
    server.use(http.get(CLAIM, () => problem(403, "CLAIM_OUT_OF_SCOPE")));
    renderPage();

    expect(await screen.findByRole("heading", { name: "You do not have access to this page" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Back to your home page" }).getAttribute("href")).toBe("/queue");
  });

  it("offers Start review on an unassigned claim, and opens the fields once taken", async () => {
    state.claim = reviewerClaim({ assignee: null, counts: null, allowedActions: ["START_REVIEW"] });
    let fieldsRead = false;
    server.use(
      http.get(`${CLAIM}/fields`, () => {
        fieldsRead = true;
        return HttpResponse.json({ items: state.fields, remainingToVerify: 3 });
      }),
      http.post(`${CLAIM}/start-review`, () => {
        state.claim = reviewerClaim();
        return HttpResponse.json(state.claim);
      }),
    );
    renderPage();

    expect(await screen.findByText("Not assigned")).toBeTruthy();
    expect(screen.getByText("This claim is not assigned yet")).toBeTruthy();
    expect(fieldsRead).toBe(false);

    await userEvent.click(within(actionBar()).getByRole("button", { name: "Start review" }));

    expect(await row("Invoice total")).toBeTruthy();
    expect(screen.getByText("Assigned to Trần Thu Hà")).toBeTruthy();
  });

  it("says another reviewer took the claim when Start review gets a 409", async () => {
    state.claim = reviewerClaim({ assignee: null, counts: null, allowedActions: ["START_REVIEW"] });
    server.use(http.post(`${CLAIM}/start-review`, () => problem(409, "CLAIM_ALREADY_ASSIGNED")));
    renderPage();

    await userEvent.click(await within(await screen.findByRole("region", { name: "Progress and actions" })).findByRole("button", { name: "Start review" }));

    expect((await screen.findByRole("alert")).textContent).toContain("Another reviewer took this claim first");
  });

  it("shows the blockers the API names", async () => {
    state.claim = reviewerClaim({ blockers: [{ code: "SOURCE_REQUIRED", message: "2 required fields are not resolved." }] });
    renderPage();

    expect(await screen.findByText("2 required fields are not resolved.")).toBeTruthy();
  });

  it("shows the fallback banner when the claim says the AI failed", async () => {
    state.claim = reviewerClaim({ ai: { fallbackActive: true, clinicalCheck: "NOT_RUN" } });
    renderPage();

    expect(await screen.findByText("AI assistance unavailable. Fields require manual entry.")).toBeTruthy();
  });

  it("has no banner while the AI works", async () => {
    renderPage();

    await row("Invoice total");
    expect(screen.queryByText("AI assistance unavailable. Fields require manual entry.")).toBeNull();
  });
});

describe("the field list (US06)", () => {
  it("groups by document, keeps the API's order and shows the counter", async () => {
    state.fields = [
      checkField({ id: "low", fieldKey: "invoice_total" }),
      missingField({ id: "lower" }),
      autoAcceptedField({ id: "high" }),
    ];
    renderPage();

    const section = await screen.findByRole("region", { name: "Invoice" });
    const names = within(section)
      .getAllByRole("listitem")
      .map((item) => item.getAttribute("aria-label"));
    expect(names).toEqual(["Invoice total", "Service date"]);
    expect(within(screen.getByRole("region", { name: "Visit summary" })).getByRole("listitem", { name: "Diagnosis" })).toBeTruthy();
    expect(counter().textContent).toBe("3 fields to verify");
    expect(within(actionBar()).getByText("3 fields to verify")).toBeTruthy();
  });

  it("marks an AI value with the marker and its level, the score on hover", async () => {
    renderPage();
    const item = await row("Invoice total");

    expect(within(item).getByText("AI")).toBeTruthy();
    const level = within(item).getByText("Check").parentElement!;
    expect(level.getAttribute("title")).toBe("Confidence 0.62");
    expect(within(item).getByText("1,400,000")).toBeTruthy();
  });

  it("keeps the marker on an auto-accepted suggestion", async () => {
    renderPage();
    const item = await row("Service date");

    expect(within(item).getByText("AI")).toBeTruthy();
    expect(within(item).getByText("High")).toBeTruthy();
  });

  it("shows a Missing field with no AI value and asks for entry", async () => {
    renderPage();
    const item = await row("Diagnosis");

    expect(within(item).getByText("Needs entry")).toBeTruthy();
    expect(within(item).getByText("Missing")).toBeTruthy();
    expect(within(item).getByText("No source")).toBeTruthy();
  });

  it("shows the person's initials, not the AI marker, once resolved", async () => {
    state.fields = [resolved(checkField(), { valueAmount: 1_400_000 })];
    renderPage();
    const item = await row("Invoice total");

    expect(within(item).getByText("TH").getAttribute("title")).toBe("Set by Trần Thu Hà");
    expect(within(item).queryByText("AI")).toBeNull();
    expect(within(item).queryByText("Check")).toBeNull();
    expect(within(item).getByText("1,400,000 ₫")).toBeTruthy();
  });

  it("names expense line fields by their line", async () => {
    state.fields = [checkField({ fieldKey: "line_description", itemNo: 4, valueKind: "TEXT", ai: { ...checkField().ai!, value: "Kem Obagi" } })];
    renderPage();

    expect(await row("Line 4 description")).toBeTruthy();
  });

  it("offers documents with no fields yet", async () => {
    state.documents = [invoice(), summary({ pageType: "UNCLASSIFIED", pageTypeSource: null })];
    state.fields = [checkField()];
    renderPage();

    const section = await screen.findByRole("region", { name: "Unclassified" });
    expect(section.textContent).toContain("Choose a type for its pages");
  });
});

describe("the linking rule", () => {
  it("opens on the first field with its page and region in zone C", async () => {
    renderPage();
    await row("Invoice total");

    const region = await within(viewer()).findByRole("img", { name: "Source region: invoice_total · 0.62" });
    const rect = region.querySelector("rect")!;
    expect(rect.getAttribute("x")).toBe(String(REGION.x));
    expect(rect.getAttribute("y")).toBe(String(REGION.y));
    expect(within(viewer()).getByText("p1 / 3")).toBeTruthy();
  });

  it("moves zone C to the selected field's page and region", async () => {
    renderPage();
    await selectRow("Service date");

    expect(await within(viewer()).findByRole("img", { name: "Source region: service_date · 0.97" })).toBeTruthy();
    expect(within(viewer()).getByText("p2 / 3")).toBeTruthy();
  });

  it("goes back to the source from the pin after paging away", async () => {
    renderPage();
    const item = await row("Invoice total");
    await within(viewer()).findByRole("img", { name: /Source region/ });

    await userEvent.click(within(viewer()).getByRole("button", { name: "Next page" }));
    expect(within(viewer()).getByText("p2 / 3")).toBeTruthy();
    expect(within(viewer()).queryByRole("img", { name: /Source region/ })).toBeNull();

    await userEvent.click(within(item).getByRole("button", { name: "Show source of Invoice total on page 1" }));

    expect(await within(viewer()).findByRole("img", { name: /Source region/ })).toBeTruthy();
    expect(within(viewer()).getByText("p1 / 3")).toBeTruthy();
  });

  it("shows a field with no source on the first page of its document", async () => {
    renderPage();
    await selectRow("Diagnosis");

    expect(within(viewer()).getByText("p3 / 3")).toBeTruthy();
    expect(within(viewer()).queryByRole("img", { name: /Source region/ })).toBeNull();
  });
});

describe("Accept", () => {
  it("sends the version it read, swaps the marker for initials and updates the progress", async () => {
    const bodies = onAccept(() => changed(resolved(checkField(), { valueAmount: 1_400_000 })));
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Accept" }));

    expect(await within(item).findByText("TH")).toBeTruthy();
    expect(bodies).toEqual([{ rowVersion: 1 }]);
    expect(within(item).queryByText("AI")).toBeNull();
    expect(within(item).getByText("1,400,000 ₫")).toBeTruthy();
    expect(counter().textContent).toBe("2 fields to verify");
    expect(within(actionBar()).getByText("2 fields to verify")).toBeTruthy();
  });

  it("is not offered on a Missing field, nor on one a person resolved", async () => {
    state.fields = [missingField(), resolved(checkField())];
    renderPage();
    const missing = await row("Diagnosis");

    expect(within(missing).queryByRole("button", { name: "Accept" })).toBeNull();
    expect(within(missing).getByRole("button", { name: "Enter value" })).toBeTruthy();

    const done = await selectRow("Invoice total");
    expect(within(done).queryByRole("button", { name: "Accept" })).toBeNull();
    expect(within(done).getByRole("button", { name: "Edit" })).toBeTruthy();
  });

  it("shows FIELD_HAS_NO_AI_VALUE next to the field", async () => {
    onAccept(() => problem(422, "FIELD_HAS_NO_AI_VALUE"));
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Accept" }));

    expect((await within(item).findByRole("alert")).textContent).toContain("AI has no value to accept");
  });

  it("reloads the claim when it moves to In review", async () => {
    onAccept(() => {
      state.claim = reviewerClaim({
        status: "IN_REVIEW",
        counts: { fieldsToVerify: 0, blockingAlerts: 0, warningAlerts: 0, mappingsToConfirm: 0, openFlags: 0, keptFlags: 0 },
      });
      state.remaining = 0;
      return changed(resolved(checkField()), { status: "IN_REVIEW", fieldsToVerify: 0 });
    });
    state.fields = [checkField()];
    renderPage();
    const header = await screen.findByRole("banner", { name: "Claim" });
    expect(within(header).getByText("Needs verification")).toBeTruthy();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Accept" }));

    expect(await within(header).findByText("In review")).toBeTruthy();
    expect(counter().textContent).toBe("No fields to verify");
    expect(within(actionBar()).queryByRole("button", { name: /Verify next field/ })).toBeNull();
  });
});

describe("Edit", () => {
  it("edits money as whole VND", async () => {
    const bodies = onUpdate(() => changed(resolved(checkField(), { status: "CORRECTED", valueAmount: 1_500_000 })));
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    const input = within(item).getByRole("textbox", { name: "Invoice total (₫)" }) as HTMLInputElement;
    expect(input.value).toBe("1,400,000");
    expect(document.activeElement).toBe(input);

    await userEvent.clear(input);
    await userEvent.type(input, "1.500.000{Enter}");

    expect(await within(item).findByText("1,500,000 ₫")).toBeTruthy();
    expect(bodies).toEqual([{ rowVersion: 1, action: "SET_VALUE", valueAmount: 1_500_000 }]);
    expect(within(item).queryByRole("textbox")).toBeNull();
  });

  it("edits a date typed dd/MM/yyyy", async () => {
    const field = autoAcceptedField();
    const bodies = onUpdate(() => changed(resolved(field, { status: "CORRECTED", valueDate: "2026-10-13" })));
    renderPage();
    const item = await selectRow("Service date");

    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    const input = within(item).getByRole("textbox", { name: "Service date" });
    await userEvent.clear(input);
    await userEvent.type(input, "13/10/2026{Enter}");

    expect(await within(item).findByText("13/10/2026")).toBeTruthy();
    expect(bodies[0]).toEqual({ rowVersion: 1, action: "SET_VALUE", valueDate: "2026-10-13" });
  });

  it("edits text and a number as text", async () => {
    const quantity = checkField({ id: "qty", fieldKey: "line_quantity", itemNo: 1, valueKind: "NUMBER", ai: { ...checkField().ai!, value: "2" } });
    state.fields = [quantity];
    const bodies = onUpdate(() => changed(resolved(quantity, { status: "CORRECTED", valueText: "3" })));
    renderPage();
    const item = await row("Line 1 quantity");

    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    const input = within(item).getByRole("textbox", { name: "Line 1 quantity" });
    await userEvent.clear(input);
    await userEvent.type(input, "3{Enter}");

    await within(item).findByText("3");
    expect(bodies[0]).toEqual({ rowVersion: 1, action: "SET_VALUE", valueText: "3" });
  });

  it("keeps a wrong format in the input, says why and sends nothing", async () => {
    const bodies = onUpdate(() => changed(resolved(checkField())));
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    const input = within(item).getByRole("textbox", { name: "Invoice total (₫)" }) as HTMLInputElement;
    await userEvent.clear(input);
    await userEvent.type(input, "abc{Enter}");

    expect(within(item).getByText("Enter a whole amount in VND, such as 1,400,000.")).toBeTruthy();
    expect(input.value).toBe("abc");
    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(bodies).toEqual([]);
  });

  it("shows the API's field problem next to the field", async () => {
    server.use(
      http.patch(`${CLAIM}/fields/:fieldId`, () =>
        HttpResponse.json(
          {
            title: "Invalid",
            status: 422,
            code: "VALIDATION_FAILED",
            requestId: "t",
            errors: [{ field: "valueAmount", code: "OUT_OF_RANGE", message: "The amount is too large." }],
          },
          { status: 422 },
        ),
      ),
    );
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    await userEvent.click(within(item).getByRole("button", { name: "Save" }));

    expect((await within(item).findByRole("alert")).textContent).toBe("The amount is too large.");
  });

  it("starts an ID number empty, so the mask is never saved", async () => {
    state.fields = [checkField({ fieldKey: "national_id", valueKind: "TEXT", status: "AUTO_ACCEPTED", valueText: "********4821", sourcePageId: PAGE_1, sourceBbox: REGION })];
    renderPage();
    const item = await row("ID number");
    expect(within(item).getByText("********4821")).toBeTruthy();

    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));

    expect((within(item).getByRole("textbox", { name: "ID number" }) as HTMLInputElement).value).toBe("");
  });

  it("keeps what was typed when moving to another row and back", async () => {
    renderPage();
    const item = await row("Invoice total");
    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    const input = within(item).getByRole("textbox", { name: "Invoice total (₫)" });
    await userEvent.clear(input);
    await userEvent.type(input, "999");

    await selectRow("Service date");
    expect(within(rowNow("Invoice total")).getByText("Unsaved")).toBeTruthy();
    await selectRow("Invoice total");

    expect((within(rowNow("Invoice total")).getByRole("textbox") as HTMLInputElement).value).toBe("999");
  });

  it("Cancel closes the editor without saving", async () => {
    const bodies = onUpdate(() => changed(resolved(checkField())));
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    await userEvent.click(within(item).getByRole("button", { name: "Cancel" }));

    expect(within(item).queryByRole("textbox")).toBeNull();
    expect(bodies).toEqual([]);
  });
});

describe("Not present", () => {
  it("sends NOT_PRESENT and shows it with the person's initials", async () => {
    const bodies = onUpdate(() => changed(resolved(missingField(), { status: "NOT_PRESENT" })));
    state.fields = [missingField()];
    renderPage();
    const item = await row("Diagnosis");

    await userEvent.click(within(item).getByRole("button", { name: "Not present" }));

    expect(await within(item).findByText("TH")).toBeTruthy();
    expect(within(item).getAllByText("Not present").length).toBeGreaterThan(0);
    expect(bodies).toEqual([{ rowVersion: 1, action: "NOT_PRESENT" }]);
  });
});

describe("a field with no source (BR-02)", () => {
  const drawSurface = () => within(viewer()).findByRole("application", { name: /Draw the source region of/ });

  async function drag() {
    const surface = await drawSurface();
    vi.spyOn(surface, "getBoundingClientRect").mockReturnValue({ left: 0, top: 0, width: 100, height: 200 } as DOMRect);
    fireEvent.pointerDown(surface, { clientX: 10, clientY: 20, pointerId: 1 });
    fireEvent.pointerUp(surface, { clientX: 60, clientY: 40, pointerId: 1 });
  }

  it("asks for a region before saving, then sends it with the value", async () => {
    state.fields = [missingField()];
    const bodies = onUpdate(() =>
      changed(resolved(missingField(), { status: "ENTERED", valueText: "Viêm họng", sourcePageId: "p", sourceBbox: REGION })),
    );
    renderPage();
    const item = await row("Diagnosis");

    await userEvent.click(within(item).getByRole("button", { name: "Enter value" }));
    const input = within(item).getByRole("textbox", { name: "Diagnosis" });
    await userEvent.type(input, "Viêm họng{Enter}");

    expect((await within(item).findByRole("alert")).textContent).toContain("Draw the region");
    expect(bodies).toEqual([]);

    await drag();

    expect(await within(viewer()).findByRole("img", { name: "Source region: Drawn region" })).toBeTruthy();
    expect(within(item).getByText("Source: region drawn on p3")).toBeTruthy();
    expect(within(item).queryByRole("alert")).toBeNull();
    expect((within(item).getByRole("textbox") as HTMLInputElement).value).toBe("Viêm họng");

    await userEvent.click(within(item).getByRole("button", { name: "Save" }));

    await within(item).findByText("Viêm họng");
    expect(bodies[0]).toEqual({
      rowVersion: 1,
      action: "SET_VALUE",
      valueText: "Viêm họng",
      sourcePageId: "41000000-0000-4000-8000-000000000003",
      sourceBbox: { x: 0.1, y: 0.1, w: 0.5, h: 0.1 },
    });
  });

  it("opens the drawing tool when the API answers SOURCE_REQUIRED, and keeps the input", async () => {
    onUpdate(() => problem(422, "SOURCE_REQUIRED"));
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    await userEvent.click(within(item).getByRole("button", { name: "Save" }));

    expect((await within(item).findByRole("alert")).textContent).toContain("no source on the page");
    expect(await drawSurface()).toBeTruthy();
    expect((within(item).getByRole("textbox") as HTMLInputElement).value).toBe("1,400,000");
  });

  it("asks for a region before accepting an AI value with no source", async () => {
    const noSource = checkField({ ai: { ...checkField().ai!, pageId: null, bbox: null } });
    state.fields = [noSource];
    const bodies = onAccept(() => changed(resolved(noSource)));
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Accept" }));
    expect(bodies).toEqual([]);

    await drag();
    await userEvent.click(within(item).getByRole("button", { name: "Accept" }));

    await within(item).findByText("TH");
    expect(bodies[0]).toMatchObject({ rowVersion: 1, sourceBbox: { x: 0.1, y: 0.1, w: 0.5, h: 0.1 } });
  });

  it("can be cancelled from the viewer", async () => {
    state.fields = [missingField()];
    // The drawing surface sits beside the page image; a shared React key between them would log an error here.
    const logged = vi.spyOn(console, "error");
    renderPage();
    const item = await row("Diagnosis");

    await userEvent.click(within(item).getByRole("button", { name: "Draw region" }));
    expect(await drawSurface()).toBeTruthy();
    expect(logged).not.toHaveBeenCalled();

    await userEvent.click(within(viewer()).getByRole("button", { name: "Cancel" }));

    expect(within(viewer()).queryByRole("application")).toBeNull();
  });
});

describe("a version conflict", () => {
  const conflict = () =>
    HttpResponse.json(
      {
        title: "Conflict",
        status: 409,
        code: "VERSION_CONFLICT",
        requestId: "t",
        details: { currentRowVersion: 7, current: { valueAmount: 900_000 } },
      },
      { status: 409 },
    );

  it("keeps what the person typed, shows the current value and reloads on request", async () => {
    const bodies = onUpdate(() => conflict());
    renderPage();
    const item = await row("Invoice total");
    await userEvent.click(within(item).getByRole("button", { name: "Edit" }));
    const input = within(item).getByRole("textbox", { name: "Invoice total (₫)" }) as HTMLInputElement;
    await userEvent.clear(input);
    await userEvent.type(input, "1,500,000{Enter}");

    const notice = await within(item).findByRole("alert");
    expect(notice.textContent).toContain("900,000 ₫");
    expect(input.value).toBe("1,500,000");

    state.fields = [checkField({ rowVersion: 7, valueAmount: 900_000 }), missingField(), autoAcceptedField()];
    await userEvent.click(within(notice).getByRole("button", { name: "Reload" }));

    expect(await within(item).findByText("900,000 ₫")).toBeTruthy();
    expect(within(item).queryByRole("alert")).toBeNull();
    expect((within(item).getByRole("textbox") as HTMLInputElement).value).toBe("1,500,000");

    await userEvent.click(within(item).getByRole("button", { name: "Save" }));
    await waitFor(() => expect(bodies.length).toBe(2));
    expect(bodies[1]).toMatchObject({ rowVersion: 7, valueAmount: 1_500_000 });
  });

  it("shows the same notice on Accept", async () => {
    onAccept(() => conflict());
    renderPage();
    const item = await row("Invoice total");

    await userEvent.click(within(item).getByRole("button", { name: "Accept" }));

    expect((await within(item).findByRole("alert")).textContent).toContain("Someone else changed this first");
  });
});

describe("page type", () => {
  it("offers Choose type on an Unclassified page and reloads the fields it creates", async () => {
    const chosen = summary({ pageType: "UNCLASSIFIED", pageTypeSource: null });
    state.documents = [invoice(), chosen];
    state.fields = [checkField()];
    let body: unknown;
    server.use(
      http.put(`${CLAIM}/pages/:pageId/type`, async ({ request }) => {
        body = await request.json();
        const updated = {
          ...chosen.pages[0]!,
          pageType: "VISIT_SUMMARY" as const,
          pageTypeSource: "USER" as const,
          pageTypeSetBy: { id: "x", fullName: "Trần Thu Hà", initials: "TH" },
        };
        state.documents = [invoice(), { ...chosen, pages: [updated] }];
        state.fields = [checkField(), missingField()];
        return HttpResponse.json(updated);
      }),
    );
    renderPage();
    const select = await screen.findByRole("combobox", { name: "Choose type for page 3" });

    await userEvent.selectOptions(select, "VISIT_SUMMARY");

    expect(await row("Diagnosis")).toBeTruthy();
    expect(body).toEqual({ pageType: "VISIT_SUMMARY" });
    await waitFor(() => expect(screen.queryByRole("combobox", { name: "Choose type for page 3" })).toBeNull());
    const rail = screen.getByRole("complementary", { name: "Documents" });
    expect(within(within(rail).getByRole("listitem", { name: "Page 3" })).getByText("TH")).toBeTruthy();
  });

  it("does not offer it while the AI is still reading the page", async () => {
    state.documents = [invoice(), summary({ pageType: "UNCLASSIFIED", pageTypeSource: null, aiStatus: "READING" })];
    state.fields = [checkField()];
    renderPage();

    await row("Invoice total");
    expect(screen.queryByRole("combobox", { name: "Choose type for page 3" })).toBeNull();
  });

  it("lets the reviewer change a misread page in the viewer", async () => {
    let asked: string | undefined;
    server.use(
      http.put(`${CLAIM}/pages/:pageId/type`, ({ params }) => {
        asked = String(params.pageId);
        return HttpResponse.json({ ...invoice().pages[0]!, pageType: "RECEIPT", pageTypeSource: "USER" });
      }),
    );
    renderPage();
    await row("Invoice total");

    await userEvent.selectOptions(await screen.findByRole("combobox", { name: "Change type of page 1" }), "RECEIPT");

    await waitFor(() => expect(asked).toBe(PAGE_1));
  });

  it("names a refusal next to the control", async () => {
    state.documents = [invoice(), summary({ pageType: "UNCLASSIFIED", pageTypeSource: null })];
    state.fields = [checkField()];
    server.use(http.put(`${CLAIM}/pages/:pageId/type`, () => problem(409, "CLAIM_STATE_CONFLICT")));
    renderPage();

    await userEvent.selectOptions(await screen.findByRole("combobox", { name: "Choose type for page 3" }), "OTHER");

    expect((await screen.findByRole("alert")).textContent).toContain("moved on");
  });
});

describe("Re-run AI on this page", () => {
  const run = (status: AiRunState["status"]) => aiRun({ id: RERUN_ID, task: "PAGE_REREAD", status });

  it("keeps watching a run the poll does not list yet, and reloads when it ends", async () => {
    // The poll in hand when the request comes back can be older than the run.
    let polls = 0;
    server.use(
      http.post(`${CLAIM}/pages/:pageId/rerun-extraction`, () => {
        state.runs = () => {
          polls += 1;
          if (polls === 1) {
            return [];
          }
          state.fields = [checkField({ ai: { ...checkField().ai!, value: "1,450,000" } })];
          return [run("SUCCEEDED")];
        };
        return HttpResponse.json(
          { id: RERUN_ID, task: "PAGE_REREAD", status: "QUEUED", requestedAt: "2026-10-14T04:00:00Z" },
          { status: 202 },
        );
      }),
    );
    const client = renderPage();
    const item = await row("Invoice total");

    await userEvent.click(screen.getByRole("button", { name: "Re-run AI on this page" }));
    await waitFor(() => expect(polls).toBe(1));
    expect(within(item).getByText("1,400,000")).toBeTruthy();

    await client.invalidateQueries({ queryKey: ["claims", "status", CLAIM_ID] });

    expect(await within(item).findByText("1,450,000")).toBeTruthy();
  });

  it("polls until the run ends, then reloads the fields", async () => {
    let polls = 0;
    let posted: string | undefined;
    server.use(
      http.post(`${CLAIM}/pages/:pageId/rerun-extraction`, ({ params }) => {
        posted = String(params.pageId);
        state.runs = () => {
          polls += 1;
          if (polls < 3) {
            return [run(polls === 1 ? "QUEUED" : "RUNNING")];
          }
          state.fields = [checkField({ ai: { ...checkField().ai!, value: "1,450,000" } }), missingField(), autoAcceptedField()];
          return [run("SUCCEEDED")];
        };
        return HttpResponse.json(
          { id: RERUN_ID, task: "PAGE_REREAD", status: "QUEUED", requestedAt: "2026-10-14T04:00:00Z" },
          { status: 202 },
        );
      }),
    );
    renderPage();
    const item = await row("Invoice total");
    expect(within(item).getByText("1,400,000")).toBeTruthy();

    await userEvent.click(screen.getByRole("button", { name: "Re-run AI on this page" }));

    expect(await screen.findByText(/AI is reading the page again/)).toBeTruthy();
    expect((screen.getByRole("button", { name: "Re-run AI on this page" }) as HTMLButtonElement).disabled).toBe(true);
    expect(posted).toBe(PAGE_1);

    expect(await within(item).findByText("1,450,000", {}, { timeout: 3_000 })).toBeTruthy();
    expect(screen.getByText("AI read p1 again. Fields a person resolved kept their values.")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Re-run AI on this page" }) as HTMLButtonElement).disabled).toBe(false);

    const settled = polls;
    await new Promise((resolve) => setTimeout(resolve, 200));
    expect(polls).toBeLessThanOrEqual(settled + 1);
  });

  it("says to enter by hand when the run fails, and shows the banner", async () => {
    server.use(
      http.post(`${CLAIM}/pages/:pageId/rerun-extraction`, () => {
        let first = true;
        state.runs = () => {
          if (first) {
            first = false;
            return [run("RUNNING")];
          }
          // The failed run sets `fallbackActive` on the claim, which reloads when the run ends.
          state.claim = reviewerClaim({ ai: { fallbackActive: true, clinicalCheck: "NOT_RUN" } });
          return [run("FAILED")];
        };
        return HttpResponse.json(
          { id: RERUN_ID, task: "PAGE_REREAD", status: "QUEUED", requestedAt: "2026-10-14T04:00:00Z" },
          { status: 202 },
        );
      }),
    );
    renderPage();
    await row("Invoice total");

    await userEvent.click(screen.getByRole("button", { name: "Re-run AI on this page" }));

    expect(await screen.findByText("AI could not read p1. Enter its fields by hand.", {}, { timeout: 3_000 })).toBeTruthy();
    expect(await screen.findByText("AI assistance unavailable. Fields require manual entry.")).toBeTruthy();
  });

  it("names a refusal", async () => {
    server.use(http.post(`${CLAIM}/pages/:pageId/rerun-extraction`, () => problem(409, "CLAIM_STATE_CONFLICT")));
    renderPage();
    await row("Invoice total");

    await userEvent.click(screen.getByRole("button", { name: "Re-run AI on this page" }));

    expect((await screen.findByRole("alert")).textContent).toContain("moved on");
  });
});

describe("expense lines", () => {
  const LINE_ID = "70000000-0000-4000-8000-000000000005";
  const line = (): ExpenseLine => ({
    id: LINE_ID,
    documentId: INVOICE_ID,
    lineNo: 5,
    description: null,
    quantity: null,
    unitPrice: null,
    amount: null,
    origin: "MANUAL",
    mapping: { status: "PENDING", categoryCode: null, ai: null, resolvedBy: null, resolvedAt: null },
    override: null,
  });
  const lineField = () =>
    missingField({
      id: "line-5-description",
      documentId: INVOICE_ID,
      fieldKey: "line_description",
      itemNo: 5,
      expenseLineId: LINE_ID,
      ai: null,
      aiMarker: false,
    });

  it("adds a line on an invoice and opens its first empty field", async () => {
    state.fields = [checkField()];
    let body: unknown;
    server.use(
      http.post(`${CLAIM}/expense-lines`, async ({ request }) => {
        body = await request.json();
        state.fields = [checkField(), lineField()];
        return HttpResponse.json({ line: line(), progress: progress({ fieldsToVerify: 3 }) }, { status: 201 });
      }),
    );
    renderPage();
    const section = await screen.findByRole("region", { name: "Invoice" });

    await userEvent.click(within(section).getByRole("button", { name: "Add expense line" }));

    const added = await row("Line 5 description");
    expect(body).toEqual({ documentId: INVOICE_ID });
    await waitFor(() => expect(within(added).getByRole("button", { name: "Enter value" })).toBeTruthy());
    expect(within(added).getByText("Needs entry")).toBeTruthy();
    expect(within(added).queryByText("AI")).toBeNull();
  });

  it("is offered only on an invoice", async () => {
    renderPage();

    await screen.findByRole("region", { name: "Invoice" });
    expect(within(screen.getByRole("region", { name: "Visit summary" })).queryByRole("button", { name: "Add expense line" })).toBeNull();
  });

  it("removes a line only after confirmation, using the field's line id", async () => {
    state.fields = [lineField(), checkField()];
    let removed: string | undefined;
    server.use(
      http.delete(`${CLAIM}/expense-lines/:lineId`, ({ params }) => {
        removed = String(params.lineId);
        state.fields = [checkField()];
        state.remaining = 1;
        return HttpResponse.json(progress({ fieldsToVerify: 1 }));
      }),
    );
    renderPage();
    const item = await row("Line 5 description");

    await userEvent.click(within(item).getByRole("button", { name: "Remove line" }));

    const dialog = await screen.findByRole("dialog", { name: "Remove line 5?" });
    expect(removed).toBeUndefined();

    await userEvent.click(within(dialog).getByRole("button", { name: "Remove line" }));

    await waitFor(() => expect(screen.queryByRole("listitem", { name: "Line 5 description" })).toBeNull());
    expect(removed).toBe(LINE_ID);
    expect(counter().textContent).toBe("1 field to verify");
  });

  it("keeps the line when the reviewer backs out", async () => {
    state.fields = [lineField()];
    renderPage();
    const item = await row("Line 5 description");

    await userEvent.click(within(item).getByRole("button", { name: "Remove line" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Keep line" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
    expect(rowNow("Line 5 description")).toBeTruthy();
  });

  it("keeps the dialog open and says why when removal is refused", async () => {
    state.fields = [lineField()];
    server.use(http.delete(`${CLAIM}/expense-lines/:lineId`, () => problem(409, "CLAIM_STATE_CONFLICT")));
    renderPage();
    const item = await row("Line 5 description");

    await userEvent.click(within(item).getByRole("button", { name: "Remove line" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.click(within(dialog).getByRole("button", { name: "Remove line" }));

    expect((await within(dialog).findByRole("alert")).textContent).toContain("moved on");
  });
});

describe("the keyboard path (A, E, N, arrows)", () => {
  it("N goes to the next field to verify, skipping what is resolved, and moves zone C", async () => {
    state.fields = [checkField(), autoAcceptedField(), missingField()];
    renderPage();
    const first = await row("Invoice total");
    expect(within(first).getAllByRole("button")[0]?.getAttribute("aria-current")).toBe("true");

    await userEvent.keyboard("n");

    const next = rowNow("Diagnosis");
    expect(within(next).getAllByRole("button")[0]?.getAttribute("aria-current")).toBe("true");
    expect(document.activeElement).toBe(within(next).getAllByRole("button")[0]);
    expect(within(viewer()).getByText("p3 / 3")).toBeTruthy();
  });

  it("A accepts the selected field", async () => {
    const bodies = onAccept(() => changed(resolved(checkField())));
    renderPage();
    const item = await row("Invoice total");

    await userEvent.keyboard("a");

    expect(await within(item).findByText("TH")).toBeTruthy();
    expect(bodies).toEqual([{ rowVersion: 1 }]);
  });

  it("E opens the editor and focuses it, and the key itself is not typed", async () => {
    renderPage();
    const item = await row("Invoice total");

    await userEvent.keyboard("e");

    const input = within(item).getByRole("textbox") as HTMLInputElement;
    expect(document.activeElement).toBe(input);
    expect(input.value).toBe("1,400,000");
  });

  it("typing in the editor is typing, not shortcuts", async () => {
    const bodies = onAccept(() => changed(resolved(checkField())));
    renderPage();
    const item = await row("Invoice total");
    await userEvent.keyboard("e");
    const input = within(item).getByRole("textbox") as HTMLInputElement;
    await userEvent.clear(input);

    await userEvent.keyboard("ane");

    expect(input.value).toBe("ane");
    expect(bodies).toEqual([]);
    expect(within(item).getAllByRole("button")[0]?.getAttribute("aria-current")).toBe("true");
  });

  it("Escape leaves the editor and returns to the row", async () => {
    renderPage();
    const item = await row("Invoice total");
    await userEvent.keyboard("e");

    await userEvent.keyboard("{Escape}");

    expect(within(item).queryByRole("textbox")).toBeNull();
    expect(document.activeElement).toBe(within(item).getAllByRole("button")[0]);
  });

  it("N walks the rows as they are shown, by document", async () => {
    // In the API's order Diagnosis comes second, but it is on another document and is drawn after Line 1 amount.
    const lineAmount = checkField({ id: "line", fieldKey: "line_amount", itemNo: 1 });
    state.fields = [checkField(), missingField(), lineAmount];
    renderPage();
    await row("Invoice total");

    await userEvent.keyboard("n");

    expect(within(rowNow("Line 1 amount")).getAllByRole("button")[0]?.getAttribute("aria-current")).toBe("true");
  });

  it("the arrow keys move through the rows as they are shown", async () => {
    renderPage();
    await row("Invoice total");

    await userEvent.keyboard("{ArrowDown}");
    expect(within(rowNow("Service date")).getAllByRole("button")[0]?.getAttribute("aria-current")).toBe("true");

    await userEvent.keyboard("{ArrowUp}");
    expect(within(rowNow("Invoice total")).getAllByRole("button")[0]?.getAttribute("aria-current")).toBe("true");
  });

  it("ignores a shortcut with a modifier, which belongs to the browser", async () => {
    const bodies = onAccept(() => changed(resolved(checkField())));
    renderPage();
    await row("Invoice total");

    await userEvent.keyboard("{Control>}a{/Control}");

    expect(bodies).toEqual([]);
  });

  it("Verify next field in the action bar does what N does", async () => {
    state.fields = [checkField(), missingField()];
    renderPage();
    await row("Invoice total");

    await userEvent.click(within(actionBar()).getByRole("button", { name: /Verify next field/ }));

    expect(within(rowNow("Diagnosis")).getAllByRole("button")[0]?.getAttribute("aria-current")).toBe("true");
  });
});

describe("an approver (read only)", () => {
  beforeEach(() => {
    state.claim = reviewerClaim({ status: "PENDING_APPROVAL", allowedActions: ["APPROVE", "REJECT"] });
    state.documents = [invoice(), summary({ pageType: "UNCLASSIFIED", pageTypeSource: null })];
  });

  it("sees the fields, markers and regions, and no way to change anything", async () => {
    renderPage("SENIOR_APPROVER");
    const item = await row("Invoice total");

    expect(within(item).getByText("AI")).toBeTruthy();
    expect(await within(viewer()).findByRole("img", { name: /Source region/ })).toBeTruthy();
    for (const name of ["Accept", "Edit", "Not present", "Enter value", "Remove line", "Add expense line", "Re-run AI on this page", "Start review"]) {
      expect(screen.queryByRole("button", { name })).toBeNull();
    }
    expect(screen.queryByRole("button", { name: /Verify next field/ })).toBeNull();
    expect(screen.queryByRole("combobox")).toBeNull();
  });

  it("can still move through the fields, but A and E do nothing", async () => {
    const bodies = onAccept(() => changed(resolved(checkField())));
    renderPage("SENIOR_APPROVER");
    const item = await row("Invoice total");

    await userEvent.keyboard("ae");
    expect(bodies).toEqual([]);
    expect(within(item).queryByRole("textbox")).toBeNull();

    await userEvent.keyboard("n");
    expect(within(rowNow("Diagnosis")).getAllByRole("button")[0]?.getAttribute("aria-current")).toBe("true");
  });
});

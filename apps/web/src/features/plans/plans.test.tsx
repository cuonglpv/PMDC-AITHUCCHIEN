import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import type { ReactNode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import type { components } from "@/lib/api/schema";
import type { StaffRole } from "@/lib/api/types";
import { API, authSession, server } from "@/test/server";
import { RuleSetEditor } from "./editor/RuleSetEditor";
import { VersionsPage } from "./VersionsPage";

type Detail = components["schemas"]["RuleSetVersionDetail"];

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push, replace: vi.fn() }) }));

const PLAN = "20000000-0000-4000-8000-000000000001";
const V3 = "21000000-0000-4000-8000-000000000003";
const DRAFT = "21000000-0000-4000-8000-000000000004";
const admin = { id: "a", fullName: "Đỗ Hải Nam", initials: "DN" };

const version = (overrides: Partial<Detail>): Detail => ({
  id: V3,
  planId: PLAN,
  versionNo: 3,
  status: "PUBLISHED",
  effectiveFrom: "2026-01-01",
  basedOnVersionId: null,
  changeNote: null,
  createdBy: admin,
  createdAt: "2026-01-01T00:00:00Z",
  publishedBy: admin,
  publishedAt: "2026-01-01T00:00:00Z",
  rules: [
    { id: "e", ruleCode: "ELIG-01", ruleType: "ELIGIBILITY", categoryCode: null, clauseText: "In force.", clauseRef: null, applyOrder: 0, rateBp: null, limitAmount: null, limitScope: null, waitingDays: null },
    { id: "c", ruleCode: "COV-01", ruleType: "COVERAGE", categoryCode: "CONSULTATION", clauseText: "Covered.", clauseRef: "3.1", applyOrder: 40, rateBp: null, limitAmount: null, limitScope: null, waitingDays: null },
    { id: "r", ruleCode: "COINS-80", ruleType: "COINSURANCE", categoryCode: null, clauseText: "80%.", clauseRef: null, applyOrder: 50, rateBp: 8000, limitAmount: null, limitScope: null, waitingDays: null },
  ],
  ...overrides,
});

const categories = [
  { code: "CONSULTATION", name: "Consultation", description: null },
  { code: "COSMETIC", name: "Cosmetic products", description: null },
];

function renderAs(role: StaffRole, ui: ReactNode) {
  session.start(authSession(role));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  push.mockClear();
  server.use(
    http.get(`${API}/plans`, () =>
      HttpResponse.json({ items: [{ id: PLAN, code: "GOLD-OP", name: "Gold Outpatient", description: null, currentVersion: { id: V3, versionNo: 3, effectiveFrom: "2026-01-01" }, draftVersionId: null }] }),
    ),
    http.get(`${API}/benefit-categories`, () => HttpResponse.json({ items: categories })),
  );
});

describe("Versions (SCR-41)", () => {
  // A summary is a version without its rules; the extra property does no harm in a mock.
  const summary = version({});

  it("lets the Administrator start a draft from the latest published version", async () => {
    let body: unknown;
    server.use(
      http.get(`${API}/plans/${PLAN}/versions`, () => HttpResponse.json({ items: [summary] })),
      http.post(`${API}/plans/${PLAN}/versions`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(version({ id: DRAFT, versionNo: 4, status: "DRAFT", effectiveFrom: null }), { status: 201 });
      }),
    );
    renderAs("ADMINISTRATOR", <VersionsPage planId={PLAN} />);

    await userEvent.setup().click(await screen.findByRole("button", { name: "New draft from v3" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith(`/plans/${PLAN}/versions/${DRAFT}`));
    expect(body).toEqual({ basedOnVersionId: V3 });
  });

  it("offers the open draft instead of a second one", async () => {
    server.use(
      http.get(`${API}/plans/${PLAN}/versions`, () =>
        HttpResponse.json({ items: [{ ...summary, id: DRAFT, versionNo: 4, status: "DRAFT", effectiveFrom: null }, summary] }),
      ),
    );
    renderAs("ADMINISTRATOR", <VersionsPage planId={PLAN} />);

    expect(await screen.findByRole("link", { name: "Open draft v4" })).toHaveProperty("href", expect.stringContaining(DRAFT));
    expect(screen.queryByRole("button", { name: /New draft/ })).toBeNull();
  });

  it("shows a reviewer the versions with no way to change them", async () => {
    server.use(http.get(`${API}/plans/${PLAN}/versions`, () => HttpResponse.json({ items: [summary] })));
    renderAs("CLAIMS_REVIEWER", <VersionsPage planId={PLAN} />);

    const row = await screen.findByRole("row", { name: /v3/ });
    expect(within(row).getByText("Published")).toBeTruthy();
    expect(within(row).getByText("01/01/2026")).toBeTruthy();
    expect(screen.queryByRole("button")).toBeNull();
  });
});

describe("Rule set editor (SCR-42)", () => {
  it("shows a published version read only, even to the Administrator", async () => {
    server.use(http.get(`${API}/plans/${PLAN}/versions/${V3}`, () => HttpResponse.json(version({}))));
    renderAs("ADMINISTRATOR", <RuleSetEditor planId={PLAN} versionId={V3} />);

    expect(await screen.findByText(/A published version cannot be changed/)).toBeTruthy();
    expect(screen.queryByRole("textbox")).toBeNull();
    expect(screen.queryByRole("button", { name: "Publish" })).toBeNull();
  });

  it("hides the system eligibility rule and counts the rest per tab", async () => {
    server.use(http.get(`${API}/plans/${PLAN}/versions/${V3}`, () => HttpResponse.json(version({}))));
    renderAs("CLAIMS_REVIEWER", <RuleSetEditor planId={PLAN} versionId={V3} />);

    expect(await screen.findByRole("tab", { name: "Categories (1)" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Coinsurance (1)" })).toBeTruthy();
    expect(screen.queryByText("ELIG-01")).toBeNull();
  });

  it("saves the whole list at once, with the rate in basis points", async () => {
    let body: { rules: { ruleCode: string; rateBp?: number }[] } | undefined;
    server.use(
      http.get(`${API}/plans/${PLAN}/versions/${DRAFT}`, () =>
        HttpResponse.json(version({ id: DRAFT, versionNo: 4, status: "DRAFT", effectiveFrom: null, publishedBy: null, publishedAt: null })),
      ),
      http.put(`${API}/plans/${PLAN}/versions/${DRAFT}/rules`, async ({ request }) => {
        body = (await request.json()) as typeof body;
        return HttpResponse.json(version({ id: DRAFT, status: "DRAFT" }));
      }),
    );
    renderAs("ADMINISTRATOR", <RuleSetEditor planId={PLAN} versionId={DRAFT} />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("tab", { name: "Coinsurance (1)" }));
    const rate = screen.getByLabelText("Insurer pays (%), COINS-80");
    await user.clear(rate);
    await user.type(rate, "70");
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    await waitFor(() => expect(screen.getByText("Saved")).toBeTruthy());
    expect(body!.rules.map((rule) => rule.ruleCode)).toEqual(["COV-01", "COINS-80"]);
    expect(body!.rules[1]!.rateBp).toBe(7000);
  });

  it("puts the API's errors next to the field and keeps the input", async () => {
    server.use(
      http.get(`${API}/plans/${PLAN}/versions/${DRAFT}`, () =>
        HttpResponse.json(version({ id: DRAFT, status: "DRAFT", effectiveFrom: null, publishedBy: null, publishedAt: null })),
      ),
      http.put(`${API}/plans/${PLAN}/versions/${DRAFT}/rules`, () =>
        HttpResponse.json(
          {
            title: "A rule is missing the values of its type",
            status: 422,
            code: "RULE_INVALID",
            requestId: "t",
            errors: [{ field: "rules[0].categoryCode", code: "REQUIRED", message: "Choose a benefit category." }],
          },
          { status: 422, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );
    renderAs("ADMINISTRATOR", <RuleSetEditor planId={PLAN} versionId={DRAFT} />);
    const user = userEvent.setup();

    const category = await screen.findByLabelText("Category, COV-01");
    await user.selectOptions(category, "");
    await user.click(screen.getByRole("button", { name: "Save draft" }));

    expect(await screen.findByText("Choose a benefit category.")).toBeTruthy();
    expect(category.getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByRole("alert").textContent).toContain("A rule is missing the values of its type");
    expect(screen.getByText("Unsaved changes")).toBeTruthy();
  });

  it("warns about categories with no rule before publishing", async () => {
    server.use(
      http.get(`${API}/plans/${PLAN}/versions/${DRAFT}`, () =>
        HttpResponse.json(version({ id: DRAFT, status: "DRAFT", effectiveFrom: null, publishedBy: null, publishedAt: null })),
      ),
    );
    renderAs("ADMINISTRATOR", <RuleSetEditor planId={PLAN} versionId={DRAFT} />);

    expect(await screen.findByText(/Not covered or excluded yet: Cosmetic products/)).toBeTruthy();
  });

  it("cannot publish unsaved changes", async () => {
    server.use(
      http.get(`${API}/plans/${PLAN}/versions/${DRAFT}`, () =>
        HttpResponse.json(version({ id: DRAFT, status: "DRAFT", effectiveFrom: null, publishedBy: null, publishedAt: null })),
      ),
    );
    renderAs("ADMINISTRATOR", <RuleSetEditor planId={PLAN} versionId={DRAFT} />);
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Clause text, COV-01"), " More.");

    expect((screen.getByRole("button", { name: "Publish" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("publishes with the chosen date and shows why the API refused one", async () => {
    const dates: string[] = [];
    server.use(
      http.get(`${API}/plans/${PLAN}/versions/${DRAFT}`, () =>
        HttpResponse.json(version({ id: DRAFT, versionNo: 4, status: "DRAFT", effectiveFrom: null, publishedBy: null, publishedAt: null })),
      ),
      http.post(`${API}/plans/${PLAN}/versions/${DRAFT}/publish`, async ({ request }) => {
        const { effectiveFrom } = (await request.json()) as { effectiveFrom: string };
        dates.push(effectiveFrom);
        return HttpResponse.json(
          {
            title: "The effective date must be later than the current version's",
            status: 422,
            code: "EFFECTIVE_DATE_NOT_LATER",
            requestId: "t",
            errors: [{ field: "effectiveFrom", code: "NOT_LATER", message: "It must be later than 01/01/2026." }],
          },
          { status: 422, headers: { "Content-Type": "application/problem+json" } },
        );
      }),
    );
    renderAs("ADMINISTRATOR", <RuleSetEditor planId={PLAN} versionId={DRAFT} />);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Publish" }));
    await user.type(await screen.findByLabelText("Effective from"), "2026-01-01");
    await user.click(screen.getByRole("button", { name: "Publish v4" }));

    expect(await screen.findByText("It must be later than 01/01/2026.")).toBeTruthy();
    expect(dates).toEqual(["2026-01-01"]);
  });
});

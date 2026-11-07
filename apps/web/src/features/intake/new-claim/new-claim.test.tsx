import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import type { ClaimSaved } from "@/lib/api/types";
import { CLAIM_ID, claimDetail, MEMBER_ID, POLICY_ID, policySummary, searchResult } from "@/test/intake";
import { API, authSession, server } from "@/test/server";
import { intakeKeys } from "../queries";
import { NewClaimPage } from "./NewClaimPage";

const push = vi.fn();
let search = "";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn() }),
  useSearchParams: () => new URLSearchParams(search),
}));

function renderPage() {
  session.start(authSession("INTAKE_OFFICER"));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <NewClaimPage />
    </QueryClientProvider>,
  );
  return client;
}

const summaryDates: (string | null)[] = [];

beforeEach(() => {
  push.mockClear();
  search = "";
  summaryDates.length = 0;
  server.use(
    http.get(`${API}/policies/${POLICY_ID}/summary`, ({ request }) => {
      const serviceDate = new URL(request.url).searchParams.get("serviceDate");
      summaryDates.push(serviceDate);
      return HttpResponse.json(policySummary({ asOfDate: serviceDate ?? "2026-11-07" }));
    }),
  );
});

async function findMember(user: ReturnType<typeof userEvent.setup>, query = "nguyen van an") {
  await user.type(screen.getByLabelText("Member name, national ID or policy number"), query);
  await user.click(screen.getByRole("button", { name: "Search" }));
}

describe("New claim, member and policy (SCR-11)", () => {
  it("searches with a POST, so a citizen ID never reaches the URL", async () => {
    let body: unknown;
    let url = "";
    server.use(
      http.post(`${API}/members/search`, async ({ request }) => {
        url = request.url;
        body = await request.json();
        return HttpResponse.json({ items: [searchResult()] });
      }),
    );
    renderPage();

    await findMember(userEvent.setup(), "079088004821");

    await screen.findByRole("row", { name: /Nguyễn Văn An/ });
    expect(body).toEqual({ query: "079088004821" });
    expect(url).not.toContain("079088004821");
  });

  it("shows each match's masked ID, policy status, coverage period and remaining balance", async () => {
    server.use(
      http.post(`${API}/members/search`, () =>
        HttpResponse.json({
          items: [
            searchResult(),
            searchResult({
              policy: policySummary({
                policyId: "lapsed",
                policyNumber: "PL-2025-00731",
                status: "LAPSED",
                coverageStart: "2025-01-01",
                coverageEnd: "2025-12-31",
                remainingBalance: null,
              }),
            }),
          ],
        }),
      ),
    );
    renderPage();

    await findMember(userEvent.setup());

    const row = await screen.findByRole("row", { name: /PL-2026-00731/ });
    expect(within(row).getByText("********4821")).toBeTruthy();
    expect(within(row).getByText("Active")).toBeTruthy();
    expect(within(row).getByText("01/01/2026 – 31/12/2026")).toBeTruthy();
    expect(within(row).getByText("800,000 ₫")).toBeTruthy();
    expect(within(screen.getByRole("row", { name: /PL-2025-00731/ })).getByText("Lapsed")).toBeTruthy();
  });

  it("says No member found, and nothing can be created", async () => {
    server.use(http.post(`${API}/members/search`, () => HttpResponse.json({ items: [] })));
    renderPage();

    await findMember(userEvent.setup(), "khong co ai");

    expect(await screen.findByText("No member found")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Continue" })).toHaveProperty("disabled", true);
  });

  it("needs two characters before it searches", async () => {
    let searched = false;
    server.use(
      http.post(`${API}/members/search`, () => {
        searched = true;
        return HttpResponse.json({ items: [] });
      }),
    );
    renderPage();

    await findMember(userEvent.setup(), "a");

    expect(await screen.findByText("Type at least 2 characters.")).toBeTruthy();
    expect(searched).toBe(false);
  });
});

describe("New claim, claim details (SCR-12)", () => {
  it("creates the claim at the end of step 2 and goes to its documents with the warnings", async () => {
    let body: unknown;
    const saved: ClaimSaved = {
      claim: claimDetail(),
      warnings: [{ code: "COVERAGE_OUTSIDE_PERIOD", message: "Service date outside coverage." }],
    };
    server.use(
      http.post(`${API}/members/search`, () => HttpResponse.json({ items: [searchResult()] })),
      http.post(`${API}/claims`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(saved, { status: 201 });
      }),
    );
    const client = renderPage();
    const user = userEvent.setup();

    await findMember(user);
    await user.click(await screen.findByRole("radio", { name: "Choose Nguyễn Văn An, policy PL-2026-00731" }));
    expect(await screen.findByText("of 5,000,000 ₫, as of 07/11/2026")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Continue" }));

    await user.type(screen.getByLabelText("Service date"), "12/10/2026");
    await user.type(screen.getByLabelText("Provider"), "Phòng khám Đa khoa Hồng Ngọc");
    await user.type(screen.getByLabelText("Claimed total (₫)"), "1,400,000");
    await user.click(screen.getByLabelText("Counter"));
    await waitFor(() => expect(summaryDates).toContain("2026-10-12"));
    await user.click(screen.getByRole("button", { name: "Create claim" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith(`/intake/${CLAIM_ID}`));
    expect(body).toEqual({
      memberId: MEMBER_ID,
      policyId: POLICY_ID,
      serviceDate: "2026-10-12",
      providerName: "Phòng khám Đa khoa Hồng Ngọc",
      claimedTotal: 1_400_000,
      channel: "COUNTER",
    });
    expect(client.getQueryData(intakeKeys.warnings(CLAIM_ID))).toEqual(saved.warnings);
  });

  it("keeps the officer on step 2 until the details are complete", async () => {
    let created = false;
    server.use(
      http.post(`${API}/members/search`, () => HttpResponse.json({ items: [searchResult()] })),
      http.post(`${API}/claims`, () => {
        created = true;
        return HttpResponse.json({}, { status: 201 });
      }),
    );
    renderPage();
    const user = userEvent.setup();

    await findMember(user);
    await user.click(await screen.findByRole("radio", { name: /Choose Nguyễn Văn An/ }));
    await user.click(screen.getByRole("button", { name: "Continue" }));
    await user.type(screen.getByLabelText("Claimed total (₫)"), "1.4 trieu");
    await user.click(screen.getByRole("button", { name: "Create claim" }));

    expect(await screen.findByText("Enter the service date.")).toBeTruthy();
    expect(screen.getByText("Enter the provider's name.")).toBeTruthy();
    expect(screen.getByText("Enter a whole amount in VND, such as 1,400,000.")).toBeTruthy();
    expect(screen.getByText("Choose how the claim arrived.")).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByLabelText("Service date"));

    // Dates are typed day first, as on the claim form, and must exist.
    await user.type(screen.getByLabelText("Service date"), "31/02/2026");
    await user.click(screen.getByRole("button", { name: "Create claim" }));
    expect(await screen.findByText("Enter the date as dd/mm/yyyy, such as 12/10/2026.")).toBeTruthy();
    expect(created).toBe(false);
  });

  it("shows the API's field problems next to their fields", async () => {
    server.use(
      http.post(`${API}/members/search`, () => HttpResponse.json({ items: [searchResult()] })),
      http.post(`${API}/claims`, () =>
        HttpResponse.json(
          {
            title: "Check the highlighted fields",
            status: 422,
            code: "VALIDATION_FAILED",
            requestId: "t",
            errors: [{ field: "serviceDate", code: "OUT_OF_RANGE", message: "The service date cannot be in the future." }],
          },
          { status: 422, headers: { "Content-Type": "application/problem+json" } },
        ),
      ),
    );
    renderPage();
    const user = userEvent.setup();

    await findMember(user);
    await user.click(await screen.findByRole("radio", { name: /Choose Nguyễn Văn An/ }));
    await user.click(screen.getByRole("button", { name: "Continue" }));
    await user.type(screen.getByLabelText("Service date"), "05/01/2027");
    await user.type(screen.getByLabelText("Provider"), "Bệnh viện Bạch Mai");
    await user.type(screen.getByLabelText("Claimed total (₫)"), "350000");
    await user.click(screen.getByLabelText("Email"));
    await user.click(screen.getByRole("button", { name: "Create claim" }));

    expect(await screen.findByText("The service date cannot be in the future.")).toBeTruthy();
    expect(screen.getByLabelText("Service date").getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByLabelText("Provider")).toHaveProperty("value", "Bệnh viện Bạch Mai");
    expect(push).not.toHaveBeenCalled();
  });
});

describe("Editing a created draft", () => {
  it("sends only what changed, with the row version", async () => {
    search = `claimId=${CLAIM_ID}`;
    let body: unknown;
    server.use(
      http.get(`${API}/claims/${CLAIM_ID}`, () => HttpResponse.json(claimDetail())),
      http.patch(`${API}/claims/${CLAIM_ID}`, async ({ request }) => {
        body = await request.json();
        return HttpResponse.json({ claim: claimDetail({ rowVersion: 4 }), warnings: [] });
      }),
    );
    renderPage();
    const user = userEvent.setup();

    const provider = await screen.findByLabelText("Provider");
    expect(screen.getByLabelText("Service date")).toHaveProperty("value", "12/10/2026");
    expect(screen.getByLabelText("Claimed total (₫)")).toHaveProperty("value", "1,400,000");
    await user.clear(provider);
    await user.type(provider, "Bệnh viện Đại học Y Dược");
    await user.click(screen.getByRole("button", { name: "Save and continue" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith(`/intake/${CLAIM_ID}`));
    expect(body).toEqual({ rowVersion: 3, providerName: "Bệnh viện Đại học Y Dược" });
  });

  it("keeps the officer's input on a version conflict and shows what the other person changed", async () => {
    search = `claimId=${CLAIM_ID}`;
    let current = claimDetail();
    const versions: number[] = [];
    server.use(
      http.get(`${API}/claims/${CLAIM_ID}`, () => HttpResponse.json(current)),
      http.patch(`${API}/claims/${CLAIM_ID}`, async ({ request }) => {
        const sent = (await request.json()) as { rowVersion: number };
        versions.push(sent.rowVersion);
        if (sent.rowVersion === 3) {
          current = claimDetail({ rowVersion: 4, claimedTotal: 1_500_000 });
          return HttpResponse.json(
            {
              title: "Another user changed this claim",
              status: 409,
              code: "VERSION_CONFLICT",
              requestId: "t",
              details: { currentRowVersion: 4, current: { claimedTotal: 1_500_000 } },
            },
            { status: 409, headers: { "Content-Type": "application/problem+json" } },
          );
        }
        return HttpResponse.json({ claim: current, warnings: [] });
      }),
    );
    renderPage();
    const user = userEvent.setup();

    const provider = await screen.findByLabelText("Provider");
    await user.clear(provider);
    await user.type(provider, "Bệnh viện Chợ Rẫy");
    await user.click(screen.getByRole("button", { name: "Save and continue" }));

    const notice = await screen.findByRole("alert");
    expect(notice.textContent).toContain("Someone else changed this claim");
    expect(within(notice).getByText("1,500,000 ₫")).toBeTruthy();
    expect(screen.getByLabelText("Provider")).toHaveProperty("value", "Bệnh viện Chợ Rẫy");

    await user.click(within(notice).getByRole("button", { name: "Load the latest version" }));
    await waitFor(() => expect(screen.queryByRole("alert")).toBeNull());
    expect(screen.getByLabelText("Provider")).toHaveProperty("value", "Bệnh viện Chợ Rẫy");
    await user.click(screen.getByRole("button", { name: "Save and continue" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith(`/intake/${CLAIM_ID}`));
    expect(versions).toEqual([3, 4]);
  });

  it("does not offer to edit a submitted claim", async () => {
    search = `claimId=${CLAIM_ID}`;
    server.use(
      http.get(`${API}/claims/${CLAIM_ID}`, () =>
        HttpResponse.json(claimDetail({ status: "PROCESSING", allowedActions: [] })),
      ),
    );
    renderPage();

    expect(await screen.findByText("This claim can no longer be edited")).toBeTruthy();
    expect(screen.queryByLabelText("Provider")).toBeNull();
  });
});

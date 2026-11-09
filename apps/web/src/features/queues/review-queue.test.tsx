import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import type { ClaimQueueItem, ClaimQueuePage } from "@/lib/api/types";
import { CLAIM_ID, queueItem } from "@/test/intake";
import { API, authSession, problem, server } from "@/test/server";
import { reviewerClaim } from "@/test/workspace";
import { ReviewQueuePage } from "./ReviewQueuePage";

const navigation = vi.hoisted(() => ({ push: vi.fn(), replace: vi.fn(), search: "" }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
  useSearchParams: () => new URLSearchParams(navigation.search),
}));

const OTHER_ID = "30000000-0000-4000-8000-000000000199";

function available(overrides: Partial<ClaimQueueItem> = {}): ClaimQueueItem {
  return queueItem({
    claimNumber: "CLM-2026-000203",
    status: "NEEDS_VERIFICATION",
    memberName: null,
    priority: "HIGH",
    claimedTotal: 7_800_000,
    ...overrides,
  });
}

const COUNTS = { AVAILABLE: 4, ASSIGNED_TO_ME: 12, NEEDS_VERIFICATION: 5, RETURNED: 1 };

function pageOf(items: ClaimQueueItem[], overrides: Partial<ClaimQueuePage> = {}): ClaimQueuePage {
  return { items, page: 1, pageSize: 25, total: items.length, counts: COUNTS, ...overrides };
}

const asked: URLSearchParams[] = [];
let answer: (query: URLSearchParams) => Response;

beforeEach(() => {
  asked.length = 0;
  navigation.push.mockClear();
  navigation.replace.mockClear();
  navigation.search = "";
  answer = () => HttpResponse.json(pageOf([available()]));
  server.use(
    http.get(`${API}/claims`, ({ request }) => {
      const query = new URL(request.url).searchParams;
      asked.push(query);
      return answer(query);
    }),
  );
});

function renderPage() {
  session.start(authSession("CLAIMS_REVIEWER"));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <ReviewQueuePage />
    </QueryClientProvider>,
  );
}

const lastView = () => asked.at(-1)?.get("view");

describe("SCR-20 tabs (US23)", () => {
  it("opens on Available and shows every tab with its count", async () => {
    renderPage();

    expect(await screen.findByRole("tab", { name: "Available 4" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Assigned to me 12" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Needs verification 5" })).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Returned 1" })).toBeTruthy();
    expect(lastView()).toBe("AVAILABLE");
  });

  it.each([
    ["Assigned to me 12", "ASSIGNED_TO_ME", "assigned"],
    ["Needs verification 5", "NEEDS_VERIFICATION", "verification"],
    ["Returned 1", "RETURNED", "returned"],
  ])("asks the API for the %s tab", async (name, view, tab) => {
    renderPage();
    await userEvent.click(await screen.findByRole("tab", { name }));

    await waitFor(() => expect(lastView()).toBe(view));
    expect(navigation.replace).toHaveBeenCalledWith(`/queue?tab=${tab}`, { scroll: false });
  });

  it("opens the tab named in the link", async () => {
    navigation.search = "tab=returned";
    renderPage();

    await waitFor(() => expect(lastView()).toBe("RETURNED"));
  });

  it("shows the rows in the order the API sent, and never sorts them itself", async () => {
    answer = () =>
      HttpResponse.json(
        pageOf([
          available({ id: "a", claimNumber: "CLM-2026-000300", claimedTotal: 100 }),
          available({ id: "b", claimNumber: "CLM-2026-000100", claimedTotal: 9_000_000 }),
        ]),
      );
    renderPage();

    await screen.findByText("CLM-2026-000300");
    const numbers = screen.getAllByText(/^CLM-2026-/).map((cell) => cell.textContent);
    expect(numbers).toEqual(["CLM-2026-000300", "CLM-2026-000100"]);
  });
});

describe("Available (BR-11)", () => {
  it("shows priority, number, plan, date, claimed total, status and age, and no member column", async () => {
    answer = () => HttpResponse.json(pageOf([available({ statusChangedAt: new Date(Date.now() - 27 * 3_600_000).toISOString() })]));
    renderPage();

    const row = (await screen.findByText("CLM-2026-000203")).closest("tr")!;
    expect(within(row).getByText("High")).toBeTruthy();
    expect(within(row).getByText("GOLD-OP")).toBeTruthy();
    expect(within(row).getByText("12/10/2026")).toBeTruthy();
    expect(within(row).getByText("7,800,000 ₫")).toBeTruthy();
    expect(within(row).getByText("Needs verification")).toBeTruthy();
    expect(within(row).getByText("1d 3h")).toBeTruthy();
    expect(screen.queryByRole("columnheader", { name: "Member" })).toBeNull();
  });

  it("does not show a member name even if one were sent", async () => {
    answer = () => HttpResponse.json(pageOf([available({ memberName: "Nguyễn Văn An" })]));
    renderPage();

    await screen.findByText("CLM-2026-000203");
    expect(screen.queryByText("Nguyễn Văn An")).toBeNull();
  });

  it("marks a normal claim as Normal", async () => {
    answer = () => HttpResponse.json(pageOf([available({ priority: "NORMAL" })]));
    renderPage();

    expect(await screen.findByText("Normal")).toBeTruthy();
  });
});

describe("the reviewer's own tabs", () => {
  it("shows the member and what needs attention, and opens the claim from its link", async () => {
    navigation.search = "tab=assigned";
    answer = () =>
      HttpResponse.json(
        pageOf([
          queueItem({
            status: "IN_REVIEW",
            memberName: "Nguyễn Văn An",
            fieldsToVerify: 3,
            blockingAlerts: 1,
            openFlags: 2,
          }),
        ]),
      );
    renderPage();

    expect(await screen.findByText("Nguyễn Văn An")).toBeTruthy();
    expect(screen.getByText(/fields to verify/).parentElement?.textContent).toContain("3");
    expect(screen.getByText(/blocking alerts/).parentElement?.textContent).toContain("1");
    expect(screen.getByText(/open flags/).parentElement?.textContent).toContain("2");
    expect(screen.queryByRole("button", { name: /Start review of/ })).toBeNull();
    expect(screen.getByRole("link", { name: "CLM-2026-000184" }).getAttribute("href")).toBe(`/claims/${CLAIM_ID}`);

    await userEvent.click(screen.getByText("Nguyễn Văn An"));
    expect(navigation.push).toHaveBeenCalledWith(`/claims/${CLAIM_ID}`);
  });

  it("shows a returned claim with the Returned pill", async () => {
    navigation.search = "tab=returned";
    answer = () => HttpResponse.json(pageOf([queueItem({ status: "IN_REVIEW", isReturned: true })]));
    renderPage();

    expect(await screen.findByText("Returned", { selector: "span" })).toBeTruthy();
  });
});

describe("Start review", () => {
  it("assigns the claim and opens it", async () => {
    server.use(http.post(`${API}/claims/${CLAIM_ID}/start-review`, () => HttpResponse.json(reviewerClaim())));
    renderPage();

    await userEvent.click(await screen.findByRole("button", { name: "Start review of CLM-2026-000203" }));

    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/claims/${CLAIM_ID}`));
  });

  it("says another reviewer took it on a 409 and refreshes the list", async () => {
    server.use(http.post(`${API}/claims/${CLAIM_ID}/start-review`, () => problem(409, "CLAIM_ALREADY_ASSIGNED")));
    renderPage();
    await screen.findByText("CLM-2026-000203");
    const before = asked.length;
    answer = () => HttpResponse.json(pageOf([]));

    await userEvent.click(screen.getByRole("button", { name: "Start review of CLM-2026-000203" }));

    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Another reviewer took this claim first");
    await waitFor(() => expect(asked.length).toBeGreaterThan(before));
    expect(await screen.findByText("No claims available")).toBeTruthy();
    expect(navigation.push).not.toHaveBeenCalled();
  });
});

describe("Open next claim", () => {
  it("takes the first claim the API offers, asking for one row", async () => {
    server.use(http.post(`${API}/claims/${CLAIM_ID}/start-review`, () => HttpResponse.json(reviewerClaim())));
    renderPage();
    await screen.findByText("CLM-2026-000203");

    await userEvent.click(screen.getByRole("button", { name: "Open next claim" }));

    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/claims/${CLAIM_ID}`));
    const single = asked.find((query) => query.get("pageSize") === "1");
    expect(single?.get("view")).toBe("AVAILABLE");
  });

  it("takes the next one when someone else got there first", async () => {
    const offered = [available(), available({ id: OTHER_ID, claimNumber: "CLM-2026-000199" })];
    answer = (query) => HttpResponse.json(pageOf(query.get("pageSize") === "1" ? offered.slice(0, 1) : offered));
    const started: string[] = [];
    server.use(
      http.post(`${API}/claims/:id/start-review`, ({ params }) => {
        started.push(String(params.id));
        if (params.id === CLAIM_ID) {
          offered.shift();
          return problem(409, "CLAIM_ALREADY_ASSIGNED");
        }
        return HttpResponse.json(reviewerClaim({ id: OTHER_ID }));
      }),
    );
    renderPage();
    await screen.findByText("CLM-2026-000203");

    await userEvent.click(screen.getByRole("button", { name: "Open next claim" }));

    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(`/claims/${OTHER_ID}`));
    expect(started).toEqual([CLAIM_ID, OTHER_ID]);
  });

  it("says so when nothing is available", async () => {
    answer = () => HttpResponse.json(pageOf([]));
    renderPage();
    await screen.findByText("No claims available");

    await userEvent.click(screen.getByRole("button", { name: "Open next claim" }));

    expect((await screen.findByRole("alert")).textContent).toContain("No claims are available right now");
    expect(navigation.push).not.toHaveBeenCalled();
  });
});

describe("filter and paging", () => {
  it("filters by claim number and tells that empty apart from an empty tab", async () => {
    renderPage();
    await screen.findByText("CLM-2026-000203");
    answer = () => HttpResponse.json(pageOf([]));

    await userEvent.type(screen.getByRole("textbox", { name: "Filter by claim no." }), "000999");

    expect(await screen.findByText("No results for this filter")).toBeTruthy();
    expect(asked.at(-1)?.get("claimNumber")).toBe("000999");
  });

  it("tells an empty tab apart", async () => {
    navigation.search = "tab=assigned";
    answer = () => HttpResponse.json(pageOf([]));
    renderPage();

    expect(await screen.findByText("No claims assigned to you")).toBeTruthy();
  });

  it("moves between pages and shows where it is", async () => {
    answer = (query) =>
      HttpResponse.json(
        pageOf([available({ claimNumber: `CLM-2026-00000${query.get("page")}` })], {
          page: Number(query.get("page")),
          total: 30,
        }),
      );
    renderPage();

    expect(await screen.findByText("1–25 of 30")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Previous page" }) as HTMLButtonElement).disabled).toBe(true);

    await userEvent.click(screen.getByRole("button", { name: "Next page" }));

    expect(await screen.findByText("26–30 of 30")).toBeTruthy();
    expect(screen.getByText("CLM-2026-000002")).toBeTruthy();
    expect((screen.getByRole("button", { name: "Next page" }) as HTMLButtonElement).disabled).toBe(true);
  });
});

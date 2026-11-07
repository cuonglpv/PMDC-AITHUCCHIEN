import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import type { ClaimQueuePage } from "@/lib/api/types";
import { CLAIM_ID, queueItem } from "@/test/intake";
import { API, authSession, problem, server } from "@/test/server";
import { IntakeHomePage } from "./IntakeHomePage";

const push = vi.fn();
const replace = vi.fn();
let search = "";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace }),
  useSearchParams: () => new URLSearchParams(search),
}));

const pageOf = (items: ClaimQueuePage["items"], counts = { MY_DRAFTS: 2, MY_SUBMISSIONS: 5 }): ClaimQueuePage => ({
  items,
  page: 1,
  pageSize: 25,
  total: items.length,
  counts,
});

function renderPage() {
  session.start(authSession("INTAKE_OFFICER"));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <IntakeHomePage />
    </QueryClientProvider>,
  );
}

const views: string[] = [];

beforeEach(() => {
  push.mockClear();
  replace.mockClear();
  search = "";
  views.length = 0;
  server.use(
    http.get(`${API}/claims`, ({ request }) => {
      const view = new URL(request.url).searchParams.get("view")!;
      views.push(view);
      return HttpResponse.json(
        view === "MY_DRAFTS"
          ? pageOf([queueItem(), queueItem({ id: "c2", claimNumber: "CLM-2026-000185", memberName: "Lê Thị Mai" })])
          : pageOf([queueItem({ id: "c3", claimNumber: "CLM-2026-000170", status: "NEEDS_VERIFICATION" })]),
      );
    }),
  );
});

describe("Intake home (SCR-10)", () => {
  it("shows a skeleton while the drafts load", async () => {
    renderPage();
    expect(screen.getByLabelText("Loading drafts")).toBeTruthy();
    // Let the request finish inside this test, so it cannot answer the next one.
    await screen.findByRole("row", { name: /CLM-2026-000184/ });
  });

  it("lists the officer's drafts with a badge on each tab", async () => {
    renderPage();

    const row = await screen.findByRole("row", { name: /CLM-2026-000184/ });
    expect(within(row).getByText("Nguyễn Văn An")).toBeTruthy();
    expect(within(row).getByText("12/10/2026")).toBeTruthy();
    expect(within(row).getByText("1,400,000 ₫")).toBeTruthy();
    expect(screen.getByRole("tab", { name: "Drafts 2" }).getAttribute("aria-selected")).toBe("true");
    expect(screen.getByRole("tab", { name: "My submissions 5" })).toBeTruthy();
    expect(views).toEqual(["MY_DRAFTS"]);
  });

  it("opens a claim from its row", async () => {
    renderPage();

    await userEvent.setup().click(await screen.findByText("Lê Thị Mai"));

    expect(push).toHaveBeenCalledWith("/intake/c2");
    expect(screen.getByRole("link", { name: "CLM-2026-000184" }).getAttribute("href")).toBe(`/intake/${CLAIM_ID}`);
  });

  it("offers New claim", async () => {
    renderPage();
    expect(screen.getAllByRole("link", { name: /New claim/ })[0]!.getAttribute("href")).toBe("/intake/new");
    // Let the drafts request finish inside this test, so it cannot be counted by the next one.
    await screen.findByRole("row", { name: /CLM-2026-000184/ });
  });

  it("opens My submissions from ?tab=submitted, with each claim's status", async () => {
    search = "tab=submitted";
    renderPage();

    const row = await screen.findByRole("row", { name: /CLM-2026-000170/ });
    expect(within(row).getByText("Needs verification")).toBeTruthy();
    expect(screen.getByRole("tab", { name: /My submissions/ }).getAttribute("aria-selected")).toBe("true");
    expect(views).toEqual(["MY_SUBMISSIONS"]);
  });

  it("switches tab and keeps the choice in the URL", async () => {
    renderPage();
    await screen.findByRole("row", { name: /CLM-2026-000184/ });

    await userEvent.setup().click(screen.getByRole("tab", { name: /My submissions/ }));

    expect(await screen.findByRole("row", { name: /CLM-2026-000170/ })).toBeTruthy();
    expect(replace).toHaveBeenCalledWith("/intake?tab=submitted", { scroll: false });
  });

  it("tells an empty drafts list from an empty submissions list", async () => {
    server.use(http.get(`${API}/claims`, () => HttpResponse.json(pageOf([], { MY_DRAFTS: 0, MY_SUBMISSIONS: 0 }))));
    renderPage();

    expect(await screen.findByText("No drafts")).toBeTruthy();
    await userEvent.setup().click(screen.getByRole("tab", { name: /My submissions/ }));
    expect(await screen.findByText("No submitted claims yet")).toBeTruthy();
  });

  it("says when the list could not be loaded", async () => {
    server.use(http.get(`${API}/claims`, () => problem(500, "INTERNAL_ERROR")));
    renderPage();

    expect(await screen.findByRole("alert")).toHaveProperty(
      "textContent",
      expect.stringContaining("This page could not be loaded"),
    );
  });

  it("pages through a long list", async () => {
    server.use(
      http.get(`${API}/claims`, ({ request }) => {
        const pageNo = Number(new URL(request.url).searchParams.get("page"));
        return HttpResponse.json({
          ...pageOf([queueItem({ id: `c${pageNo}`, claimNumber: `CLM-2026-00010${pageNo}` })]),
          page: pageNo,
          total: 30,
        });
      }),
    );
    renderPage();
    const user = userEvent.setup();

    await screen.findByText("CLM-2026-000101");
    expect(screen.getByText("Page 1 of 2")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Next" }));

    await waitFor(() => expect(screen.getByText("CLM-2026-000102")).toBeTruthy());
  });
});

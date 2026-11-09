import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import type { StaffRole } from "@/lib/api/types";
import { CLAIM_ID, claimDetail } from "@/test/intake";
import { API, authSession, problem, server } from "@/test/server";
import { AppShell } from "./AppShell";

const navigation = vi.hoisted(() => ({ push: vi.fn(), replace: vi.fn(), pathname: "/queue" }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
  usePathname: () => navigation.pathname,
}));

beforeEach(() => {
  navigation.push.mockClear();
  navigation.pathname = "/queue";
});

function renderShell(role: StaffRole) {
  session.start(authSession(role));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <AppShell>
        <p>content</p>
      </AppShell>
    </QueryClientProvider>,
  );
}

const box = () => screen.getByRole("textbox", { name: "Go to claim no." });

describe("Go to claim (US23)", () => {
  it.each([
    ["CLAIMS_REVIEWER", `/claims/${CLAIM_ID}`],
    ["SENIOR_APPROVER", `/claims/${CLAIM_ID}`],
    ["INTAKE_OFFICER", `/intake/${CLAIM_ID}`],
  ] as const)("opens the claim where %s reads it", async (role, path) => {
    let asked = "";
    server.use(
      http.get(`${API}/claim-numbers/:number`, ({ params }) => {
        asked = String(params.number);
        return HttpResponse.json(claimDetail());
      }),
    );
    renderShell(role);

    await userEvent.type(box(), " clm-2026-000184 {Enter}");

    await waitFor(() => expect(navigation.push).toHaveBeenCalledWith(path));
    expect(asked).toBe("CLM-2026-000184");
    expect((box() as HTMLInputElement).value).toBe("");
  });

  it("is not offered to the Administrator, who reads no claims", () => {
    renderShell("ADMINISTRATOR");

    expect(screen.queryByRole("textbox", { name: "Go to claim no." })).toBeNull();
  });

  it.each([404, 403] as const)("shows one message for a %s, so a number reveals nothing", async (status) => {
    server.use(
      http.get(`${API}/claim-numbers/:number`, () =>
        problem(status, status === 404 ? "NOT_FOUND" : "FORBIDDEN_ROLE"),
      ),
    );
    renderShell("CLAIMS_REVIEWER");

    await userEvent.type(box(), "CLM-2026-999999{Enter}");

    expect((await screen.findByRole("alert")).textContent).toBe("No claim with this number that you can open.");
    expect(navigation.push).not.toHaveBeenCalled();
  });

  it("clears the message when the number changes", async () => {
    server.use(http.get(`${API}/claim-numbers/:number`, () => problem(404, "NOT_FOUND")));
    renderShell("CLAIMS_REVIEWER");
    await userEvent.type(box(), "CLM{Enter}");
    await screen.findByRole("alert");

    await userEvent.type(box(), "-");

    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("says the lookup failed when the API itself fails", async () => {
    server.use(http.get(`${API}/claim-numbers/:number`, () => problem(500, "INTERNAL_ERROR")));
    renderShell("CLAIMS_REVIEWER");

    await userEvent.type(box(), "CLM{Enter}");

    expect((await screen.findByRole("alert")).textContent).toContain("could not be looked up");
  });
});

describe("the sidebar", () => {
  it("is expanded on list pages", () => {
    renderShell("CLAIMS_REVIEWER");

    expect(screen.getByText("Trần Thu Hà")).toBeTruthy();
  });

  it("collapses to icons inside the workspace, keeping each link's name", () => {
    navigation.pathname = `/claims/${CLAIM_ID}/evidence`;
    renderShell("CLAIMS_REVIEWER");

    expect(screen.queryByText("Trần Thu Hà")).toBeNull();
    expect(screen.getByRole("link", { name: /Review queue/ }).getAttribute("title")).toBe("Review queue");
  });
});

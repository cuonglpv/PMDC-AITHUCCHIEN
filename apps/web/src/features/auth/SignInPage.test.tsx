import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import { API, authSession, problem, server } from "@/test/server";
import { SignInPage } from "./SignInPage";

const replace = vi.fn();
let search = "";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
  useSearchParams: () => new URLSearchParams(search),
}));

async function signIn() {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText("Work email"), "ha.tran@claimlens.test");
  await user.type(screen.getByLabelText("Password"), "a password");
  await user.click(screen.getByRole("button", { name: "Sign in" }));
}

describe("Sign in (US01)", () => {
  beforeEach(() => {
    replace.mockClear();
    search = "";
  });

  it.each([
    ["INTAKE_OFFICER", "/intake"],
    ["CLAIMS_REVIEWER", "/queue"],
    ["SENIOR_APPROVER", "/approvals"],
    ["ADMINISTRATOR", "/plans"],
  ] as const)("sends %s to %s", async (role, home) => {
    server.use(http.post(`${API}/auth/login`, () => HttpResponse.json(authSession(role))));
    render(<SignInPage />);

    await signIn();

    await waitFor(() => expect(replace).toHaveBeenCalledWith(home));
    expect(session.getState().status).toBe("signedIn");
  });

  it("returns to the page the user was on", async () => {
    search = "next=/claims/abc/evidence";
    server.use(http.post(`${API}/auth/login`, () => HttpResponse.json(authSession())));
    render(<SignInPage />);

    await signIn();

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/claims/abc/evidence"));
  });

  it("ignores a return target outside the app", async () => {
    search = "next=https://evil.test";
    server.use(http.post(`${API}/auth/login`, () => HttpResponse.json(authSession())));
    render(<SignInPage />);

    await signIn();

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/queue"));
  });

  it("shows one generic message for wrong credentials", async () => {
    server.use(http.post(`${API}/auth/login`, () => problem(401, "INVALID_CREDENTIALS")));
    render(<SignInPage />);

    await signIn();

    expect(await screen.findByRole("alert")).toHaveProperty("textContent", "Wrong email or password.");
    expect(replace).not.toHaveBeenCalled();
  });

  it("says when there were too many attempts", async () => {
    server.use(http.post(`${API}/auth/login`, () => problem(429, "TOO_MANY_ATTEMPTS")));
    render(<SignInPage />);

    await signIn();

    expect((await screen.findByRole("alert")).textContent).toContain("Too many sign-in attempts");
  });

  it("explains missing values and ties each to its field, without calling the API", async () => {
    render(<SignInPage />);

    await userEvent.setup().click(screen.getByRole("button", { name: "Sign in" }));

    const email = screen.getByLabelText("Work email");
    await waitFor(() => expect(email.getAttribute("aria-invalid")).toBe("true"));
    expect(screen.getByText("Enter your work email.").id).toBe(email.getAttribute("aria-describedby"));
  });

  it("can show the password", async () => {
    render(<SignInPage />);
    const password = screen.getByLabelText("Password");
    expect(password.getAttribute("type")).toBe("password");

    await userEvent.setup().click(screen.getByRole("button", { name: "Show password" }));

    expect(password.getAttribute("type")).toBe("text");
  });

  it("keeps the input after a failure", async () => {
    server.use(http.post(`${API}/auth/login`, () => problem(401, "INVALID_CREDENTIALS")));
    render(<SignInPage />);

    await signIn();
    await screen.findByRole("alert");

    expect((screen.getByLabelText("Work email") as HTMLInputElement).value).toBe("ha.tran@claimlens.test");
  });
});

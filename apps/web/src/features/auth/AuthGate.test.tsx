import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { session } from "@/lib/api/session";
import { CLAIM_ID } from "@/test/intake";
import { authSession } from "@/test/server";
import { AuthGate } from "./AuthGate";

const replace = vi.fn();
let pathname = "/intake";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
  usePathname: () => pathname,
}));

function renderAt(path: string) {
  pathname = path;
  return render(
    <AuthGate>
      <p>Intake screen</p>
    </AuthGate>,
  );
}

describe("role guard on the intake screens", () => {
  beforeEach(() => replace.mockClear());

  it.each(["/intake", "/intake/new", `/intake/${CLAIM_ID}`])("lets the Intake Officer open %s", (path) => {
    session.start(authSession("INTAKE_OFFICER"));
    renderAt(path);

    expect(screen.getByText("Intake screen")).toBeTruthy();
    expect(replace).not.toHaveBeenCalled();
  });

  it.each(["CLAIMS_REVIEWER", "SENIOR_APPROVER", "ADMINISTRATOR"] as const)(
    "sends a %s to the 403 page without showing intake",
    (role) => {
      session.start(authSession(role));
      renderAt("/intake/new");

      expect(screen.queryByText("Intake screen")).toBeNull();
      expect(replace).toHaveBeenCalledWith("/403");
    },
  );

  it("sends a signed-out visitor to Sign in, and back to the claim afterwards", () => {
    session.end();
    renderAt(`/intake/${CLAIM_ID}`);

    expect(screen.queryByText("Intake screen")).toBeNull();
    expect(replace).toHaveBeenCalledWith(`/sign-in?next=${encodeURIComponent(`/intake/${CLAIM_ID}`)}`);
  });
});

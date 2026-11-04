import { describe, expect, it } from "vitest";
import type { StaffRole } from "@/lib/api/types";
import { canOpen, navFor, ROLE_HOME, safeReturnPath } from "./roles";

const ROLES: StaffRole[] = ["INTAKE_OFFICER", "CLAIMS_REVIEWER", "SENIOR_APPROVER", "ADMINISTRATOR"];

describe("role routing", () => {
  it.each([
    ["INTAKE_OFFICER", "/intake"],
    ["CLAIMS_REVIEWER", "/queue"],
    ["SENIOR_APPROVER", "/approvals"],
    ["ADMINISTRATOR", "/plans"],
  ] as const)("%s lands on %s", (role, home) => {
    expect(ROLE_HOME[role]).toBe(home);
  });

  it.each(ROLES)("%s can open its own home", (role) => {
    expect(canOpen(role, ROLE_HOME[role])).toBe(true);
  });

  it("keeps the Administrator out of claim content (BR-11)", () => {
    expect(canOpen("ADMINISTRATOR", "/claims/abc/evidence")).toBe(false);
    expect(canOpen("ADMINISTRATOR", "/queue")).toBe(false);
  });

  it("keeps the Intake Officer out of the workspace", () => {
    expect(canOpen("INTAKE_OFFICER", "/claims/abc/overview")).toBe(false);
  });

  it("lets every role open Profile", () => {
    ROLES.forEach((role) => expect(canOpen(role, "/profile")).toBe(true));
  });
});

describe("navigation", () => {
  it("shows each role only what it can use", () => {
    expect(navFor("INTAKE_OFFICER").map((item) => item.label)).toEqual(["Intake"]);
    expect(navFor("CLAIMS_REVIEWER").map((item) => item.label)).toEqual(["Review queue", "Benefit plans"]);
    expect(navFor("ADMINISTRATOR").map((item) => item.label)).toEqual(["Benefit plans"]);
  });

  it("hides the Should item in the first release", () => {
    expect(navFor("SENIOR_APPROVER").map((item) => item.label)).toEqual(["Approval queue", "Benefit plans"]);
  });

  it("never links to a page a role cannot open", () => {
    ROLES.forEach((role) => navFor(role).forEach((item) => expect(canOpen(role, item.href)).toBe(true)));
  });
});

describe("return path after sign-in", () => {
  it("accepts a path inside the app", () => {
    expect(safeReturnPath("/claims/abc/evidence")).toBe("/claims/abc/evidence");
  });

  it.each(["https://evil.test", "//evil.test", "/\\evil.test", "evil"])("refuses %s", (target) => {
    expect(safeReturnPath(target)).toBeNull();
  });
});

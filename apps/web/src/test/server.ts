import { setupServer } from "msw/node";
import type { AuthSession, ErrorCode, StaffRole } from "@/lib/api/types";

export const server = setupServer();

export const API = "http://localhost:3000/api/v1";

export function authSession(role: StaffRole = "CLAIMS_REVIEWER", accessToken = "token-1"): AuthSession {
  return {
    accessToken,
    tokenType: "Bearer",
    expiresIn: 900,
    user: {
      id: "10000000-0000-4000-8000-000000000002",
      email: "ha.tran@claimlens.test",
      fullName: "Trần Thu Hà",
      initials: "TH",
      role,
    },
  };
}

export function problem(status: number, code: ErrorCode, title = "Problem") {
  return Response.json(
    { title, status, code, requestId: "test" },
    { status, headers: { "Content-Type": "application/problem+json" } },
  );
}

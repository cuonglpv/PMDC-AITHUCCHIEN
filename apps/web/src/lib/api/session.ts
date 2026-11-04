import type { AuthSession, StaffUser } from "./types";

/**
 * The signed-in session. The access token lives only here, in the page's memory, never in localStorage
 * (docs/07-api-contract.md). The refresh token is an HttpOnly cookie that JavaScript cannot read.
 */
export type SessionState =
  | { status: "unknown" }
  | { status: "signedIn"; user: StaffUser }
  | { status: "signedOut" };

let accessToken: string | null = null;
let state: SessionState = { status: "unknown" };
let refreshing: Promise<string | null> | null = null;
const listeners = new Set<() => void>();

function setState(next: SessionState) {
  state = next;
  listeners.forEach((listener) => listener());
}

export const session = {
  subscribe(listener: () => void) {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  },

  getState: () => state,

  getToken: () => accessToken,

  start(auth: AuthSession) {
    accessToken = auth.accessToken;
    setState({ status: "signedIn", user: auth.user });
  },

  end() {
    accessToken = null;
    setState({ status: "signedOut" });
  },

  /** Gets a new access token. Many callers at once share one request, so a token is never rotated twice in parallel. */
  refresh(): Promise<string | null> {
    refreshing ??= requestRefresh().finally(() => {
      refreshing = null;
    });
    return refreshing;
  },

  /** For tests: back to the state of a freshly loaded page. */
  reset() {
    accessToken = null;
    refreshing = null;
    state = { status: "unknown" };
  },
};

async function requestRefresh(): Promise<string | null> {
  try {
    const response = await fetch(`${apiOrigin()}/api/v1/auth/refresh`, { method: "POST" });
    if (!response.ok) {
      session.end();
      return null;
    }
    const auth = (await response.json()) as AuthSession;
    session.start(auth);
    return auth.accessToken;
  } catch {
    session.end();
    return null;
  }
}

export function apiOrigin() {
  return typeof window === "undefined" ? "" : window.location.origin;
}

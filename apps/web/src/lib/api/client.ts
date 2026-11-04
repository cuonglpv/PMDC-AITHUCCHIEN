import createClient from "openapi-fetch";
import type { paths } from "./schema";
import { apiOrigin, session } from "./session";
import type { Problem } from "./types";

function withToken(request: Request): Request {
  const token = session.getToken();
  if (token) {
    request.headers.set("Authorization", `Bearer ${token}`);
  }
  return request;
}

/**
 * Adds the access token. On a 401 AUTH_REQUIRED it refreshes and retries once (docs/07-api-contract.md).
 * If the refresh fails, the session has ended and the gate returns the user to Sign in.
 */
export async function authFetch(request: Request): Promise<Response> {
  const retry = request.clone();
  const response = await fetch(withToken(request));
  if (response.status !== 401) {
    return response;
  }

  const problem = (await response.clone().json().catch(() => null)) as Problem | null;
  if (problem?.code !== "AUTH_REQUIRED") {
    return response;
  }

  const token = await session.refresh();
  return token ? fetch(withToken(retry)) : response;
}

/** The only way the app reaches the API. Its paths and shapes are generated from docs/api/claims-api.yaml. */
export const api = createClient<paths>({
  baseUrl: `${apiOrigin()}/api/v1`,
  fetch: authFetch,
});

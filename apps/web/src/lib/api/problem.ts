import type { ErrorCode, Problem } from "./types";

/** A refusal from the API. Screens read `code`, never the title text. */
export class ApiProblem extends Error {
  constructor(
    readonly status: number,
    readonly problem: Problem | null,
  ) {
    super(problem?.title ?? `Request failed with ${status}`);
  }

  get code(): ErrorCode | undefined {
    return this.problem?.code;
  }

  get errors() {
    return this.problem?.errors ?? [];
  }
}

/** For an operation that answers 204 No Content: resolves with nothing, or throws the problem. */
export async function expectNoContent(call: Promise<{ error?: unknown; response: Response }>): Promise<void> {
  const { error, response } = await call;
  if (!response.ok) {
    throw new ApiProblem(response.status, (error as Problem | undefined) ?? null);
  }
}

/** Turns an openapi-fetch result into its data, or throws the problem, so TanStack Query sees a failure. */
export async function unwrap<T>(call: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await call;
  if (error !== undefined || data === undefined) {
    throw new ApiProblem(response.status, (error as Problem | undefined) ?? null);
  }
  return data;
}

import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { CLAIM_ID, document } from "@/test/intake";
import { API, authSession, problem, server } from "@/test/server";
import { ApiProblem } from "./problem";
import { session } from "./session";
import { isAbortError, uploadDocument } from "./upload";

const URL_ = `${API}/claims/${CLAIM_ID}/documents`;
const file = () => new File(["%PDF-1.7 hoa don"], "hoa-don.pdf", { type: "application/pdf" });

describe("uploadDocument", () => {
  it("sends one file as multipart with the access token, and reports progress", async () => {
    session.start(authSession("INTAKE_OFFICER", "abc"));
    let token: string | null = null;
    let name: string | undefined;
    server.use(
      http.post(URL_, async ({ request }) => {
        token = request.headers.get("authorization");
        name = ((await request.formData()).get("file") as File | null)?.name;
        return HttpResponse.json(document({ status: "CONVERTING", pageCount: null, pages: [] }), { status: 201 });
      }),
    );
    const progress: number[] = [];

    const stored = await uploadDocument(CLAIM_ID, file(), { onProgress: (value) => progress.push(value) });

    expect(stored.status).toBe("CONVERTING");
    expect(token).toBe("Bearer abc");
    expect(name).toBe("hoa-don.pdf");
    expect(progress.at(-1)).toBe(1);
  });

  it("refreshes once on AUTH_REQUIRED and sends the file again with the new token", async () => {
    session.start(authSession("INTAKE_OFFICER", "expired"));
    const tokens: (string | null)[] = [];
    server.use(
      http.post(URL_, ({ request }) => {
        const token = request.headers.get("authorization");
        tokens.push(token);
        return token === "Bearer fresh"
          ? HttpResponse.json(document(), { status: 201 })
          : problem(401, "AUTH_REQUIRED");
      }),
      http.post(`${API}/auth/refresh`, () => HttpResponse.json(authSession("INTAKE_OFFICER", "fresh"))),
    );

    await uploadDocument(CLAIM_ID, file());

    expect(tokens).toEqual(["Bearer expired", "Bearer fresh"]);
  });

  it("retries only once, so a bad token cannot loop", async () => {
    session.start(authSession("INTAKE_OFFICER", "expired"));
    let calls = 0;
    server.use(
      http.post(URL_, () => {
        calls += 1;
        return problem(401, "AUTH_REQUIRED");
      }),
      http.post(`${API}/auth/refresh`, () => HttpResponse.json(authSession("INTAKE_OFFICER"))),
    );

    await expect(uploadDocument(CLAIM_ID, file())).rejects.toMatchObject({ status: 401 });
    expect(calls).toBe(2);
  });

  it.each([
    [413, "FILE_TOO_LARGE"],
    [415, "UNSUPPORTED_FILE_TYPE"],
  ] as const)("throws the %s refusal with its code", async (status, code) => {
    session.start(authSession("INTAKE_OFFICER"));
    server.use(http.post(URL_, () => problem(status, code)));

    const refusal = await uploadDocument(CLAIM_ID, file()).catch((error: unknown) => error);

    expect(refusal).toBeInstanceOf(ApiProblem);
    expect((refusal as ApiProblem).code).toBe(code);
    expect((refusal as ApiProblem).status).toBe(status);
  });

  it("rejects with an AbortError when cancelled", async () => {
    session.start(authSession("INTAKE_OFFICER"));
    const controller = new AbortController();
    controller.abort();

    const error = await uploadDocument(CLAIM_ID, file(), { signal: controller.signal }).catch((reason: unknown) => reason);

    expect(isAbortError(error)).toBe(true);
  });
});

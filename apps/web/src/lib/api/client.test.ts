import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { API, authSession, problem, server } from "@/test/server";
import { api } from "./client";
import { session } from "./session";

const me = authSession().user;

describe("api client", () => {
  it("sends the access token from memory", async () => {
    session.start(authSession("CLAIMS_REVIEWER", "abc"));
    let seen: string | null = null;
    server.use(
      http.get(`${API}/me`, ({ request }) => {
        seen = request.headers.get("authorization");
        return HttpResponse.json(me);
      }),
    );

    await api.GET("/me");

    expect(seen).toBe("Bearer abc");
  });

  it("refreshes once on AUTH_REQUIRED and repeats the request with the new token", async () => {
    session.start(authSession("CLAIMS_REVIEWER", "expired"));
    const tokens: (string | null)[] = [];
    server.use(
      http.get(`${API}/me`, ({ request }) => {
        const token = request.headers.get("authorization");
        tokens.push(token);
        return token === "Bearer fresh" ? HttpResponse.json(me) : problem(401, "AUTH_REQUIRED");
      }),
      http.post(`${API}/auth/refresh`, () => HttpResponse.json(authSession("CLAIMS_REVIEWER", "fresh"))),
    );

    const { data } = await api.GET("/me");

    expect(data?.email).toBe(me.email);
    expect(tokens).toEqual(["Bearer expired", "Bearer fresh"]);
    expect(session.getToken()).toBe("fresh");
  });

  it("retries only once, so a bad token cannot loop", async () => {
    session.start(authSession("CLAIMS_REVIEWER", "expired"));
    let calls = 0;
    server.use(
      http.get(`${API}/me`, () => {
        calls += 1;
        return problem(401, "AUTH_REQUIRED");
      }),
      http.post(`${API}/auth/refresh`, () => HttpResponse.json(authSession())),
    );

    const { response } = await api.GET("/me");

    expect(response.status).toBe(401);
    expect(calls).toBe(2);
  });

  it("ends the session when the refresh fails, which sends the user to Sign in", async () => {
    session.start(authSession());
    server.use(
      http.get(`${API}/me`, () => problem(401, "AUTH_REQUIRED")),
      http.post(`${API}/auth/refresh`, () => problem(401, "REFRESH_TOKEN_INVALID")),
    );

    await api.GET("/me");

    expect(session.getState().status).toBe("signedOut");
    expect(session.getToken()).toBeNull();
  });

  it("does not refresh for INVALID_CREDENTIALS", async () => {
    let refreshed = false;
    server.use(
      http.post(`${API}/auth/login`, () => problem(401, "INVALID_CREDENTIALS")),
      http.post(`${API}/auth/refresh`, () => {
        refreshed = true;
        return HttpResponse.json(authSession());
      }),
    );

    await api.POST("/auth/login", { body: { email: "a@b.test", password: "x" } });

    expect(refreshed).toBe(false);
  });

  it("does not refresh for a 403", async () => {
    session.start(authSession());
    let refreshed = false;
    server.use(
      http.get(`${API}/me`, () => problem(403, "FORBIDDEN_ROLE")),
      http.post(`${API}/auth/refresh`, () => {
        refreshed = true;
        return HttpResponse.json(authSession());
      }),
    );

    const { response } = await api.GET("/me");

    expect(response.status).toBe(403);
    expect(refreshed).toBe(false);
  });

  it("shares one refresh between requests that fail together", async () => {
    session.start(authSession("CLAIMS_REVIEWER", "expired"));
    let refreshes = 0;
    server.use(
      http.get(`${API}/me`, ({ request }) =>
        request.headers.get("authorization") === "Bearer fresh" ? HttpResponse.json(me) : problem(401, "AUTH_REQUIRED"),
      ),
      http.post(`${API}/auth/refresh`, () => {
        refreshes += 1;
        return HttpResponse.json(authSession("CLAIMS_REVIEWER", "fresh"));
      }),
    );

    await Promise.all([api.GET("/me"), api.GET("/me"), api.GET("/me")]);

    expect(refreshes).toBe(1);
  });

  it("never keeps the token in web storage", async () => {
    session.start(authSession("CLAIMS_REVIEWER", "secret-token"));

    expect(JSON.stringify({ ...localStorage })).not.toContain("secret-token");
    expect(JSON.stringify({ ...sessionStorage })).not.toContain("secret-token");
  });
});

import { ClientErrorCode, ErrorCode } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";

import { REQUEST_ID_HEADER } from "../request-id";
import { problemResponse } from "./problem";

/**
 * The one writer behind every failure the server hop originates itself. Both
 * presets render through it, so this pins the envelope once: the API's shape
 * member for member, `requestId` in the body, and never a `traceId` — there is
 * no backend trace to have gotten one from.
 */
describe("problemResponse", () => {
  it("emits the API envelope with about:blank, the fixed copy, and the request id", async () => {
    const res: Response = problemResponse(404, ErrorCode.HTTP_NOT_FOUND, {
      requestId: "req-404",
    });

    expect(res.status).toBe(404);
    expect(res.headers.get("content-type")).toBe("application/problem+json");
    expect(res.headers.get(REQUEST_ID_HEADER)).toBe("req-404");
    expect(await res.json()).toEqual({
      type: "about:blank",
      title: "Not found",
      status: 404,
      code: ErrorCode.HTTP_NOT_FOUND,
      detail: "That could not be found.",
      requestId: "req-404",
    });
  });

  it("never names a traceId", async () => {
    const res: Response = problemResponse(503, ClientErrorCode.TRANSPORT_NETWORK_ERROR, {
      requestId: "req-503",
    });

    const body: Record<string, unknown> = (await res.json()) as Record<string, unknown>;
    expect(Object.keys(body)).not.toContain("traceId");
  });

  it("lets a caller replace the fixed detail, never the title", async () => {
    const res: Response = problemResponse(403, ClientErrorCode.BFF_CSRF_INVALID, {
      requestId: "req-403",
      detail: "Custom wording.",
    });

    const body: Record<string, unknown> = (await res.json()) as Record<string, unknown>;
    expect(body["title"]).toBe("CSRF token mismatch or missing");
    expect(body["detail"]).toBe("Custom wording.");
  });

  it("carries every Set-Cookie the caller accumulated, without mutating them", () => {
    const cookies: Headers = new Headers();
    cookies.append("set-cookie", "a=1; Path=/");
    cookies.append("set-cookie", "b=2; Path=/");

    const res: Response = problemResponse(401, ClientErrorCode.BFF_SESSION_REFRESH_FAILED, {
      requestId: "req-401",
      headers: cookies,
    });

    expect(res.headers.getSetCookie()).toEqual(["a=1; Path=/", "b=2; Path=/"]);
    expect(res.headers.get("content-type")).toBe("application/problem+json");
    expect(cookies.has("content-type")).toBe(false);
  });

  it.each([
    [404, ErrorCode.HTTP_NOT_FOUND],
    [401, ClientErrorCode.BFF_SESSION_MISSING],
    [401, ClientErrorCode.BFF_SESSION_REFRESH_FAILED],
    [403, ClientErrorCode.BFF_CSRF_INVALID],
    [401, ErrorCode.AUTH_UNAUTHENTICATED],
    [503, ClientErrorCode.TRANSPORT_NETWORK_ERROR],
    [504, ClientErrorCode.TRANSPORT_TIMEOUT],
  ])("ships a title and a sentence of detail for %i %s", async (status: number, code: string) => {
    const res: Response = problemResponse(status, code, { requestId: "req" });

    const body: Record<string, unknown> = (await res.json()) as Record<string, unknown>;
    expect(body["status"]).toBe(status);
    expect(body["code"]).toBe(code);
    expect(typeof body["title"]).toBe("string");
    expect(body["title"]).not.toBe("");
    expect(typeof body["detail"]).toBe("string");
    expect(body["detail"]).toMatch(/\.$/u);
  });

  it("falls back to the code as the title for a code without shipped copy", async () => {
    const res: Response = problemResponse(500, "Fork.Custom", { requestId: "req-500" });

    const body: Record<string, unknown> = (await res.json()) as Record<string, unknown>;
    expect(body["title"]).toBe("Fork.Custom");
    expect(body).not.toHaveProperty("detail");
  });
});

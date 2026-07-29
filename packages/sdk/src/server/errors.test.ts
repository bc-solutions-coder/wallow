import { describe, expect, it } from "vitest";

import { REQUEST_ID_HEADER } from "../request-id";
import { parseProblemDetails, redact, REDACTED, UNKNOWN_ERROR_CODE, WallowError } from "./errors";

function problemResponse(status: number): Response {
  return new Response(null, {
    status,
    headers: { "content-type": "application/problem+json" },
  });
}

/** A problem response that also carries the correlation header, as the API's does. */
function correlatedResponse(status: number, requestId: string): Response {
  return new Response(null, {
    status,
    headers: {
      "content-type": "application/problem+json",
      [REQUEST_ID_HEADER]: requestId,
    },
  });
}

describe("WallowError", () => {
  it("is an instance of Error and of WallowError", () => {
    const error: WallowError = new WallowError({
      status: 403,
      code: "FORBIDDEN",
      title: "Forbidden",
    });

    expect(error).toBeInstanceOf(Error);
    expect(error).toBeInstanceOf(WallowError);
  });

  it("exposes status, code, title, and detail", () => {
    const error: WallowError = new WallowError({
      status: 409,
      code: "TENANT_CONFLICT",
      title: "Conflict",
      detail: "Tenant slug already taken.",
    });

    expect(error.status).toBe(409);
    expect(error.code).toBe("TENANT_CONFLICT");
    expect(error.title).toBe("Conflict");
    expect(error.detail).toBe("Tenant slug already taken.");
  });

  it("uses the name WallowError and a message derived from title and detail", () => {
    const error: WallowError = new WallowError({
      status: 409,
      code: "TENANT_CONFLICT",
      title: "Conflict",
      detail: "Tenant slug already taken.",
    });

    expect(error.name).toBe("WallowError");
    expect(error.message).toContain("Conflict");
    expect(error.message).toContain("Tenant slug already taken.");
  });

  it("falls back to the title as the message when no detail is given", () => {
    const error: WallowError = new WallowError({
      status: 403,
      code: "FORBIDDEN",
      title: "Forbidden",
    });

    expect(error.message).toBe("Forbidden");
    expect(error.detail).toBeUndefined();
  });
});

describe("parseProblemDetails", () => {
  it("maps a well-formed RFC 7807 body with extensions.code", () => {
    const body: string = JSON.stringify({
      type: "https://tools.ietf.org/html/rfc9110#section-15.5.5",
      title: "Not Found",
      status: 404,
      detail: "Inquiry 42 does not exist.",
      instance: "/api/inquiries/42",
      extensions: { code: "INQUIRY_NOT_FOUND", traceId: "00-abc-def-01" },
    });

    const error: WallowError = parseProblemDetails(problemResponse(404), body);

    expect(error).toBeInstanceOf(WallowError);
    expect(error.status).toBe(404);
    expect(error.code).toBe("INQUIRY_NOT_FOUND");
    expect(error.title).toBe("Not Found");
    expect(error.detail).toBe("Inquiry 42 does not exist.");
  });

  it("reads a flattened top-level code when extensions are inlined", () => {
    const body: string = JSON.stringify({
      title: "Bad Request",
      status: 400,
      detail: "One or more validation errors occurred.",
      code: "VALIDATION_FAILED",
      traceId: "00-abc-def-01",
    });

    const error: WallowError = parseProblemDetails(problemResponse(400), body);

    expect(error.code).toBe("VALIDATION_FAILED");
    expect(error.status).toBe(400);
    expect(error.title).toBe("Bad Request");
  });

  it("prefers the response status when the body omits status", () => {
    const body: string = JSON.stringify({ title: "Gone" });

    const error: WallowError = parseProblemDetails(problemResponse(410), body);

    expect(error.status).toBe(410);
  });

  it("reads the code from the API's bare { succeeded, error } auth body", () => {
    const body: string = JSON.stringify({ succeeded: false, error: "no_mfa_session" });

    const error: WallowError = parseProblemDetails(problemResponse(401), body);

    expect(error.code).toBe("no_mfa_session");
    expect(error.status).toBe(401);
  });

  it("prefers a problem-details code over a co-occurring error member", () => {
    const body: string = JSON.stringify({
      title: "Bad Request",
      status: 400,
      code: "VALIDATION_FAILED",
      error: "ignore_me",
    });

    const error: WallowError = parseProblemDetails(problemResponse(400), body);

    expect(error.code).toBe("VALIDATION_FAILED");
  });

  it("ignores a non-string error member rather than stringifying it", () => {
    const body: string = JSON.stringify({
      title: "Bad Request",
      status: 400,
      error: { nested: "oauth-style-object" },
    });

    const error: WallowError = parseProblemDetails(problemResponse(400), body);

    expect(error.code).toBe(UNKNOWN_ERROR_CODE);
  });

  it("falls back to UNKNOWN when problem details carry no code", () => {
    const body: string = JSON.stringify({
      title: "Internal Server Error",
      status: 500,
    });

    const error: WallowError = parseProblemDetails(problemResponse(500), body);

    expect(error.code).toBe(UNKNOWN_ERROR_CODE);
    expect(error.title).toBe("Internal Server Error");
    expect(error.status).toBe(500);
  });

  it("synthesizes an UNKNOWN error for a non-JSON body", () => {
    const error: WallowError = parseProblemDetails(
      new Response(null, {
        status: 502,
        headers: { "content-type": "text/html" },
      }),
      "<html><body>Bad Gateway</body></html>",
    );

    expect(error).toBeInstanceOf(WallowError);
    expect(error.code).toBe(UNKNOWN_ERROR_CODE);
    expect(error.title).toBe("Unknown error");
    expect(error.status).toBe(502);
  });

  it("synthesizes an UNKNOWN error for an empty body", () => {
    const error: WallowError = parseProblemDetails(problemResponse(503), "");

    expect(error.code).toBe(UNKNOWN_ERROR_CODE);
    expect(error.title).toBe("Unknown error");
    expect(error.status).toBe(503);
  });

  it("synthesizes an UNKNOWN error for JSON that is not an object", () => {
    const error: WallowError = parseProblemDetails(problemResponse(500), JSON.stringify("boom"));

    expect(error.code).toBe(UNKNOWN_ERROR_CODE);
    expect(error.title).toBe("Unknown error");
    expect(error.status).toBe(500);
  });
});

/**
 * Request-id correlation (Wallow-pu6a.6.7).
 *
 * A `WallowError` is what a user's bug report is really about, so it has to name
 * the request that produced it. Two members do that: `requestId` is the
 * `x-request-id` the BFF stamped on the request and the API echoed back, and
 * `traceId` is the W3C trace id ASP.NET Core already puts in every problem
 * details body — the same id OTel exports the trace under.
 */
describe("WallowError correlation members", () => {
  it("exposes the request id and trace id it was constructed with", () => {
    const error: WallowError = new WallowError({
      status: 500,
      code: "INTERNAL",
      title: "Internal Server Error",
      requestId: "3f2504e0-4f89-11d3-9a0c-0305e82c3301",
      traceId: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
    });

    expect(error.requestId).toBe("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
    expect(error.traceId).toBe("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
  });

  it("leaves both undefined when neither was given", () => {
    const error: WallowError = new WallowError({
      status: 403,
      code: "FORBIDDEN",
      title: "Forbidden",
    });

    expect(error.requestId).toBeUndefined();
    expect(error.traceId).toBeUndefined();
  });
});

describe("parseProblemDetails correlation", () => {
  it("carries the response's x-request-id onto the error", () => {
    const body: string = JSON.stringify({ title: "Conflict", status: 409 });

    const error: WallowError = parseProblemDetails(
      correlatedResponse(409, "3f2504e0-4f89-11d3-9a0c-0305e82c3301"),
      body,
    );

    expect(error.requestId).toBe("3f2504e0-4f89-11d3-9a0c-0305e82c3301");
  });

  it("leaves requestId undefined when the response carries no correlation header", () => {
    const error: WallowError = parseProblemDetails(
      problemResponse(409),
      JSON.stringify({ title: "Conflict" }),
    );

    expect(error.requestId).toBeUndefined();
  });

  it("reads the trace id from extensions.traceId", () => {
    const body: string = JSON.stringify({
      title: "Not Found",
      status: 404,
      extensions: { code: "INQUIRY_NOT_FOUND", traceId: "00-abc-def-01" },
    });

    const error: WallowError = parseProblemDetails(problemResponse(404), body);

    expect(error.traceId).toBe("00-abc-def-01");
  });

  it("reads a flattened top-level traceId", () => {
    // The default ASP.NET Core problem details serializer hoists it to the root.
    const body: string = JSON.stringify({
      title: "Bad Request",
      status: 400,
      traceId: "00-flattened-trace-01",
    });

    const error: WallowError = parseProblemDetails(problemResponse(400), body);

    expect(error.traceId).toBe("00-flattened-trace-01");
  });

  it("prefers extensions.traceId over a flattened one, as it does for code", () => {
    const body: string = JSON.stringify({
      title: "Bad Request",
      status: 400,
      traceId: "00-from-root-01",
      extensions: { traceId: "00-from-extensions-01" },
    });

    const error: WallowError = parseProblemDetails(problemResponse(400), body);

    expect(error.traceId).toBe("00-from-extensions-01");
  });

  it("ignores a non-string traceId rather than coercing it", () => {
    const body: string = JSON.stringify({ title: "Bad Request", status: 400, traceId: 42 });

    const error: WallowError = parseProblemDetails(problemResponse(400), body);

    expect(error.traceId).toBeUndefined();
  });

  it("still carries the request id when the body is not problem details at all", () => {
    // The synthetic UNKNOWN path is exactly where correlation matters most: an
    // HTML error page from a gateway names nothing else the request can be
    // traced by.
    const error: WallowError = parseProblemDetails(
      correlatedResponse(502, "gateway-req-9"),
      "<html><body>Bad Gateway</body></html>",
    );

    expect(error.code).toBe(UNKNOWN_ERROR_CODE);
    expect(error.requestId).toBe("gateway-req-9");
    expect(error.traceId).toBeUndefined();
  });
});

describe("redact", () => {
  it("strips authorization and cookie headers", () => {
    const redacted: Record<string, unknown> = redact({
      authorization: "Bearer abc.def.ghi",
      Cookie: "wallow_session=super-secret-value",
      "set-cookie": "wallow_session=super-secret-value; HttpOnly",
      "content-type": "application/json",
    }) as Record<string, unknown>;

    expect(redacted["authorization"]).toBe(REDACTED);
    expect(redacted["Cookie"]).toBe(REDACTED);
    expect(redacted["set-cookie"]).toBe(REDACTED);
    expect(redacted["content-type"]).toBe("application/json");
  });

  it("strips token-shaped and credential-named members", () => {
    const redacted: Record<string, unknown> = redact({
      accessToken: "a.b.c",
      refresh_token: "rt-123",
      idToken: "x.y.z",
      clientSecret: "shhh",
      password: "hunter2",
      email: "user@example.com",
    }) as Record<string, unknown>;

    expect(redacted["accessToken"]).toBe(REDACTED);
    expect(redacted["refresh_token"]).toBe(REDACTED);
    expect(redacted["idToken"]).toBe(REDACTED);
    expect(redacted["clientSecret"]).toBe(REDACTED);
    expect(redacted["password"]).toBe(REDACTED);
    expect(redacted["email"]).toBe("user@example.com");
  });

  it("redacts nested objects and arrays without mutating the input", () => {
    const input: Record<string, unknown> = {
      request: {
        url: "https://api.example.com/inquiries",
        headers: { authorization: "Bearer abc.def.ghi" },
      },
      events: [{ cookie: "sid=1" }, { status: 500 }],
    };

    const redacted: Record<string, unknown> = redact(input) as Record<string, unknown>;
    const request: Record<string, unknown> = redacted["request"] as Record<string, unknown>;
    const headers: Record<string, unknown> = request["headers"] as Record<string, unknown>;
    const events: Record<string, unknown>[] = redacted["events"] as Record<string, unknown>[];

    expect(headers["authorization"]).toBe(REDACTED);
    expect(request["url"]).toBe("https://api.example.com/inquiries");
    expect(events[0]?.["cookie"]).toBe(REDACTED);
    expect(events[1]?.["status"]).toBe(500);
    expect(
      ((input["request"] as Record<string, unknown>)["headers"] as Record<string, unknown>)[
        "authorization"
      ],
    ).toBe("Bearer abc.def.ghi");
  });

  it("redacts token-shaped string values regardless of member name", () => {
    const redacted: Record<string, unknown> = redact({
      note: "Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature",
      jwt: "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature",
      title: "Not Found",
    }) as Record<string, unknown>;

    expect(redacted["note"]).toBe(REDACTED);
    expect(redacted["jwt"]).toBe(REDACTED);
    expect(redacted["title"]).toBe("Not Found");
  });

  it("passes primitives through untouched", () => {
    expect(redact(42)).toBe(42);
    expect(redact("plain text")).toBe("plain text");
    expect(redact(null)).toBeNull();
    expect(redact(undefined)).toBeUndefined();
  });
});

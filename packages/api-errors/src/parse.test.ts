import { describe, expect, it } from "vitest";

import { ApiFailure, isApiFailure } from "./failure";
import { failureFromResponse, toApiFailure } from "./parse";

/**
 * Every branch of the two parsers: pass-through, transport classification,
 * problem+json, OAuth bodies, and the unrecognised-response fallback, plus
 * the correlation and Retry-After headers that ride along on each.
 */

const BAD_REQUEST: number = 400;
const FORBIDDEN: number = 403;
const UNAUTHORIZED: number = 401;
const TOO_MANY_REQUESTS: number = 429;
const BAD_GATEWAY: number = 502;
const SERVICE_UNAVAILABLE: number = 503;
const GATEWAY_TIMEOUT: number = 504;
const CLIENT_CLOSED_REQUEST: number = 499;
const INTERNAL_SERVER_ERROR: number = 500;

function response(status: number, headers: Record<string, string> = {}): Response {
  return new Response(null, { status, headers });
}

function problemBody(overrides: Record<string, unknown> = {}): string {
  return JSON.stringify({
    type: "https://wallow.dev/errors/validation-failed",
    title: "The request is invalid.",
    status: BAD_REQUEST,
    code: "Validation.Failed",
    traceId: "00-trace-span-01",
    ...overrides,
  });
}

describe("failureFromResponse", () => {
  it("parses problem+json with its top-level code", () => {
    const failure: ApiFailure = failureFromResponse(
      response(BAD_REQUEST, { "x-request-id": "req-7" }),
      problemBody({ detail: "Name is required.", errors: { Name: ["Required"] } }),
    );

    expect(failure.status).toBe(BAD_REQUEST);
    expect(failure.code).toBe("Validation.Failed");
    expect(failure.title).toBe("The request is invalid.");
    expect(failure.detail).toBe("Name is required.");
    expect(failure.traceId).toBe("00-trace-span-01");
    expect(failure.requestId).toBe("req-7");
    expect(failure.fieldErrors).toEqual({ Name: ["Required"] });
    expect(failure.cause).toBeUndefined();
  });

  it("keeps the response status when the body disagrees", () => {
    const failure: ApiFailure = failureFromResponse(
      response(BAD_GATEWAY),
      problemBody({ status: BAD_REQUEST }),
    );

    expect(failure.status).toBe(BAD_GATEWAY);
  });

  it("falls back to the response status when the body carries none", () => {
    const failure: ApiFailure = failureFromResponse(
      response(BAD_REQUEST),
      problemBody({ status: undefined }),
    );

    expect(failure.status).toBe(BAD_REQUEST);
  });

  it("drops field errors that are not arrays of strings", () => {
    const failure: ApiFailure = failureFromResponse(
      response(BAD_REQUEST),
      problemBody({ errors: { Name: ["Required"], Age: "not an array", Tags: [1] } }),
    );

    expect(failure.fieldErrors).toEqual({ Name: ["Required"] });
  });

  it("leaves fieldErrors absent when no entry survives", () => {
    const failure: ApiFailure = failureFromResponse(
      response(BAD_REQUEST),
      problemBody({ errors: { Age: 3 } }),
    );

    expect(failure.fieldErrors).toBeUndefined();
  });

  it("supplies a title when the problem has none", () => {
    const failure: ApiFailure = failureFromResponse(
      response(BAD_REQUEST),
      problemBody({ title: undefined }),
    );

    expect(failure.title).toBe("Request failed");
  });

  it("normalises an OAuth error body to OAuth.<PascalCase>", () => {
    const failure: ApiFailure = failureFromResponse(
      response(BAD_REQUEST),
      JSON.stringify({
        error: "invalid_grant",
        error_description: "The refresh token has expired.",
      }),
    );

    expect(failure.code).toBe("OAuth.InvalidGrant");
    expect(failure.status).toBe(BAD_REQUEST);
    expect(failure.title).toBe("invalid_grant");
    expect(failure.detail).toBe("The refresh token has expired.");
  });

  it.each([
    ["invalid_client", "OAuth.InvalidClient"],
    ["unsupported_grant_type", "OAuth.UnsupportedGrantType"],
    ["access_denied", "OAuth.AccessDenied"],
    ["server_error", "OAuth.ServerError"],
    ["temporarily_unavailable", "OAuth.TemporarilyUnavailable"],
  ])("maps the OAuth token %s to %s", (token, code) => {
    expect(failureFromResponse(response(BAD_REQUEST), JSON.stringify({ error: token })).code).toBe(
      code,
    );
  });

  it("leaves detail absent when the OAuth body has no description", () => {
    const failure: ApiFailure = failureFromResponse(
      response(UNAUTHORIZED),
      JSON.stringify({ error: "invalid_client" }),
    );

    expect(failure.detail).toBeUndefined();
  });

  it.each([
    ["an HTML page", "<html><body>502 Bad Gateway</body></html>"],
    ["an empty body", ""],
    ["a JSON array", "[1, 2]"],
    ["a JSON object without a code", JSON.stringify({ message: "boom" })],
    ["a non-string code", JSON.stringify({ code: 12, title: "x" })],
  ])("treats %s as an unrecognised response", (_label, bodyText) => {
    const failure: ApiFailure = failureFromResponse(
      response(BAD_GATEWAY, { "x-request-id": "req-9" }),
      bodyText,
    );

    expect(failure.code).toBe("Client.UnrecognizedResponse");
    expect(failure.status).toBe(BAD_GATEWAY);
    expect(failure.detail).toBeUndefined();
    expect(failure.cause).toBe(bodyText);
    expect(failure.requestId).toBe("req-9");
  });

  it("reads Retry-After seconds", () => {
    const failure: ApiFailure = failureFromResponse(
      response(TOO_MANY_REQUESTS, { "retry-after": "30" }),
      problemBody({ status: TOO_MANY_REQUESTS, code: "RateLimit.Exceeded" }),
    );

    expect(failure.retryAfter).toBe(30);
  });

  it("reads an HTTP-date Retry-After as seconds from now", () => {
    const inSixtySeconds: string = new Date(Date.now() + 60_000).toUTCString();
    const failure: ApiFailure = failureFromResponse(
      response(TOO_MANY_REQUESTS, { "retry-after": inSixtySeconds }),
      "",
    );

    expect(failure.retryAfter).toBeGreaterThanOrEqual(58);
    expect(failure.retryAfter).toBeLessThanOrEqual(60);
  });

  it("clamps a Retry-After date in the past to zero", () => {
    const failure: ApiFailure = failureFromResponse(
      response(TOO_MANY_REQUESTS, { "retry-after": new Date(0).toUTCString() }),
      "",
    );

    expect(failure.retryAfter).toBe(0);
  });

  it.each(["", "soon", "-5", "1.5"])("ignores the unparseable Retry-After %j", (value) => {
    const failure: ApiFailure = failureFromResponse(
      response(TOO_MANY_REQUESTS, { "retry-after": value }),
      "",
    );

    expect(failure.retryAfter).toBeUndefined();
  });
});

describe("toApiFailure", () => {
  it("passes an existing failure through untouched", () => {
    const failure: ApiFailure = new ApiFailure({
      status: BAD_REQUEST,
      code: "Validation.Failed",
      title: "x",
    });

    expect(toApiFailure(failure)).toBe(failure);
    expect(toApiFailure(failure, { status: BAD_GATEWAY, requestId: "ignored" })).toBe(failure);
  });

  describe("a thrown error without a status", () => {
    it("classifies a fetch TypeError as Transport.NetworkError 503", () => {
      const error: TypeError = new TypeError("fetch failed");
      const failure: ApiFailure = toApiFailure(error);

      expect(failure.code).toBe("Transport.NetworkError");
      expect(failure.status).toBe(SERVICE_UNAVAILABLE);
      expect(failure.detail).toBeUndefined();
      expect(failure.cause).toBe(error);
      expect(failure.message).not.toContain("fetch failed");
    });

    it("classifies an unknown Error as Transport.NetworkError", () => {
      expect(toApiFailure(new Error("socket hang up")).code).toBe("Transport.NetworkError");
    });

    it("classifies a TimeoutError as Transport.Timeout 504", () => {
      const error: Error = new Error("The operation was aborted due to timeout");
      error.name = "TimeoutError";
      const failure: ApiFailure = toApiFailure(error);

      expect(failure.code).toBe("Transport.Timeout");
      expect(failure.status).toBe(GATEWAY_TIMEOUT);
      expect(failure.cause).toBe(error);
    });

    it.each([
      "UND_ERR_CONNECT_TIMEOUT",
      "UND_ERR_HEADERS_TIMEOUT",
      "UND_ERR_BODY_TIMEOUT",
      "ETIMEDOUT",
    ])("classifies an undici %s as Transport.Timeout", (code) => {
      const error: Error = Object.assign(new Error("timed out"), { code });

      expect(toApiFailure(error).code).toBe("Transport.Timeout");
    });

    it("classifies a timeout wrapped as the cause of a fetch failure", () => {
      const inner: Error = Object.assign(new Error("connect timeout"), {
        code: "UND_ERR_CONNECT_TIMEOUT",
      });
      const error: TypeError = new TypeError("fetch failed", { cause: inner });

      expect(toApiFailure(error).code).toBe("Transport.Timeout");
    });

    it("survives a cyclic cause chain", () => {
      const error: Error = new Error("fetch failed");
      Object.assign(error, { cause: error });

      expect(toApiFailure(error).code).toBe("Transport.NetworkError");
    });

    it("classifies an AbortError as Transport.Aborted 499", () => {
      const error: Error = new Error("This operation was aborted");
      error.name = "AbortError";
      const failure: ApiFailure = toApiFailure(error);

      expect(failure.code).toBe("Transport.Aborted");
      expect(failure.status).toBe(CLIENT_CLOSED_REQUEST);
    });

    it("classifies a real DOMException abort", () => {
      const error: DOMException = new DOMException("aborted", "AbortError");

      expect(toApiFailure(error).code).toBe("Transport.Aborted");
    });

    it("stamps the request id from the context", () => {
      expect(toApiFailure(new Error("x"), { requestId: "req-3" }).requestId).toBe("req-3");
    });
  });

  it("treats a thrown error with a status as an unrecognised response", () => {
    const error: Error = new Error("Unexpected token < in JSON");
    const failure: ApiFailure = toApiFailure(error, { status: BAD_GATEWAY, requestId: "req-4" });

    expect(failure.code).toBe("Client.UnrecognizedResponse");
    expect(failure.status).toBe(BAD_GATEWAY);
    expect(failure.detail).toBeUndefined();
    expect(failure.cause).toBe(error);
    expect(failure.requestId).toBe("req-4");
  });

  it("parses a plain problem object", () => {
    const failure: ApiFailure = toApiFailure(
      {
        status: BAD_REQUEST,
        code: "Validation.Failed",
        title: "The request is invalid.",
        detail: "Name is required.",
        traceId: "00-t-s-01",
        errors: { name: ["Required"] },
      },
      { requestId: "req-5" },
    );

    expect(isApiFailure(failure)).toBe(true);
    expect(failure.status).toBe(BAD_REQUEST);
    expect(failure.code).toBe("Validation.Failed");
    expect(failure.title).toBe("The request is invalid.");
    expect(failure.detail).toBe("Name is required.");
    expect(failure.traceId).toBe("00-t-s-01");
    expect(failure.fieldErrors).toEqual({ name: ["Required"] });
    expect(failure.requestId).toBe("req-5");
  });

  it("takes the status from the body when no response answered", () => {
    const failure: ApiFailure = toApiFailure({ code: "Auth.Forbidden", status: FORBIDDEN });

    expect(failure.status).toBe(FORBIDDEN);
  });

  it("prefers the context status over the body's when both are known", () => {
    const failure: ApiFailure = toApiFailure(
      { code: "Auth.Forbidden", status: FORBIDDEN },
      { status: BAD_REQUEST },
    );

    expect(failure.status).toBe(BAD_REQUEST);
  });

  it("takes the status from the context when the problem object carries none", () => {
    expect(
      toApiFailure({ code: "Auth.Unauthenticated", title: "x" }, { status: UNAUTHORIZED }).status,
    ).toBe(UNAUTHORIZED);
  });

  it("normalises a plain OAuth object", () => {
    const failure: ApiFailure = toApiFailure(
      { error: "invalid_grant", error_description: "expired" },
      { status: BAD_REQUEST },
    );

    expect(failure.code).toBe("OAuth.InvalidGrant");
    expect(failure.status).toBe(BAD_REQUEST);
    expect(failure.detail).toBe("expired");
  });

  it.each([
    ["a string", "boom"],
    ["a number", 7],
    ["undefined", undefined],
    ["null", null],
    ["an object without a code", { message: "boom" }],
  ])("treats %s as an unrecognised response at the context status", (_label, input) => {
    const failure: ApiFailure = toApiFailure(input, { status: BAD_GATEWAY });

    expect(failure.code).toBe("Client.UnrecognizedResponse");
    expect(failure.status).toBe(BAD_GATEWAY);
    expect(failure.detail).toBeUndefined();
    expect(failure.cause).toBe(input);
  });

  it("treats an unrecognised input without a status as a 500", () => {
    expect(toApiFailure({ message: "boom" }).status).toBe(INTERNAL_SERVER_ERROR);
  });

  it("never carries the input's text as detail", () => {
    expect(toApiFailure("Bearer secret-token").detail).toBeUndefined();
    expect(toApiFailure(new Error("Bearer secret-token")).detail).toBeUndefined();
  });
});

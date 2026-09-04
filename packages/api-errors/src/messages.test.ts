import { describe, expect, it } from "vitest";

import { ClientErrorCode } from "./codes";
import { ApiFailure } from "./failure";
import {
  defineFailureMessages,
  failureReference,
  type FailureMessageRegistry,
  isSilentFailure,
  resolveFailureMessage,
} from "./messages";

/**
 * The resolver, one step of its precedence at a time: call-site messages,
 * the app registry, the shipped code copy, the problem's own detail, the
 * shipped status copy, the call-site fallback, and the generic sentence.
 */

const BAD_REQUEST: number = 400;
const UNAUTHORIZED: number = 401;
const FORBIDDEN: number = 403;
const NOT_FOUND: number = 404;
const CONFLICT: number = 409;
const TOO_MANY_REQUESTS: number = 429;
const UNPROCESSABLE: number = 422;
const INTERNAL_SERVER_ERROR: number = 500;
const BAD_GATEWAY: number = 502;
const SERVICE_UNAVAILABLE: number = 503;
const GATEWAY_TIMEOUT: number = 504;
const CLIENT_CLOSED_REQUEST: number = 499;
const NO_WAIT: number = 0;
const ONE_SECOND: number = 1;

const GENERIC: string = "Something went wrong. Please try again.";
const SERVER_SIDE: string = "Something went wrong on our side. Please try again later.";
const SESSION_EXPIRED: string = "Your session has expired. Please sign in again.";

function failure(status: number, code: string, detail?: string, retryAfter?: number): ApiFailure {
  return new ApiFailure({ status, code, title: "Title", detail, retryAfter });
}

describe("resolveFailureMessage", () => {
  it("prefers a call-site message over everything", () => {
    const registry: FailureMessageRegistry = defineFailureMessages({
      "Validation.Failed": () => "From the registry",
    });

    expect(
      resolveFailureMessage(failure(BAD_REQUEST, "Validation.Failed", "From detail"), {
        registry,
        messages: { "Validation.Failed": () => "From the call site" },
        fallback: "From the fallback",
      }),
    ).toBe("From the call site");
  });

  it("prefers the app registry over the shipped copy and detail", () => {
    const registry: FailureMessageRegistry = defineFailureMessages({
      [ClientErrorCode.TRANSPORT_NETWORK_ERROR]: () => "Offline.",
      "Validation.Failed": (f) => `Registry saw ${f.status}`,
    });

    expect(
      resolveFailureMessage(failure(SERVICE_UNAVAILABLE, "Transport.NetworkError"), {
        registry,
      }),
    ).toBe("Offline.");
    expect(
      resolveFailureMessage(failure(BAD_REQUEST, "Validation.Failed", "From detail"), {
        registry,
      }),
    ).toBe("Registry saw 400");
  });

  it("hands the failure to a registry entry", () => {
    const registry: FailureMessageRegistry = defineFailureMessages({
      "RateLimit.Exceeded": (f) => `Wait ${f.retryAfter ?? 0}s`,
    });

    expect(
      resolveFailureMessage(failure(TOO_MANY_REQUESTS, "RateLimit.Exceeded", undefined, 12), {
        registry,
      }),
    ).toBe("Wait 12s");
  });

  it.each([
    [
      "Transport.NetworkError",
      SERVICE_UNAVAILABLE,
      "Unable to reach the server. Check your connection and try again.",
    ],
    [
      "Transport.Timeout",
      GATEWAY_TIMEOUT,
      "The server took too long to respond. Please try again.",
    ],
    ["Client.UnrecognizedResponse", BAD_GATEWAY, SERVER_SIDE],
    ["Bff.SessionRefreshFailed", UNAUTHORIZED, SESSION_EXPIRED],
  ])("ships copy for %s", (code, status, expected) => {
    expect(resolveFailureMessage(failure(status, code, "Raw detail that must not show"))).toBe(
      expected,
    );
  });

  it("uses a 4xx problem's detail when no copy is registered for its code", () => {
    expect(
      resolveFailureMessage(failure(UNPROCESSABLE, "Inquiry.Closed", "This inquiry is closed.")),
    ).toBe("This inquiry is closed.");
  });

  it("uses detail ahead of the shipped status copy", () => {
    expect(
      resolveFailureMessage(failure(NOT_FOUND, "Organization.NotFound", "No such organization.")),
    ).toBe("No such organization.");
  });

  it.each([
    [UNAUTHORIZED, SESSION_EXPIRED],
    [FORBIDDEN, "You don't have permission to do that."],
    [NOT_FOUND, "That could not be found."],
    [CONFLICT, "That change conflicts with a newer one. Refresh and try again."],
    [INTERNAL_SERVER_ERROR, SERVER_SIDE],
    [BAD_GATEWAY, SERVER_SIDE],
    [SERVICE_UNAVAILABLE, SERVER_SIDE],
  ])("ships copy for status %i", (status, expected) => {
    expect(resolveFailureMessage(failure(status, "Some.Code"))).toBe(expected);
  });

  it("ships both 429 variants", () => {
    expect(
      resolveFailureMessage(failure(TOO_MANY_REQUESTS, "RateLimit.Exceeded", undefined, 30)),
    ).toBe("Too many requests. Please wait 30 seconds and try again.");
    expect(resolveFailureMessage(failure(TOO_MANY_REQUESTS, "RateLimit.Exceeded"))).toBe(
      "Too many requests. Please wait a moment and try again.",
    );
  });

  it("treats a zero Retry-After as no wait and pluralises a single second", () => {
    expect(
      resolveFailureMessage(failure(TOO_MANY_REQUESTS, "RateLimit.Exceeded", undefined, NO_WAIT)),
    ).toBe("Too many requests. Please wait a moment and try again.");
    expect(
      resolveFailureMessage(
        failure(TOO_MANY_REQUESTS, "RateLimit.Exceeded", undefined, ONE_SECOND),
      ),
    ).toBe("Too many requests. Please wait 1 second and try again.");
  });

  it.each(["constructor", "valueOf", "toString", "__proto__", "hasOwnProperty"])(
    "ignores a wire code spelling the Object.prototype member %s",
    (code) => {
      const registry: FailureMessageRegistry = defineFailureMessages({ "Some.Code": () => "x" });

      expect(resolveFailureMessage(failure(BAD_REQUEST, code), { registry, messages: {} })).toBe(
        GENERIC,
      );
    },
  );

  it("never shows a 5xx problem's detail", () => {
    expect(
      resolveFailureMessage(
        failure(INTERNAL_SERVER_ERROR, "Server.Error", "NullReferenceException at …"),
      ),
    ).toBe(SERVER_SIDE);
  });

  it("never shows detail for a client-minted code", () => {
    expect(
      resolveFailureMessage(failure(FORBIDDEN, "Bff.CsrfInvalid", "token mismatch: abc")),
    ).toBe("You don't have permission to do that.");
    expect(resolveFailureMessage(failure(UNAUTHORIZED, "Bff.SessionMissing", "no cookie"))).toBe(
      SESSION_EXPIRED,
    );
  });

  it("falls through a 4xx without detail to the call-site fallback", () => {
    expect(
      resolveFailureMessage(failure(BAD_REQUEST, "Some.Code"), { fallback: "Could not save." }),
    ).toBe("Could not save.");
  });

  it("ends at the generic sentence", () => {
    expect(resolveFailureMessage(failure(BAD_REQUEST, "Some.Code"))).toBe(GENERIC);
    expect(resolveFailureMessage(failure(CLIENT_CLOSED_REQUEST, "Transport.Aborted"))).toBe(
      GENERIC,
    );
  });

  it("classifies a non-failure input rather than echoing its message", () => {
    expect(resolveFailureMessage(new Error("ECONNREFUSED 127.0.0.1:5001"))).toBe(
      "Unable to reach the server. Check your connection and try again.",
    );
    expect(resolveFailureMessage("boom")).toBe(SERVER_SIDE);
    expect(resolveFailureMessage(undefined, { fallback: "Fallback" })).toBe(SERVER_SIDE);
  });

  it("always returns a string", () => {
    expect(typeof resolveFailureMessage(null)).toBe("string");
  });
});

describe("defineFailureMessages", () => {
  it("returns the registry it was given", () => {
    const entries: FailureMessageRegistry = { "Validation.Failed": () => "x" };

    expect(defineFailureMessages(entries)).toBe(entries);
  });
});

describe("isSilentFailure", () => {
  it("is true for Transport.Aborted only", () => {
    expect(isSilentFailure(failure(CLIENT_CLOSED_REQUEST, "Transport.Aborted"))).toBe(true);
    expect(isSilentFailure(failure(GATEWAY_TIMEOUT, "Transport.Timeout"))).toBe(false);
    expect(isSilentFailure(failure(SERVICE_UNAVAILABLE, "Transport.NetworkError"))).toBe(false);
    expect(isSilentFailure(failure(BAD_REQUEST, "Validation.Failed"))).toBe(false);
  });

  it("classifies a raw abort before deciding", () => {
    const error: Error = new Error("aborted");
    error.name = "AbortError";

    expect(isSilentFailure(error)).toBe(true);
    expect(isSilentFailure(new Error("x"))).toBe(false);
    expect(isSilentFailure(undefined)).toBe(false);
  });
});

describe("failureReference", () => {
  function referenced(status: number, code: string): ApiFailure {
    return new ApiFailure({
      status,
      code,
      title: "Title",
      detail: "Detail",
      traceId: "trace",
      requestId: "request",
    });
  }

  it("quotes both ids for a 5xx", () => {
    expect(failureReference(referenced(INTERNAL_SERVER_ERROR, "Server.Error"))).toEqual({
      traceId: "trace",
      requestId: "request",
    });
  });

  it("quotes a transport failure whatever its status", () => {
    expect(failureReference(referenced(SERVICE_UNAVAILABLE, "Transport.NetworkError"))).toEqual({
      traceId: "trace",
      requestId: "request",
    });
    expect(failureReference(referenced(NO_WAIT, "Transport.Timeout"))).toEqual({
      traceId: "trace",
      requestId: "request",
    });
  });

  it("keeps a 4xx's ids off the screen", () => {
    expect(failureReference(referenced(CONFLICT, "Orders.Closed"))).toBeUndefined();
    expect(failureReference(referenced(UNAUTHORIZED, "Auth.Unauthenticated"))).toBeUndefined();
  });

  it("answers undefined when a quotable failure carries no id", () => {
    expect(failureReference(failure(INTERNAL_SERVER_ERROR, "Server.Error"))).toBeUndefined();
  });

  it("classifies a plain Error as a transport failure with nothing to quote", () => {
    expect(failureReference(new Error("fetch failed"))).toBeUndefined();
  });
});

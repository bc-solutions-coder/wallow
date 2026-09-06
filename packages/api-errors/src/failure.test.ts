import { describe, expect, it } from "vitest";

import { ApiFailure, isApiFailure } from "./failure";

/**
 * The failure type itself: what it carries, how its log line reads, and the
 * brand that lets a consumer recognise one across bundles.
 */

const NOT_FOUND: number = 404;

describe("ApiFailure", () => {
  it("carries every member it was built with", () => {
    const cause: Error = new Error("upstream");
    const failure: ApiFailure = new ApiFailure({
      status: NOT_FOUND,
      code: "Http.NotFound",
      title: "Not found",
      detail: "No such organization.",
      traceId: "00-abc-def-01",
      requestId: "req-1",
      fieldErrors: { name: ["Required"] },
      retryAfter: 30,
      cause,
    });

    expect(failure).toBeInstanceOf(Error);
    expect(failure.name).toBe("ApiFailure");
    expect(failure.status).toBe(NOT_FOUND);
    expect(failure.code).toBe("Http.NotFound");
    expect(failure.title).toBe("Not found");
    expect(failure.detail).toBe("No such organization.");
    expect(failure.traceId).toBe("00-abc-def-01");
    expect(failure.requestId).toBe("req-1");
    expect(failure.fieldErrors).toEqual({ name: ["Required"] });
    expect(failure.retryAfter).toBe(30);
    expect(failure.cause).toBe(cause);
  });

  it("reads `[<status> <code>] <title>` in a log line", () => {
    const failure: ApiFailure = new ApiFailure({
      status: NOT_FOUND,
      code: "Http.NotFound",
      title: "Not found",
    });

    expect(failure.message).toBe("[404 Http.NotFound] Not found");
    expect(String(failure)).toBe("ApiFailure: [404 Http.NotFound] Not found");
  });

  it("leaves the optional members absent rather than null", () => {
    const failure: ApiFailure = new ApiFailure({
      status: NOT_FOUND,
      code: "Http.NotFound",
      title: "Not found",
    });

    expect(failure.detail).toBeUndefined();
    expect(failure.traceId).toBeUndefined();
    expect(failure.requestId).toBeUndefined();
    expect(failure.fieldErrors).toBeUndefined();
    expect(failure.retryAfter).toBeUndefined();
    expect(failure.cause).toBeUndefined();
  });
});

describe("isApiFailure", () => {
  it("recognises an instance", () => {
    expect(isApiFailure(new ApiFailure({ status: NOT_FOUND, code: "X", title: "x" }))).toBe(true);
  });

  it("recognises a failure branded by another copy of this module", () => {
    // Two bundles carry two classes; the global-registry symbol is what they share.
    const foreign: Error = Object.assign(new Error("[404 X] x"), {
      status: NOT_FOUND,
      code: "X",
      title: "x",
      [Symbol.for("wallow.api-failure")]: true,
    });

    expect(isApiFailure(foreign)).toBe(true);
  });

  it.each([
    undefined,
    null,
    "Http.NotFound",
    new Error("[404 Http.NotFound] Not found"),
    { status: NOT_FOUND, code: "Http.NotFound", title: "Not found" },
  ])("rejects %o", (value) => {
    expect(isApiFailure(value)).toBe(false);
  });
});

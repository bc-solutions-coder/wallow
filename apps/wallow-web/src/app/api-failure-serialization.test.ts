import { ApiFailure, isApiFailure } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";

import { apiFailureSerialization } from "./api-failure-serialization";

/*
 * The SSR seam for an `ApiFailure`: the adapter claims only branded failures,
 * and what comes back on the client is a branded failure again with every
 * user-facing field intact — the boundary's `isApiFailure` branch must hold
 * after hydration exactly as it did on the server.
 */

const FAILURE = new ApiFailure({
  status: 503,
  code: "Transport.NetworkError",
  title: "Service Unavailable",
  detail: "Upstream did not answer.",
  requestId: "req-42",
  retryAfter: 30,
  cause: new Error("fetch failed"),
});

describe("apiFailureSerialization", () => {
  it("claims branded failures and nothing else", () => {
    expect(apiFailureSerialization.test(FAILURE)).toBe(true);
    expect(apiFailureSerialization.test(new Error("[503 Transport.NetworkError]"))).toBe(false);
    expect(apiFailureSerialization.test({ status: 503, code: "Transport.NetworkError" })).toBe(
      false,
    );
  });

  it("round-trips a failure as a branded failure with its fields", () => {
    const serialized = apiFailureSerialization.toSerializable(FAILURE);
    const restored = apiFailureSerialization.fromSerializable(serialized);

    expect(isApiFailure(restored)).toBe(true);
    expect(restored).toMatchObject({
      status: 503,
      code: "Transport.NetworkError",
      title: "Service Unavailable",
      detail: "Upstream did not answer.",
      requestId: "req-42",
      retryAfter: 30,
    });
    expect(restored.traceId).toBeUndefined();
  });

  it("leaves the cause and every absent field out of the document", () => {
    const serialized = apiFailureSerialization.toSerializable(FAILURE);

    expect(serialized).not.toHaveProperty("cause");
    expect(serialized).not.toHaveProperty("traceId");
    expect(serialized).not.toHaveProperty("fieldErrors");
  });
});

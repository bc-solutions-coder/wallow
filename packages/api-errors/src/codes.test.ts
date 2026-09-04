import { describe, expect, it } from "vitest";

import { ClientErrorCode } from "./codes";
import { ErrorCode } from "./generated";

/**
 * The two halves of `FailureCode`: the codes the client mints itself and the
 * catalogue the API publishes, which must not overlap.
 */

describe("ClientErrorCode", () => {
  it("names exactly the seven client-side codes", () => {
    expect(Object.values(ClientErrorCode).toSorted()).toEqual([
      "Bff.CsrfInvalid",
      "Bff.SessionMissing",
      "Bff.SessionRefreshFailed",
      "Client.UnrecognizedResponse",
      "Transport.Aborted",
      "Transport.NetworkError",
      "Transport.Timeout",
    ]);
  });

  it("shares no value with the API catalogue", () => {
    const published: ReadonlySet<string> = new Set(Object.values(ErrorCode));

    for (const code of Object.values(ClientErrorCode)) {
      expect(published.has(code)).toBe(false);
    }
  });
});

describe("ErrorCode", () => {
  it("is a runtime object keyed the way the SDK's generated enums are", () => {
    expect(ErrorCode.VALIDATION_FAILED).toBe("Validation.Failed");
    expect(ErrorCode.AUTH_UNAUTHENTICATED).toBe("Auth.Unauthenticated");
  });
});

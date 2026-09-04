import { describe, expect, it } from "vitest";

import * as api from "./index";

/**
 * The single entry's runtime surface. Types are pinned by the consumers that
 * compile against them; this lists what a bundle actually exports.
 */

describe("@bc-solutions-coder/api-errors", () => {
  it("exports the documented runtime surface and nothing more", () => {
    expect(Object.keys(api).toSorted()).toEqual([
      "ApiFailure",
      "ClientErrorCode",
      "ErrorCode",
      "defineFailureMessages",
      "failureFromResponse",
      "isApiFailure",
      "isSilentFailure",
      "parseRetryAfter",
      "resolveFailureMessage",
      "splitFieldErrors",
      "toApiFailure",
    ]);
  });
});

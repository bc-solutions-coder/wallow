import { REQUEST_ID_HEADER as LOGGER_REQUEST_ID_HEADER } from "@bc-solutions-coder/logger/server";
import { REQUEST_ID_HEADER } from "@bc-solutions-coder/sdk/server";
import { describe, expect, it } from "vitest";

/**
 * The one header name `@bc-solutions-coder/logger` mirrors from the SDK.
 *
 * The logger declares zero dependencies — importing the SDK for one string would
 * drag an OIDC client into every consumer of a logging package — so the string is
 * duplicated by design. This app depends on both packages, which makes it the one
 * place the duplication can be pinned.
 *
 * Drift is invisible at runtime rather than loud: a correlation header the logger
 * reads under a name the proxy no longer writes yields records with no
 * correlation id, which looks like a working system.
 */

describe("the logger mirrors the SDK's correlation header", () => {
  it("reads the correlation id from the header the proxy writes", () => {
    expect(LOGGER_REQUEST_ID_HEADER).toBe(REQUEST_ID_HEADER);
  });
});

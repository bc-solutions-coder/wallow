import {
  DEFAULT_CLIENT_IP_HEADER,
  REQUEST_ID_HEADER as LOGGER_REQUEST_ID_HEADER,
} from "@bc-solutions-coder/logger/server";
import { CLIENT_IP_HEADER, REQUEST_ID_HEADER } from "@bc-solutions-coder/sdk/server";
import { describe, expect, it } from "vitest";

/**
 * The two header names `@bc-solutions-coder/logger` mirrors from the SDK.
 *
 * The logger declares zero dependencies — importing the SDK for one string would
 * drag an OIDC client into every consumer of a logging package — so the strings
 * are duplicated by design. This app depends on both packages, which makes it the
 * one place the duplication can be pinned.
 *
 * Drift is invisible at runtime rather than loud: a correlation header the
 * logger reads under a name the proxy no longer writes yields records with no
 * correlation id, and a client-IP header under the wrong name rate-limits every
 * user of this app as one client. Both look like a working system.
 */

describe("the logger mirrors the SDK's correlation headers", () => {
  it("reads the correlation id from the header the proxy writes", () => {
    expect(LOGGER_REQUEST_ID_HEADER).toBe(REQUEST_ID_HEADER);
  });

  it("reads the client address from the header the host stamps", () => {
    expect(DEFAULT_CLIENT_IP_HEADER).toBe(CLIENT_IP_HEADER);
  });
});

import { AUTH_URL_GLOBAL_KEY, DEFAULT_AUTH_URL } from "@bc-solutions-coder/env/auth-origin";
import { afterEach, describe, expect, it } from "vitest";

import { authUrl } from "./auth-url";

/**
 * The accessor's precedence: what the server published into the document, and
 * the local dev default for a caller with neither a document nor a request —
 * which is what lets a screen mount in a spec with no provider and no router.
 */
describe("authUrl", () => {
  afterEach(() => {
    delete (globalThis as Record<string, unknown>)[AUTH_URL_GLOBAL_KEY];
  });

  it("answers with the origin the document published", () => {
    (globalThis as Record<string, unknown>)[AUTH_URL_GLOBAL_KEY] = "https://wallow.dev/auth";

    expect(authUrl()).toBe("https://wallow.dev/auth");
  });

  it("falls back to the local dev default when nothing published one", () => {
    expect(authUrl()).toBe(DEFAULT_AUTH_URL);
  });

  it("ignores a global that is not a non-blank string", () => {
    (globalThis as Record<string, unknown>)[AUTH_URL_GLOBAL_KEY] = 7;

    expect(authUrl()).toBe(DEFAULT_AUTH_URL);
  });
});

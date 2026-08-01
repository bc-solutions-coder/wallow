import { describe, expect, it } from "vitest";

import { toSlug } from "./string";

/**
 * `toSlug`'s output shape: lowercase, single hyphens between alphanumeric runs,
 * no leading or trailing separator.
 *
 * The empty-string cases are the contract, not an oversight — the function
 * transliterates nothing, so a caller wanting a guaranteed-nonempty identifier
 * has to supply a fallback.
 */

describe("toSlug", () => {
  it("joins the words of a display name", () => {
    expect(toSlug("Microsoft Entra ID")).toBe("microsoft-entra-id");
  });

  it("collapses a run of separators into one hyphen", () => {
    expect(toSlug("Sign  in / out")).toBe("sign-in-out");
  });

  it("trims the separators off both ends", () => {
    expect(toSlug("  (Google)  ")).toBe("google");
  });

  it("leaves an already-slugged value alone", () => {
    expect(toSlug("github")).toBe("github");
  });

  it("keeps digits", () => {
    expect(toSlug("Auth0 v2")).toBe("auth0-v2");
  });

  it("reduces a purely non-ASCII name to nothing", () => {
    expect(toSlug("日本語")).toBe("");
  });

  it("reduces a purely punctuational name to nothing", () => {
    expect(toSlug("--- ---")).toBe("");
  });
});

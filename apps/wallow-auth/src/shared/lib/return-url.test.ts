import { describe, expect, it } from "vitest";

import { decideReturnUrl, isRedirectUriAllowed } from "./return-url";

/**
 * The app's one returnUrl decision function, mode by mode. The rows that differ
 * between modes — a bare `?returnUrl=` and an absolute URL — are the whole
 * reason the modes exist, so each gets an assertion per mode.
 *
 * Node project: pure narrowing, no DOM.
 */

describe("decideReturnUrl — every mode agrees on the settled rows", () => {
  it("reads an absent returnUrl as absent — a direct visit is not an attack", () => {
    expect(decideReturnUrl(undefined, "refuse-empty").verdict).toBe("absent");
    expect(decideReturnUrl(undefined, "empty-ok").verdict).toBe("absent");
    expect(decideReturnUrl(undefined, "server-allowlist").verdict).toBe("absent");
  });

  it("accepts a relative single-slash path and carries the value", () => {
    expect(decideReturnUrl("/dashboard?tab=keys", "refuse-empty")).toEqual({
      verdict: "accept",
      returnUrl: "/dashboard?tab=keys",
    });
    expect(decideReturnUrl("/dashboard?tab=keys", "empty-ok")).toEqual({
      verdict: "accept",
      returnUrl: "/dashboard?tab=keys",
    });
    expect(decideReturnUrl("/dashboard?tab=keys", "server-allowlist")).toEqual({
      verdict: "accept",
      returnUrl: "/dashboard?tab=keys",
    });
  });

  it("refuses a protocol-relative path in every mode", () => {
    // `//evil.example` resolves cross-origin despite its leading slash; no mode
    // may ever defer it to anything weaker than refusal except the server ask.
    expect(decideReturnUrl("//evil.example", "refuse-empty").verdict).toBe("refuse");
    expect(decideReturnUrl("//evil.example", "empty-ok").verdict).toBe("refuse");
  });

  it("refuses a backslash disguise the same way the server's validator does", () => {
    expect(decideReturnUrl(String.raw`/\evil.example`, "refuse-empty").verdict).toBe("refuse");
    expect(decideReturnUrl(String.raw`/\evil.example`, "empty-ok").verdict).toBe("refuse");
  });
});

describe("decideReturnUrl — the empty row is where the modes split", () => {
  it("refuse-empty reads a bare ?returnUrl= as a present, unsafe value", () => {
    expect(decideReturnUrl("", "refuse-empty").verdict).toBe("refuse");
  });

  it("empty-ok reads it as no destination, keeping it off the error page", () => {
    expect(decideReturnUrl("", "empty-ok").verdict).toBe("absent");
  });

  it("server-allowlist refuses it without asking — not a destination worth a probe", () => {
    expect(decideReturnUrl("", "server-allowlist").verdict).toBe("refuse");
  });
});

describe("decideReturnUrl — the absolute row is server-allowlist's whole point", () => {
  it("defers an absolute URL to the server allow-list as ask, carrying the value", () => {
    // The external-login hand-off arrives absolute and allow-listed; string
    // inspection cannot tell it from an attack, so neither accept nor refuse is
    // safe to decide locally.
    expect(decideReturnUrl("https://app.example/return", "server-allowlist")).toEqual({
      verdict: "ask",
      returnUrl: "https://app.example/return",
    });
  });

  it("refuses the same absolute URL in the local-only modes", () => {
    expect(decideReturnUrl("https://app.example/return", "refuse-empty").verdict).toBe("refuse");
    expect(decideReturnUrl("https://app.example/return", "empty-ok").verdict).toBe("refuse");
  });

  it("refuses a whitespace-only value rather than treating it as empty", () => {
    // `" "` fails the emptiness comparison AND the safety rule, so it must land
    // on refuse (or ask, where the server is the judge) — never on absent.
    expect(decideReturnUrl(" ", "empty-ok").verdict).toBe("refuse");
    expect(decideReturnUrl(" ", "server-allowlist").verdict).toBe("ask");
  });
});

describe("isRedirectUriAllowed", () => {
  it("allows only a literal allowed: true", () => {
    expect(isRedirectUriAllowed({ allowed: true })).toBe(true);
  });

  it("refuses truthy look-alikes", () => {
    // JS truthiness would admit the string "false"; the strict comparison is
    // the C# client's `body?.Allowed == true`, reproduced.
    expect(isRedirectUriAllowed({ allowed: "true" })).toBe(false);
    expect(isRedirectUriAllowed({ allowed: "false" })).toBe(false);
    expect(isRedirectUriAllowed({ allowed: 1 })).toBe(false);
  });

  it("refuses a body with no allowed key, a non-object, and null", () => {
    expect(isRedirectUriAllowed({})).toBe(false);
    expect(isRedirectUriAllowed("allowed")).toBe(false);
    expect(isRedirectUriAllowed(null)).toBe(false);
    expect(isRedirectUriAllowed(undefined)).toBe(false);
  });
});

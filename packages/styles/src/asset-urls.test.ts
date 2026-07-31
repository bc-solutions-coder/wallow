import { describe, expect, it } from "vitest";

import { toRootRelativeAssetUrl } from "./asset-urls";
import { appIconUrl, forkBranding } from "./branding";

/**
 * The bug these tests exist for: `packages/styles/branding.json` names the app icon by bare
 * filename, and rendering that value directly makes the browser resolve it
 * against the current page. On `/login` that happens to work; on
 * `/mfa/challenge` the browser asks for `/mfa/piggy-icon.svg` and the icon
 * disappears. The Blazor app never had the problem because Blazor normalised
 * asset paths against the app base — React has no equivalent, so the URL has to
 * be root-relative before it reaches the markup.
 *
 * Route depths used below are real wallow-auth routes.
 */
const routes: readonly string[] = [
  "http://localhost:3002/",
  "http://localhost:3002/login",
  "http://localhost:3002/mfa/challenge",
  "http://localhost:3002/verify-email/confirm",
];

describe("appIconUrl", () => {
  it("serves the fork's icon from the site root", () => {
    expect(appIconUrl).toBe("/piggy-icon.svg");
  });

  it("names the icon packages/styles/branding.json names, so a fork still swaps it there", () => {
    // The rebrand contract: the filename is the JSON's to choose. Only the
    // leading slash is ours.
    expect(appIconUrl).toBe(`/${forkBranding.appIcon}`);
  });

  it("resolves to the same file from every route depth", () => {
    const resolved: string[] = routes.map(
      (route: string): string => new URL(appIconUrl, route).pathname,
    );

    expect(new Set(resolved)).toEqual(new Set(["/piggy-icon.svg"]));
  });

  it("does not resolve under the route directory the way the raw JSON value does", () => {
    // The regression guard, stated as the bug: rendering forkBranding.appIcon
    // instead of appIconUrl is what produced /mfa/piggy-icon.svg.
    expect(new URL(forkBranding.appIcon, "http://localhost:3002/mfa/challenge").pathname).toBe(
      "/mfa/piggy-icon.svg",
    );
    expect(new URL(appIconUrl, "http://localhost:3002/mfa/challenge").pathname).toBe(
      "/piggy-icon.svg",
    );
  });
});

describe("toRootRelativeAssetUrl", () => {
  it("roots a bare filename", () => {
    expect(toRootRelativeAssetUrl("piggy-icon.svg")).toBe("/piggy-icon.svg");
  });

  it("roots a path written relative to the current directory", () => {
    expect(toRootRelativeAssetUrl("./piggy-icon.svg")).toBe("/piggy-icon.svg");
  });

  it("leaves an already-rooted path alone", () => {
    // Idempotent, so a caller that has already been through here — or a fork
    // that writes the leading slash in its JSON — does not end up at
    // //piggy-icon.svg.
    expect(toRootRelativeAssetUrl("/piggy-icon.svg")).toBe("/piggy-icon.svg");
  });

  it("keeps a nested asset path nested", () => {
    expect(toRootRelativeAssetUrl("brand/logo.svg")).toBe("/brand/logo.svg");
  });

  it("leaves an absolute URL untouched", () => {
    // A client's branding logoUrl is hosted elsewhere; rooting it would break it.
    expect(toRootRelativeAssetUrl("https://cdn.test/acme.svg")).toBe("https://cdn.test/acme.svg");
  });
});

/**
 * The second half of the same bug (Wallow-8via). "The site root" is only the
 * URL root when the app owns the origin. Served under a prefix — which
 * wallow-auth's `AUTH_BASE_PATH` knob exists to do — the brand assets are
 * copied under that prefix too (Vite's `base` plus nitro's `baseURL`), so a
 * root-relative `/piggy-icon.svg` points at the SITE root, which behind the
 * path-based ingress is a DIFFERENT app. The icon 404s.
 *
 * The base path is a build-time value the CONSUMING app bakes in, so it arrives
 * as an argument: this package ships a prebuilt bundle and would otherwise
 * freeze its own `import.meta.env.BASE_URL` (always "/") into every consumer.
 * It is normalized here rather than by each caller so an app can hand over
 * Vite's raw `import.meta.env.BASE_URL` ("/", "/auth/") or an already-normalized
 * prefix ("/auth") and get the same answer.
 */
describe("toRootRelativeAssetUrl under a base path", () => {
  it("serves a bare filename from under the prefix, not from the site root", () => {
    expect(toRootRelativeAssetUrl("piggy-icon.svg", "/auth")).toBe("/auth/piggy-icon.svg");
  });

  it("prefixes a path written relative to the current directory", () => {
    expect(toRootRelativeAssetUrl("./piggy-icon.svg", "/auth")).toBe("/auth/piggy-icon.svg");
  });

  it("prefixes a path a fork wrote with its own leading slash", () => {
    expect(toRootRelativeAssetUrl("/piggy-icon.svg", "/auth")).toBe("/auth/piggy-icon.svg");
  });

  it("keeps a nested asset path nested under the prefix", () => {
    expect(toRootRelativeAssetUrl("brand/logo.svg", "/auth")).toBe("/auth/brand/logo.svg");
  });

  it("handles a multi-segment prefix", () => {
    expect(toRootRelativeAssetUrl("piggy-icon.svg", "/apps/auth")).toBe(
      "/apps/auth/piggy-icon.svg",
    );
  });

  it("is idempotent, so a value already under the prefix is not prefixed twice", () => {
    expect(toRootRelativeAssetUrl("/auth/piggy-icon.svg", "/auth")).toBe("/auth/piggy-icon.svg");
  });

  it("only treats the prefix as present on a segment boundary", () => {
    // `/authentic` is not below `/auth` — the same rule the passthrough's
    // stripBasePath follows, for the same reason.
    expect(toRootRelativeAssetUrl("/authentic/logo.svg", "/auth")).toBe("/auth/authentic/logo.svg");
  });

  it("leaves an absolute URL untouched under a prefix", () => {
    expect(toRootRelativeAssetUrl("https://cdn.test/acme.svg", "/auth")).toBe(
      "https://cdn.test/acme.svg",
    );
  });

  it("leaves a protocol-relative URL untouched under a prefix", () => {
    expect(toRootRelativeAssetUrl("//cdn.test/acme.svg", "/auth")).toBe("//cdn.test/acme.svg");
  });

  it.each([
    ["Vite's own unprefixed BASE_URL", "/"],
    ["the empty string", ""],
    ["whitespace from a hand-edited env file", "   "],
  ])("treats %s as no prefix, leaving the unbased result unchanged", (_label, basePath) => {
    expect(toRootRelativeAssetUrl("piggy-icon.svg", basePath)).toBe("/piggy-icon.svg");
  });

  it.each([
    ["Vite's own BASE_URL, with its trailing slash", "/auth/"],
    ["the canonical prefix", "/auth"],
    ["a prefix written without its leading slash", "auth"],
    ["surrounding whitespace from a hand-edited env file", "  /auth  "],
  ])("accepts %s and produces one canonical URL", (_label, basePath) => {
    expect(toRootRelativeAssetUrl("piggy-icon.svg", basePath)).toBe("/auth/piggy-icon.svg");
  });

  it("resolves to the same file from every route depth below the prefix", () => {
    const based: string = toRootRelativeAssetUrl("piggy-icon.svg", "/auth");
    const resolved: string[] = [
      "http://wallow.dev/auth/",
      "http://wallow.dev/auth/login",
      "http://wallow.dev/auth/mfa/challenge",
    ].map((route: string): string => new URL(based, route).pathname);

    expect(new Set(resolved)).toEqual(new Set(["/auth/piggy-icon.svg"]));
  });
});

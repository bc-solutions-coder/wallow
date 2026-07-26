import { describe, expect, it } from "vitest";

import { toRootRelativeAssetUrl } from "./asset-urls";
import { appIconUrl, forkBranding } from "./branding";

/**
 * The bug these tests exist for: `api/branding.json` names the app icon by bare
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

  it("names the icon api/branding.json names, so a fork still swaps it there", () => {
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

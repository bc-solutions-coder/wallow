import { describe, expect, it } from "vitest";

import { appIconLinks } from "./icon-links";

describe("appIconLinks", () => {
  it("offers PNG and Apple touch icons alongside the original logo", () => {
    expect(appIconLinks()).toEqual([
      { rel: "icon", type: "image/png", sizes: "32x32", href: "/favicon.png" },
      { rel: "icon", href: "/piggy-icon.svg" },
      { rel: "apple-touch-icon", sizes: "180x180", href: "/apple-touch-icon.png" },
    ]);
  });

  it("keeps every icon under the auth deployment prefix", () => {
    expect(appIconLinks({ basePath: "/auth/" }).map((link) => link.href)).toEqual([
      "/auth/favicon.png",
      "/auth/piggy-icon.svg",
      "/auth/apple-touch-icon.png",
    ]);
  });

  it("does not attach Wallow fallbacks to a fork that only supplies its own logo", () => {
    expect(appIconLinks({ branding: { appIcon: "custom.svg" } })).toEqual([
      { rel: "icon", href: "/custom.svg" },
    ]);
  });
});

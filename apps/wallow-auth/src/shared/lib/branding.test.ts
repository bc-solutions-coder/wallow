import { forkBranding, toAppIconUrl } from "@bc-solutions-coder/styles";
import { describe, expect, it } from "vitest";

import { BASE_PATH } from "./base-path";
import { appIconUrl, forkResolvedBranding } from "./branding";

/**
 * This app's branding constants, bound to its base path. Node project: pure
 * string work, no DOM.
 *
 * `@bc-solutions-coder/styles` ships a PREBUILT bundle, so it cannot read this
 * app's `import.meta.env.BASE_URL` — its own is frozen at "/", and a screen that
 * picks up the package's unbased `appIconUrl` 404s the icon under a base path.
 *
 * That last hazard used to be pinned by source greps — this module passing
 * `BASE_PATH` into both helpers, and each screen importing branding from HERE
 * rather than from the package. They are gone with the rest of the source tests
 * (Wallow-xg9t.1). The value is baked at BUILD time, so a default run cannot tell
 * a prefixed URL from an unprefixed one either way; what can is a based build
 * driven end to end, which is `e2e/`'s job.
 */

describe("appIconUrl", () => {
  it("serves the fork's icon from under this build's base path", () => {
    expect(appIconUrl).toBe(toAppIconUrl(BASE_PATH));
  });

  it("is unchanged under the default build, where there is no prefix", () => {
    expect(appIconUrl).toBe(`/${forkBranding.appIcon}`);
  });
});

describe("forkResolvedBranding", () => {
  it("resolves the fork's own branding with its icon under this build's base path", () => {
    expect(forkResolvedBranding.logoUrl).toBe(toAppIconUrl(BASE_PATH));
  });

  it("still carries the fork identity the layout renders", () => {
    expect(forkResolvedBranding.name).toBe(forkBranding.appName);
    expect(forkResolvedBranding.tagline).toBe(forkBranding.tagline);
  });
});

# @bc-solutions-coder/styles

Private workspace branding and Tailwind configuration. Add
`"@bc-solutions-coder/styles": "workspace:*"` to a consuming workspace package.
It is not registry-installable. Fork display settings and palettes live in
[branding.json](branding.json); shared icon files live in [assets](assets).

Add the styling plugins to the app's existing Vite plugin list:

```ts
import { wallowStyles } from "@bc-solutions-coder/styles/vite";
import { defineConfig } from "vite";

export default defineConfig({ plugins: [...wallowStyles()] });
```

`wallowStyles()` installs Tailwind, serves the virtual theme stylesheet, and sets
`publicDir` to the shared asset directory. An app with additional public files must
account for that directory choice. The Node-only `./assets` entry exports
`brandAssetsDir` when a build needs the directory directly.

In the app stylesheet, import the shared entry and declare source paths relative to
that stylesheet:

```css
@import "@bc-solutions-coder/styles/styles.css";
@source "./";
```

The stylesheet maps Tailwind tokens to CSS variables. Supply their values either by
rendering theme CSS in the document head or by importing `virtual:wallow-theme.css`
with the Vite plugins installed. The virtual import uses the fork palette and is also
useful in browser test setup.

```ts
import {
  mergeClientBranding,
  forkBranding,
  renderThemeStyle,
  resolveForkBranding,
  toAppIconUrl,
} from "@bc-solutions-coder/styles";

const fork = resolveForkBranding("/auth");
const themeCss = renderThemeStyle(fork);
const icon = toAppIconUrl("/auth"); // "/auth/piggy-icon.svg" with this fork's icon
const resolved = mergeClientBranding(forkBranding, null, "/auth");
```

Pass the consuming app base path explicitly. The `appIconUrl` and
`forkResolvedBranding` constants assume the origin root. Client branding replaces the
name, tagline, and logo, while mode-specific theme values overlay the fork palette.
An empty client tagline or logo stays absent rather than using the fork identity.
Invalid theme JSON contributes no overrides. CSS names and values are not sanitized;
use trusted theme data when inserting rendered CSS into a style element.

`resolveForkLinks(env)` reads nonblank `WALLOW_REPOSITORY_URL` and `WALLOW_DOCS_URL`
overrides, then uses the package's fork-link constants. Those constants fall back to
upstream links when JSON fields are absent; explicitly empty JSON fields remain empty.
`forkLinksScript(links)` returns escaped inline script source for SSR, and
`readInjectedForkLinks(globalThis)` reads the pair in the browser after it runs.

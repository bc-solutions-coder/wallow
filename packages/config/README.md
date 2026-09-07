# @bc-solutions-coder/config

Private workspace Vite presets. Add `"@bc-solutions-coder/config": "workspace:*"` to
a workspace package's development dependencies. This package is neither built nor
published: Vite loads its TypeScript source while evaluating configuration.
There is no root barrel or runtime entry.

For an ESM library, use `./vite/library`:

```ts
import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    "server/index": "src/server/index.ts",
  },
});
```

Entry paths resolve relative to the calling configuration URL. The preset writes
unminified ES2023 ESM with source maps to `dist`, clearing that directory first. Entry
and asset filenames stay stable; internal chunks have hashes. Bare imports remain
external. Set `preserveModules: true` to retain source-module output paths. Run the
package's declaration build separately, as the workspace library build scripts do.

For a TanStack Start app, merge `./vite/app` with app-specific configuration:

```ts
import { tanstackStart } from "@tanstack/react-start/plugin/vite";
import react from "@vitejs/plugin-react";
import { nitro } from "nitro/vite";
import { defineConfig } from "vite";
import { wallowAppConfig } from "@bc-solutions-coder/config/vite/app";

export default defineConfig({
  ...wallowAppConfig({ defaultPort: 3000 }),
  plugins: [tanstackStart(), react(), nitro()],
});
```

The app preset reads `PORT` when called, configures tsconfig path resolution, keeps
React external-store hooks and React Query providers in consistent SSR module graphs,
and enables public-directory copying. The app still owns its plugin list and deployment
base path. The vendored selector adapter is used by aliases, not exported as a consumer
subpath.

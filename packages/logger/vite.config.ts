import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// One lib entry per `exports` subpath. `wallow/module-lists-in-sync` diffs
// these keys against the manifest and `tsconfig.build.json`, so an `exports`
// subpath with no entry here — a path the build would never emit — fails lint.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    server: "src/server.ts",
  },
});

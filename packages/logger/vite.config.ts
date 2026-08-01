import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// One lib entry per `exports` subpath. `charter.test.ts` diffs these keys
// against the manifest, so a new entry that is not declared here would publish
// an `exports` path pointing at a file the build never emitted.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    server: "src/server.ts",
  },
});

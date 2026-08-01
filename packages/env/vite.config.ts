import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// One lib entry per `exports` subpath — there is no `.` barrel, so a subpath
// missing from this map would emit no file for consumers to resolve.
// `charter.test.ts` diffs these keys against the manifest.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    "base-path": "src/base-path.ts",
    "internal-origin": "src/internal-origin.ts",
    "request-origin": "src/request-origin.ts",
  },
});

import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// One lib entry per `exports` subpath — there is no `.` barrel, so a subpath
// missing from this map would emit no file for consumers to resolve.
// `wallow/module-lists-in-sync` diffs this map against `exports`,
// `publishConfig.exports` and `tsconfig.build.json` at lint time.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    "auth-origin": "src/auth-origin.ts",
    "base-path": "src/base-path.ts",
    "internal-origin": "src/internal-origin.ts",
    "published-global": "src/published-global.ts",
  },
});

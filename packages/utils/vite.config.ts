import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// One lib entry per `exports` subpath — there is no `.` barrel, so a subpath
// missing from this map would emit no file for consumers to resolve.
// `wallow/module-lists-in-sync` diffs these keys against the manifest and
// `tsconfig.build.json` at lint time.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    format: "src/format.ts",
    guards: "src/guards.ts",
    string: "src/string.ts",
  },
});

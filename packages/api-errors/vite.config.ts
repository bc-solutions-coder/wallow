import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// One entry, the `.` barrel. `wallow/module-lists-in-sync` diffs this map
// against the manifest and `tsconfig.build.json` at lint time.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
  },
});

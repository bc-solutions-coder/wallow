import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// Unlike packages/ui this package publishes ONE entry — the curated
// `src/index.ts` barrel — so there is no subpath export to back and no extra
// entry to enumerate. `preserveModules` is still on so `dist/` mirrors `src/`
// and a consuming bundler can drop the catalog fields the app never imports.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: { index: "src/index.ts" },
  preserveModules: true,
});

import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// One barrel, not a per-component catalog like `packages/ui`: this is a single
// cohesive frame, and the pieces below `AppShell` are not separately composable.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: { index: "src/index.ts" },
});

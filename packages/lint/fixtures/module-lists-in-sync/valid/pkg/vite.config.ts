// In sync across all four lists, exercising every key normalization the rule
// performs: `index` ↔ ".", `server/index` ↔ "./server", plain `extra` ↔
// "./extra". The manifest's "./styles.css" points at a stylesheet, not a
// module, so it needs no entry and must draw nothing.
import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    "server/index": "src/server/index.ts",
    extra: "src/extra.ts",
  },
});

// NOT a vite.config.ts, and that is the point: the rule self-gates on the
// filename, so a helper or spec that CALLS defineLibraryConfig is never judged
// against whatever package.json happens to sit beside it — the sibling here
// disagrees on purpose.
import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
  },
});

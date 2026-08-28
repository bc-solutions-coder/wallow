// A `./*` wildcard makes the `exports` list non-enumerable, and an `include`
// naming a directory makes that list non-enumerable too — both are SKIPPED, not
// compared. The siblings here deliberately disagree with the entry (no "." in
// `exports`, no "src/index.ts" in `include`), which is what proves the skip: a
// rule that compared anyway would report unannotated diagnostics.
import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
  },
});

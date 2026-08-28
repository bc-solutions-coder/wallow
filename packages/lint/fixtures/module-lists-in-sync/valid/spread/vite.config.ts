// A spread makes the entries map non-enumerable as AST, so the whole file is
// out of the rule's reach — the sibling manifest disagrees on purpose to prove
// nothing is compared. (packages/ui is this shape: `...componentEntries()`.)
import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

import { componentEntries } from "./component-entries.ts";

export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    ...componentEntries(),
  },
});

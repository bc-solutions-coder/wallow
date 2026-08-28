// Out of sync in all four directions the rule reports: an entry missing from
// `exports` (gamma), an entry missing from `publishConfig.exports` (beta), an
// `exports` subpath with no entry (./omega), and a `tsconfig.build.json`
// include with no entry (src/delta.ts). The two list-side misses land on the
// `entries` property, because the missing thing has no node of its own.
import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

export default defineLibraryConfig({
  configUrl: import.meta.url,
  // expect-error: wallow/module-lists-in-sync ./omega is exported but has no entry
  // expect-error: wallow/module-lists-in-sync src/delta.ts is included but has no entry
  entries: {
    alpha: "src/alpha.ts",
    // expect-error: wallow/module-lists-in-sync beta is missing from publishConfig.exports
    beta: "src/beta.ts",
    // expect-error: wallow/module-lists-in-sync gamma is missing from exports
    gamma: "src/gamma.ts",
  },
});

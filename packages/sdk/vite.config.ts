import { defineLibraryConfig } from "../../tools/vite/library";

// Declarations do NOT come from the bundler here — see tsconfig.build.json for
// the full rationale, including why this package's own typescript devDependency
// is pinned to TS6. In short: bundler-driven declaration emit goes through the
// TypeScript compiler API programmatically, which is unstable on the TypeScript
// 7.0 GA native compiler (the stable programmatic API lands in 7.1), while the
// native `tsc --emitDeclarationOnly` CLI emits .d.ts correctly.
//
// Named entries keep the `.`, `./server`, `./query` and `./server/passthrough`
// subpaths pointing at stable, unhashed filenames. `./server/passthrough` is its
// own entry rather than a re-export so a passthrough-only app never pulls
// `openid-client` and the BFF handler graph into its server bundle.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    "server/index": "src/server/index.ts",
    "server/passthrough": "src/server/passthrough.ts",
    "query/index": "src/query/index.ts",
  },
});

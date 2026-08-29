import { defineLibraryConfig } from "@bc-solutions-coder/config/vite/library";

// Declarations do NOT come from the bundler here — see tsconfig.build.json for
// the full rationale, including why this package's own typescript devDependency
// is pinned to TS6. In short: bundler-driven declaration emit goes through the
// TypeScript compiler API programmatically, which is unstable on the TypeScript
// 7.0 GA native compiler (the stable programmatic API lands in 7.1), while the
// native `tsc --emitDeclarationOnly` CLI emits .d.ts correctly.
//
// Named entries keep the `.`, `./server`, `./query`, `./server/passthrough`,
// `./server/forwarded` and `./server/service` subpaths pointing at stable,
// unhashed filenames. `./server/passthrough` and `./server/service` are their
// own entries rather than re-exports so a passthrough-only app never pulls
// `openid-client` and the BFF handler graph into its server bundle, and a
// service-account consumer never pulls the handler graph either.
// `./server/forwarded` is the dependency-free trusted-proxy module, its own
// entry so an isomorphic Start entry can import the origin resolver without
// the server graph behind it.
export default defineLibraryConfig({
  configUrl: import.meta.url,
  entries: {
    index: "src/index.ts",
    "server/index": "src/server/index.ts",
    "server/passthrough": "src/server/passthrough.ts",
    "server/forwarded": "src/server/forwarded.ts",
    "server/service": "src/server/service.ts",
    "query/index": "src/query/index.ts",
  },
});

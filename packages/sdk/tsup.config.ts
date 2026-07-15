import { defineConfig } from "tsup";

// tsup/esbuild bundles the JS only. Declaration files are emitted separately by
// `tsc -p tsconfig.build.json` (see the package `build` script). We do NOT use
// tsup's `dts: true` because that path drives the TypeScript compiler API
// programmatically (via rollup-plugin-dts), which is unstable on the
// TypeScript 7.0 GA native compiler (the stable programmatic API does not land
// until 7.1) — and the rest of the workspace targets TS7. The native
// `tsc --emitDeclarationOnly` CLI emits .d.ts correctly, so that is the
// declaration path instead. See tsconfig.build.json for the full rationale,
// including why this package's own typescript devDependency is pinned to TS6.
export default defineConfig({
  entry: {
    index: "src/index.ts",
    "server/index": "src/server/index.ts",
  },
  format: ["esm"],
  clean: true,
  sourcemap: true,
});

import { defineConfig } from "@hey-api/openapi-ts";

/**
 * Emits `ErrorCode` — and nothing else — from the SDK's committed OpenAPI
 * snapshot. The snapshot is owned by `packages/sdk` (its drift and autoregen
 * workflows keep it in step with the API); this package only reads it, so the
 * two generated outputs can never disagree about the catalogue.
 *
 * Every operation is excluded, and any `parser.filters` value prunes the
 * resources no kept operation references (`orphans` defaults to `false`), so
 * the schema include list is the whole output.
 */
export default defineConfig({
  input: "../sdk/openapi/v1.json",
  output: {
    path: "./src/generated",
    // No post-processor binary is installed in this workspace; an empty list
    // keeps the generator from spawning one. Never set the deprecated `format`.
    postProcess: [],
  },
  parser: {
    filters: {
      operations: { exclude: ["/.*/"] },
      schemas: { include: ["ErrorCode"] },
    },
  },
  plugins: [
    {
      name: "@hey-api/typescript",
      // A runtime object as well as a union: consumers can write
      // `ErrorCode.AUTH_UNAUTHENTICATED` and the value survives into the bundle.
      enums: "javascript",
    },
  ],
});

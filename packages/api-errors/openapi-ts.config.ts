import { defineConfig } from "@hey-api/openapi-ts";

/**
 * Generate the ErrorCode schema from the SDK OpenAPI snapshot, with operations excluded.
 * The TypeScript plugin can also emit document-level helper types such as ClientOptions.
 * Only the intended API types are re-exported by the package entry.
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

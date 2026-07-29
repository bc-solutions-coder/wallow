import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: "./openapi/v1.json",
  output: {
    path: "./src/generated",
    // This workspace formats with oxfmt and installs no post-processor binary;
    // an empty list keeps the generator from spawning one. Never set the
    // deprecated `format` here — it back-fills `postProcess` from the legacy path.
    postProcess: [],
  },
  plugins: [
    {
      name: "@hey-api/client-fetch",
      runtimeConfigPath: "./src/runtime-config",
      // Every operation rejects on a non-2xx so the WallowError interceptor is
      // the single error path; without this the generated query functions
      // resolve with an error payload and TanStack Query calls it a success.
      throwOnError: true,
    },
    "@hey-api/typescript",
    { name: "@hey-api/sdk", responseStyle: "data" },
    {
      name: "@tanstack/react-query",
      queryOptions: true,
      // `tags` is what `src/query/invalidations.ts` sweeps by — hey-api emits no
      // hierarchical key prefix, so without them a subtree invalidation has
      // nothing to match on.
      queryKeys: { tags: true },
      mutationOptions: true,
    },
  ],
});

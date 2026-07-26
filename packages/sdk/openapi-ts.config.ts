import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: "./openapi/v1.json",
  output: {
    path: "./src/generated",
  },
  plugins: [
    {
      name: "@hey-api/client-fetch",
      runtimeConfigPath: "./src/runtime-config",
    },
    "@hey-api/typescript",
    "@hey-api/sdk",
  ],
});

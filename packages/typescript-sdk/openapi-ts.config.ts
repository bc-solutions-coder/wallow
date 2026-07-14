import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: "./openapi/v1.json",
  output: {
    path: "./src/generated",
    format: "prettier",
  },
  plugins: [
    {
      name: "@hey-api/client-fetch",
      runtimeConfigPath: "./src/runtime-config.ts",
    },
    "@hey-api/typescript",
    "@hey-api/sdk",
    "@tanstack/react-query",
  ],
});

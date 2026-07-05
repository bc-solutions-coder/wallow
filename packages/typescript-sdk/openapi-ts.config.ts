import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: "./openapi/v1.json",
  output: {
    path: "./src/generated",
    format: "prettier",
  },
  plugins: [
    "@hey-api/client-fetch",
    "@hey-api/typescript",
    "@hey-api/sdk",
    "@tanstack/react-query",
  ],
});

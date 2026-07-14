import type { CreateClientConfig } from "./generated/client.gen";

/**
 * Runtime configuration hook called by the generated client at construction.
 *
 * Wired in via `runtimeConfigPath` in `openapi-ts.config.ts` so the client the
 * generated SDK operations use is the same one we configure for the BFF.
 */
export const createClientConfig: CreateClientConfig = (config) => ({
  ...config,
  baseUrl: "/api",
  credentials: "include",
});

import { fileURLToPath } from "node:url";
import type { UserConfig } from "vite";

/**
 * Absolute path to the vendored ESM `with-selector` (see that file's header
 * and the alias comment below). Resolved from this module's own URL so it
 * works wherever the consumer's `vite.config.ts` lives.
 */
const WITH_SELECTOR_ESM: string = fileURLToPath(
  new URL("use-sync-external-store-with-selector.mjs", import.meta.url),
);

/**
 * Options for the plugin-free TanStack Start app configuration. Each app supplies its own plugins
 * and deployment base path.
 */
export interface AppConfigOptions {
  /**
   * Development port used when PORT is absent. PORT is read when wallowAppConfig runs; values are
   * converted with Number without range validation.
   */
  readonly defaultPort: number;
}

/**
 * Return shared app port, module-resolution aliases, SSR dependency settings, and public-directory
 * copying. Merge with the app own Vite configuration, which provides plugins and any base path.
 * Reads PORT at call time and otherwise uses defaultPort.
 */
export function wallowAppConfig(options: AppConfigOptions): UserConfig {
  return {
    server: { port: Number(process.env.PORT ?? options.defaultPort) },
    resolve: {
      alias: [
        // Use React own external-store hook to avoid a second React instance through the CJS shim.
        // Exact aliases keep the with-selector subpath available for its separate adapter.
        { find: /^use-sync-external-store\/shim$/u, replacement: "react" },
        { find: /^use-sync-external-store\/shim\/index\.js$/u, replacement: "react" },
        // The selector shim needs its own ESM adapter so SSR resolves the same React instance.
        // Both extensionless and .js specifiers occur in consumers.
        {
          find: /^use-sync-external-store\/shim\/with-selector(?:\.js)?$/u,
          replacement: WITH_SELECTOR_ESM,
        },
      ],
      // Resolve app aliases from the consuming tsconfig paths.
      // The external-store regex aliases remain explicit because paths cannot express them.
      tsconfigPaths: true,
    },
    ssr: {
      /*
       * Bundle React Query and its router integration in the same SSR graph.
       * Separate bundled and external copies create different provider contexts.
       */
      noExternal: ["@tanstack/react-router-ssr-query", "@tanstack/react-query"],
    },
    environments: {
      // Copy shared public assets into the client output even when the Nitro plugin is installed.
      client: { build: { copyPublicDir: true } },
    },
  };
}

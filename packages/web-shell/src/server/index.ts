/**
 * Node-only entry for @bc-solutions-coder/web-shell (the `./server` subpath).
 *
 * This entry hosts the standalone-host runtime, dev-server, and Vite config
 * presets — pieces that need Node APIs and must never reach a client bundle. It
 * exposes the built-client static-asset reader (moved here in Wallow-0q2s.8.2);
 * the host, dev-server, and vite-preset factories arrive in Wallow-0q2s.8.3 – .8.5.
 */
export {
  createStaticAssetReader,
  type StaticAsset,
  type StaticAssetReader,
} from "../static-assets";
export { createStandaloneHost, type ShellConfig } from "./standalone-host";
export { createDevServer, type CreateDevServerDeps, type DevServerConfig } from "./dev-server";
export {
  createClientViteConfig,
  createSsrViteConfig,
  type ViteConfigOptions,
} from "./vite-presets";

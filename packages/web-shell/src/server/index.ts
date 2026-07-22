/**
 * Node-only entry for @bc-solutions-coder/web-shell (the `./server` subpath).
 *
 * This entry hosts the standalone-host runtime, dev-server, and Vite config
 * presets — pieces that need Node APIs and must never reach a client bundle. It
 * is a placeholder at the scaffold stage (Wallow-0q2s.8.1); the host, dev-server,
 * and vite-preset factories arrive in Wallow-0q2s.8.3 – .8.5. The marker export
 * only exists so the package builds and typechecks while the scaffold is wired up.
 */
export const WEB_SHELL_SERVER_ENTRY = "@bc-solutions-coder/web-shell/server" as const;

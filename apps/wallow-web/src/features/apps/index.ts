/**
 * The apps feature's public contract.
 *
 * Three categories and no more: the components routes mount, the query options
 * their loaders prefetch, and the public values a route's CONFIGURATION needs.
 * `api.ts` itself stays internal — the seam is the feature's business, not its
 * consumers'.
 */
export { appsGetUserAppsOptions } from "./api";
export { AppList } from "./components/AppList";
export { RegisterAppForm } from "./components/RegisterAppForm";

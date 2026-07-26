/**
 * Settings (Profile) feature `api.ts` (Wallow-evd5.2.2) — a THIN RE-EXPORT SEAM
 * over the SDK query layer (`@bc-solutions-coder/sdk/query`). Profile is
 * READ-ONLY (no mutation endpoint), so the seam exposes only `settingsQueries`.
 * Routes/components keep importing from `./api`.
 */
export { settingsQueries } from "@bc-solutions-coder/sdk/query";

/**
 * The single source for wallow-web's outbound site links (Wallow-urec.2.1).
 *
 * `PublicLayout` used to define its own repository/docs constants and DERIVE the
 * docs URL from the repository URL, which diverged from the canonical
 * `docsUrl` that `packages/styles/branding.json` now supplies. Every rendered GitHub/Docs
 * link reads from here instead.
 *
 */

import { forkBranding } from "@bc-solutions-coder/styles";

/** The fork's repository, from `forkBranding.repositoryUrl`. */
export const repositoryUrl: string =
  forkBranding.repositoryUrl ?? "https://github.com/bc-solutions-coder/wallow";

/** The fork's documentation site, from `forkBranding.docsUrl`. */
export const docsUrl: string =
  forkBranding.docsUrl ?? "https://bc-solutions-coder.github.io/wallow/";

/**
 * The "Get Started" CTA target: the BFF login flow, returning to the dashboard.
 *
 * CONTRACT: `e2e-cross-app/login-journey.spec.ts` hardcodes this href — never change it.
 */
export const getStartedHref: string = "/bff/login?returnTo=/dashboard/apps";

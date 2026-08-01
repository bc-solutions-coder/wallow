/**
 * The one outbound link wallow-web owns.
 *
 * The fork's repository and docs URLs are `@bc-solutions-coder/styles`'
 * `forkRepositoryUrl` / `forkDocsUrl` — fork identity, resolved from
 * `branding.json`. This one is not: it is a path into THIS app's BFF.
 */

/**
 * The "Get Started" CTA target: the BFF login flow, returning to the dashboard.
 *
 * CONTRACT: `e2e-cross-app/login-journey.spec.ts` hardcodes this href — never change it.
 */
export const getStartedHref: string = "/bff/login?returnTo=/dashboard/apps";

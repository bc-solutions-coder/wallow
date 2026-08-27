/**
 * The fork's branding, bound to the prefix this build is served under.
 *
 * `@bc-solutions-coder/styles` exports `appIconUrl` and `forkResolvedBranding`
 * as constants, but they are resolved at the SITE root — the package ships a
 * prebuilt bundle, so its own `import.meta.env.BASE_URL` is whatever it was when
 * the package was built ("/"), never this app's. Under `AUTH_BASE_PATH=/auth`
 * the assets are served at `/auth/piggy-icon.svg` (Vite's `base` plus nitro's
 * `baseURL`), so those constants point at the site root — which behind the
 * path-based ingress this whole knob exists for is a DIFFERENT app.
 *
 * So the prefix is handed to the package's base-path-aware functions, here,
 * once. Every screen imports the fork's branding from this module rather than
 * from the package, which is what keeps a stray `import { appIconUrl } from
 * "@bc-solutions-coder/styles"` from quietly reintroducing the unprefixed URL.
 *
 * COPY THIS MODULE when you give another app a path prefix. `wallow-web` and
 * `minimal-app` import the package-level `appIconUrl` / `forkResolvedBranding`
 * constants directly, which is correct only while they are served at the site
 * root — `basePath` defaults to `""`, the identity. The moment either one gains
 * an `AUTH_BASE_PATH` equivalent, those constants become wrong in exactly the
 * way described above, and the fix is this file: one per-app module binding the
 * prefix once, with every screen importing branding from it.
 */
import {
  type ResolvedBranding,
  resolveForkBranding,
  toAppIconUrl,
} from "@bc-solutions-coder/styles";

import { BASE_PATH } from "./base-path";

/** The fork's app icon, served from under this build's base path. */
export const appIconUrl: string = toAppIconUrl(BASE_PATH);

/**
 * The fork's own branding — the shell's palette and identity, and the fallback
 * whenever no `client_id` identifies a client or its branding cannot be fetched.
 */
export const forkResolvedBranding: ResolvedBranding = resolveForkBranding(BASE_PATH);

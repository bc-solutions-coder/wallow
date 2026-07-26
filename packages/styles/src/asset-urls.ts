/**
 * Brand asset URLs, resolved against the site root rather than the current page.
 *
 * `api/branding.json` names its assets by bare filename (`appIcon:
 * "piggy-icon.svg"`), which is what a fork should be able to write. Handing that
 * value straight to an `<img src>` makes the browser resolve it against the
 * current document's URL, so the icon that loads from `/login` 404s from
 * `/mfa/challenge` — the browser asks for `/mfa/piggy-icon.svg`. Blazor
 * normalised such paths against the app base; React has no equivalent, so the
 * root-relative form is produced here instead, once, for every consumer.
 *
 * This module is pure string work and stays free of `node:` imports on purpose:
 * it is reachable from the package's browser-facing entry, so the consuming app
 * bundles it into its client build. The filesystem side of the same assets
 * (where they live, so a build can copy them) is the `./assets` subpath.
 *
 * It imports nothing, deliberately: `branding.ts` depends on it (the fork's
 * resolved `logoUrl` is an asset reference like any other), so anything it
 * imported from there would be a cycle.
 */

/**
 * Turn a branding asset reference into a root-relative URL, so it resolves to
 * the same file from every route depth.
 *
 * Absolute URLs (a client's hosted `logoUrl`) are already unambiguous and are
 * returned untouched.
 */
export function toRootRelativeAssetUrl(assetPath: string): string {
  // Absolute URLs (a client's hosted logoUrl) — and protocol-relative ones —
  // are already unambiguous from any route; rooting them would break them.
  if (/^[a-z][a-z0-9+.-]*:\/\//iu.test(assetPath) || assetPath.startsWith("//")) {
    return assetPath;
  }

  // Already root-relative: idempotent, so a value that has been through here —
  // or a fork that wrote the leading slash itself — never becomes //piggy-icon.svg.
  if (assetPath.startsWith("/")) {
    return assetPath;
  }

  // A bare filename or a ./-relative path both name a file at the root; strip
  // the leading ./ and add the slash the browser needs to resolve it there.
  return `/${assetPath.replace(/^\.\//u, "")}`;
}

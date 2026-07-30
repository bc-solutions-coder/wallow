/**
 * Brand asset URLs, resolved against the app's served root rather than the
 * current page.
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
 * Reduce a consuming app's base path to one canonical shape: either the empty
 * string (no prefix) or a leading-slash path with no trailing slash (`/auth`).
 *
 * Deliberately permissive about its input. The value reaching {@link
 * toRootRelativeAssetUrl} is whatever the app has: Vite's own
 * `import.meta.env.BASE_URL` (`/` unprefixed, `/auth/` prefixed), an
 * already-normalised prefix an app derived for its router and passthrough, or a
 * string a fork hand-copied into a compose file. All of them have to mean the
 * same thing here, because the alternative is an icon that 404s in production
 * over a stray slash.
 */
function normalizeAssetBasePath(basePath: string): string {
  const bare: string = basePath.trim().replace(/^\/+/u, "").replace(/\/+$/u, "");
  return bare === "" ? "" : `/${bare}`;
}

/**
 * Turn a branding asset reference into a URL rooted at the prefix the app is
 * served under, so it resolves to the same file from every route depth.
 *
 * `basePath` is the app's URL prefix, empty by default — its build-time
 * `AUTH_BASE_PATH`-style knob, in any of the shapes {@link
 * normalizeAssetBasePath} accepts. It is a PARAMETER rather than something read
 * here because this package ships a prebuilt bundle: its own
 * `import.meta.env.BASE_URL` is frozen at `/` when the package is built, so a
 * value read here would silently unprefix every consumer. Under a path-based
 * ingress that matters twice over — the site root the unprefixed URL points at
 * is a *different app*, so the icon does not merely 404, it 404s against someone
 * else.
 *
 * Absolute URLs (a client's hosted `logoUrl`) are already unambiguous and are
 * returned untouched.
 */
export function toRootRelativeAssetUrl(assetPath: string, basePath: string = ""): string {
  // Absolute URLs (a client's hosted logoUrl) — and protocol-relative ones —
  // are already unambiguous from any route; rooting them would break them, and
  // they are on someone else's origin, where this app's prefix means nothing.
  if (/^[a-z][a-z0-9+.-]*:\/\//iu.test(assetPath) || assetPath.startsWith("//")) {
    return assetPath;
  }

  const base: string = normalizeAssetBasePath(basePath);

  // Already under the prefix: idempotent, so a value that has been through here
  // never becomes /auth/auth/piggy-icon.svg. Segment boundary only, the same
  // rule the passthrough strips its prefix by — `/authentic` is not below
  // `/auth`, so that path still needs prefixing.
  if (base !== "" && assetPath.startsWith(`${base}/`)) {
    return assetPath;
  }

  // Root-relative already — a fork that wrote the leading slash itself. Only the
  // prefix is missing, and with no prefix this is the identity.
  if (assetPath.startsWith("/")) {
    return `${base}${assetPath}`;
  }

  // A bare filename or a ./-relative path both name a file at the served root;
  // strip the leading ./ and add the slash the browser needs to resolve it there.
  return `${base}/${assetPath.replace(/^\.\//u, "")}`;
}

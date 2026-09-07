/**
 * Resolve branding asset references against the consuming app URL prefix. Filesystem asset
 * locations are available through the Node-only ./assets entry.
 */

/**
 * Normalize outer slashes so root and prefixed app URLs have consistent asset paths.
 */
function normalizeAssetBasePath(basePath: string): string {
  const bare: string = basePath.trim().replace(/^\/+/u, "").replace(/\/+$/u, "");
  return bare === "" ? "" : `/${bare}`;
}

/**
 * Root an asset reference under the app base path so nested routes resolve the same file. Accepts
 * base paths such as auth, /auth, and /auth/. Existing paths below that prefix and URLs with a
 * scheme followed by // or a leading // are returned unchanged. This helper does not sanitize URL
 * schemes or path traversal.
 */
export function toRootRelativeAssetUrl(assetPath: string, basePath: string = ""): string {
  // Preserve hosted URLs and protocol-relative URLs without adding the app prefix.
  if (/^[a-z][a-z0-9+.-]*:\/\//iu.test(assetPath) || assetPath.startsWith("//")) {
    return assetPath;
  }

  const base: string = normalizeAssetBasePath(basePath);

  // Avoid duplicating an existing prefix, matching on a segment boundary.
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

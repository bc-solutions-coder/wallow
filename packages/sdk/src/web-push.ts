/**
 * Resolve a notification click destination to an absolute URL on the supplied origin.
 *
 * Accepts local paths starting with `/`. Missing or invalid destinations,
 * protocol-relative URLs, and paths containing backslashes fall back to `/`.
 * The supplied origin must be a valid absolute URL.
 */
export function resolveWebPushClickUrl(destination: unknown, origin: string): string {
  const home = new URL("/", origin);
  if (
    typeof destination !== "string" ||
    !destination.startsWith("/") ||
    destination.startsWith("//") ||
    destination.includes("\\")
  ) {
    return home.href;
  }
  const target = new URL(destination, home);
  return target.origin === home.origin ? target.href : home.href;
}

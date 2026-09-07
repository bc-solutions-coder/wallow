/** Resolve notification navigation against the consumer origin, with a home-page fallback. */
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

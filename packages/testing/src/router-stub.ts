/**
 * Verify that a router mock exposes its wallowRouterStub marker.
 * Call assertRouterStubApplied(Link) in beforeEach when mocking the router.
 * Mark rendered links with data-router-stub for navigation failure diagnostics.
 */

/** Throws unless `link` is a component carrying the inline stub marker. */
export function assertRouterStubApplied(link: unknown): void {
  const isComponent = typeof link === "function" || (typeof link === "object" && link !== null);

  if (isComponent && "wallowRouterStub" in link) {
    return;
  }

  throw new Error(
    "router stub not applied: @tanstack/react-router resolved to the real module, so this file's vi.mock factory never took effect",
  );
}

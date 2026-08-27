/**
 * The stub-applied guard for specs that `vi.mock("@tanstack/react-router")`.
 *
 * A silently-unapplied mock hands the spec the real `Link`, which throws a
 * misleading `TypeError` three steps downstream of the actual defect — or, if a
 * live anchor survives to be clicked, a navigation leak the runner blames on a
 * neighbouring file. The stub marks itself so the absence fails BY NAME instead.
 *
 * A `vi.mock` factory is hoisted above imports and cannot reference one, so the
 * marker is attached inline in each factory rather than through a helper here:
 *
 * ```tsx
 * vi.mock("@tanstack/react-router", () => ({
 *   Link: Object.assign((props: LinkStubProps) => <a ... />, {
 *     wallowRouterStub: true,
 *   }),
 * }));
 * ```
 *
 * The spec then imports `Link` normally and runs the check once per test:
 *
 * ```ts
 * beforeEach(() => {
 *   assertRouterStubApplied(Link);
 * });
 * ```
 *
 * The stub's rendered anchor should also carry `data-router-stub="true"`, which
 * `./navigation-escape`'s forensics read back: an escape initiated by a marked
 * anchor proves the stub WAS applied and its `preventDefault` did not run.
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

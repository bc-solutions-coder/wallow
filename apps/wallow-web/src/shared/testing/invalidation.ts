/**
 * Asserting on curated invalidation (Wallow-pu6a.5.5).
 *
 * The hand-written query layer's keys were hierarchical, so a spec could assert
 * a mutation swept its list with a literal:
 * `expect(invalidateSpy).toHaveBeenCalledWith({ queryKey: ["apps"] })`.
 *
 * The generated keys are flat — one object segment carrying `_id` and the
 * operation's OpenAPI `tags` — so a mutation invalidates through a PREDICATE
 * (`queriesWithTag` / `queriesForOperation`), and there is no literal key to
 * compare against. What a spec actually cares about is unchanged, though: "does
 * the sweep this mutation asked for reach the query this screen reads?" That is
 * what these answer — by running the real predicate against the real generated
 * key, so a mutation invalidating the wrong tag still fails the spec.
 */
import type { Query, QueryFilters } from "@bc-solutions-coder/query";
import { expect, vi } from "vitest";

/** Would `filters` sweep a cached query keyed `queryKey`? */
export function sweeps(filters: unknown, queryKey: readonly unknown[]): boolean {
  const predicate: QueryFilters["predicate"] = (filters as QueryFilters | undefined)?.predicate;

  return predicate !== undefined && predicate({ queryKey } as Query);
}

/**
 * Wait until `spy` (a `queryClient.invalidateQueries` spy) has been called with
 * a filter that sweeps `queryKey`.
 *
 * @param spy Spy installed over the render's `queryClient.invalidateQueries`.
 * @param queryKey A generated `{op}QueryKey(...)` the sweep must reach.
 */
export async function expectSwept(
  spy: { mock: { calls: readonly (readonly unknown[])[] } },
  queryKey: readonly unknown[],
): Promise<void> {
  await vi.waitFor(() => {
    expect(spy.mock.calls.some((call: readonly unknown[]) => sweeps(call[0], queryKey))).toBe(true);
  });
}

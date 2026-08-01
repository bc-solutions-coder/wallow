/**
 * Asserting on curated invalidation.
 *
 * Generated query keys are flat — one object segment carrying `_id` and the
 * operation's OpenAPI `tags` — so a mutation invalidates through a PREDICATE
 * (`queriesWithTag` / `queriesForOperation`) and there is no literal key to
 * compare a spy call against. What a spec cares about is still "does the sweep
 * this mutation asked for reach the query this screen reads?", which these
 * answer by running the REAL predicate against the REAL generated key — so a
 * mutation invalidating the wrong tag fails the spec.
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

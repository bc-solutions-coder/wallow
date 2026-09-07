/**
 * Assert that a query invalidation predicate matches an actual generated query key.
 */
import type { Query, QueryFilters } from "@bc-solutions-coder/query";
import { expect, vi } from "vitest";

/**
 * Return whether a query filter's predicate matches a generated query key.
 *
 * Only evaluates predicate filters. Other filter fields are ignored, and a filter
 * without a predicate returns false. The predicate receives only queryKey.
 */
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

/**
 * Curated invalidation filters over the GENERATED query keys (Wallow-pu6a.5.2).
 *
 * hey-api's `@tanstack/react-query` plugin emits a FLAT single-segment key —
 * `[{ _id, baseUrl, tags?, body?, headers?, path?, query? }]` — where `_id` is
 * the operation name and `tags` are the operation's OpenAPI tags. There is no
 * hierarchical prefix to sweep by, so TanStack's usual "invalidate the parent
 * key" trick has nothing to match: `{ queryKey: ['orgs'] }` is not a prefix of
 * anything the generator produces.
 *
 * These two predicates are the supported replacement. Both return TanStack
 * `QueryFilters`, so they compose with the normal call:
 *
 * ```ts
 * await queryClient.invalidateQueries(queriesWithTag("Organizations"));
 * await queryClient.invalidateQueries(
 *   queriesForOperation(organizationsGetByIdQueryKey({ client, path: { id } })),
 * );
 * ```
 *
 * This module is the ONLY hand-written file on the `./query` entry; everything
 * else it exposes is re-exported straight from `src/generated`.
 */
import type { Query, QueryFilters, QueryKey } from "@tanstack/react-query";

/** The single object segment every generated query key carries. */
// Indexed access, not `segment._id`: the generator's own key member is
// underscore-prefixed, which dot notation would trip `no-underscore-dangle` on.
type GeneratedKeySegment = Record<string, unknown>;

function keySegment(queryKey: QueryKey): GeneratedKeySegment | undefined {
  const [segment]: readonly unknown[] = queryKey;

  return typeof segment === "object" && segment !== null && !Array.isArray(segment)
    ? (segment as GeneratedKeySegment)
    : undefined;
}

/** The operation id baked into a generated key, or `undefined` for any other key. */
function operationId(queryKey: QueryKey): string | undefined {
  const id: unknown = keySegment(queryKey)?.["_id"];

  return typeof id === "string" ? id : undefined;
}

/** The OpenAPI tags baked into a generated key (empty unless tagged keys were generated). */
function tagsOf(queryKey: QueryKey): readonly string[] {
  const tags: unknown = keySegment(queryKey)?.["tags"];

  return Array.isArray(tags) ? tags.filter((tag: unknown) => typeof tag === "string") : [];
}

/**
 * Match cached queries carrying an OpenAPI tag, for use with `invalidateQueries`.
 *
 * Matches every operation and argument combination with that tag, including
 * queries for different client base URLs. Tags may cover several controllers.
 */
export function queriesWithTag(tag: string): QueryFilters {
  return {
    predicate: (query: Query): boolean => tagsOf(query.queryKey).includes(tag),
  };
}

/**
 * Match cached queries for the same operation as a generated query key.
 *
 * Pass a key from a generated `...QueryKey()` helper. The predicate matches
 * all argument combinations and client base URLs for that operation. A key
 * without an operation identifier matches nothing.
 */
export function queriesForOperation(queryKey: readonly unknown[]): QueryFilters {
  const id: string | undefined = operationId(queryKey as QueryKey);

  return {
    predicate: (query: Query): boolean => id !== undefined && operationId(query.queryKey) === id,
  };
}

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
 * Every cached query whose generated key carries `tag` — the closest thing to a
 * feature-wide sweep, since a tag groups exactly the operations one backend
 * controller exposes.
 *
 * Keys the generator did not build (and generated keys from a build without
 * `queryKeys: { tags: true }`) carry no tags and are never matched.
 */
export function queriesWithTag(tag: string): QueryFilters {
  return {
    predicate: (query: Query): boolean => tagsOf(query.queryKey).includes(tag),
  };
}

/**
 * Every cached query for the SAME operation as `queryKey`, whatever arguments it
 * was called with — `queriesForOperation(detailKeyForOrgA)` also sweeps org B's
 * detail entry.
 *
 * Takes an EXEMPLAR key rather than an id string so callers stay off hey-api's
 * internal `_id` spelling: build one with the operation's generated
 * `{op}QueryKey(...)` and hand it in. A key with no `_id` (i.e. not a generated
 * one) matches nothing rather than matching everything.
 */
export function queriesForOperation(queryKey: readonly unknown[]): QueryFilters {
  const id: string | undefined = operationId(queryKey as QueryKey);

  return {
    predicate: (query: Query): boolean => id !== undefined && operationId(query.queryKey) === id,
  };
}

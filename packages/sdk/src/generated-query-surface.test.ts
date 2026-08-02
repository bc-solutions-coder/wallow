import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { QueryClient, type QueryFilters } from "@tanstack/react-query";
import { describe, expect, it } from "vitest";

import generatorConfig from "../openapi-ts.config";
import { createWallowSdk, type WallowSdk } from "./create-sdk";
import * as queryEntry from "./query";

/**
 * Spec (Wallow-pu6a.5.2): the TanStack Query layer is GENERATED for every
 * operation, and its keys are hey-api's flat `[{ _id, baseUrl, path, tags }]`
 * shape — not the hand-written hierarchical `queryKeys` factory the previous
 * `src/query/keys.ts` exposed.
 *
 * What is pinned here, in the order the acceptance criteria list it:
 *
 *   1. `openapi-ts.config.ts` asks for the surface: `throwOnError` on the
 *      client, `responseStyle: "data"` on the sdk plugin, and the
 *      `@tanstack/react-query` plugin with `queryOptions`, `queryKeys.tags`
 *      and `mutationOptions` all on.
 *   2. Coverage: EVERY operation in the committed snapshot has its generated
 *      artifact reachable from the `./query` entry — a query options factory
 *      and key builder for every GET, a mutation options factory for every
 *      write. This is an invariant over the generated output, so a backend that
 *      adds an endpoint keeps passing without editing this file, while a config
 *      regression that silently drops the plugin fails it.
 *   3. (D4) A server instance and a browser instance built with the same
 *      `baseUrl` emit BYTE-IDENTICAL keys for the same operation — task 3.5(e)
 *      proved it for the client config and the pre-transport request URL; this
 *      proves it for the artifact that actually keys the cache, which is what
 *      makes an SSR-primed cache hydrate in the browser instead of refetching.
 *   4. `invalidations.ts` sweeps a subtree by matching on the flat key's `_id`
 *      and `tags`, since hey-api generates no hierarchical prefix to sweep by.
 */

// packages/sdk/src -> packages/sdk
const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const snapshotPath: string = resolve(packageRoot, "openapi/v1.json");

const TANSTACK_PEER: string = "@tanstack/react-query";

const BROWSER_BASE_URL: string = "https://app.test/api";
const INTERNAL_ORIGIN: string = "http://localhost:3000";

// hey-api's default operation-kind hook: GET is a query, the four write verbs
// are mutations, anything else generates neither.
const QUERY_METHODS: ReadonlySet<string> = new Set(["get"]);
const MUTATION_METHODS: ReadonlySet<string> = new Set(["delete", "patch", "post", "put"]);

/** Everything the published `./query` entry exposes, by export name. */
const querySurface: Record<string, unknown> = { ...queryEntry };

interface SnapshotOperation {
  operationId?: string;
  tags?: string[];
}

interface OpenApiSnapshot {
  paths: Record<string, Record<string, SnapshotOperation>>;
}

interface GeneratedOperation {
  /** The generated function name, i.e. the camelCased `operationId`. */
  name: string;
  method: string;
  tags: readonly string[];
}

/**
 * Every operation in the committed snapshot, named the way the generator names
 * it. hey-api camelCases the `operationId`, and `openapi-regen.test.ts` already
 * pins that every operation HAS a unique one, so lowering the first character is
 * the whole transform.
 */
function loadOperations(): GeneratedOperation[] {
  const snapshot: OpenApiSnapshot = JSON.parse(
    readFileSync(snapshotPath, "utf8"),
  ) as OpenApiSnapshot;
  const operations: GeneratedOperation[] = [];

  for (const pathItem of Object.values(snapshot.paths)) {
    for (const [method, operation] of Object.entries(pathItem)) {
      const operationId: string | undefined = operation.operationId;
      if (
        operationId !== undefined &&
        (QUERY_METHODS.has(method) || MUTATION_METHODS.has(method))
      ) {
        operations.push({
          method,
          name: `${operationId.charAt(0).toLowerCase()}${operationId.slice(1)}`,
          tags: operation.tags ?? [],
        });
      }
    }
  }

  return operations;
}

const operations: GeneratedOperation[] = loadOperations();

function operationsWithMethodIn(methods: ReadonlySet<string>): GeneratedOperation[] {
  return operations.filter((operation: GeneratedOperation) => methods.has(operation.method));
}

/** Export names the entry is missing a callable for, given a name suffix. */
function missingArtifacts(candidates: readonly GeneratedOperation[], suffix: string): string[] {
  return candidates
    .map((operation: GeneratedOperation) => `${operation.name}${suffix}`)
    .filter((exportName: string) => typeof querySurface[exportName] !== "function");
}

function requireExport<T>(exportName: string): T {
  const value: unknown = querySurface[exportName];
  if (typeof value !== "function") {
    throw new TypeError(`@bc-solutions-coder/sdk/query does not export ${exportName}`);
  }

  return value as T;
}

type QueryKeyBuilder = (options: Record<string, unknown>) => readonly unknown[];

function queryKeyBuilder(operationName: string): QueryKeyBuilder {
  return requireExport<QueryKeyBuilder>(`${operationName}QueryKey`);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * The resolved plugin entry named `name`. `defineConfig` is a pass-through at
 * this pin, so an object entry here proves the config file spells the option out
 * rather than inheriting a default that a version bump could change.
 */
async function resolvePlugin(name: string): Promise<Record<string, unknown>> {
  const config = await generatorConfig;
  const plugins: unknown = config.plugins;

  expect(Array.isArray(plugins)).toBe(true);
  if (!Array.isArray(plugins)) {
    throw new TypeError("openapi-ts.config.ts must declare a plugins array");
  }

  const entry: unknown = plugins.find(
    (plugin: unknown) => isRecord(plugin) && plugin.name === name,
  );

  expect(isRecord(entry)).toBe(true);
  if (!isRecord(entry)) {
    throw new TypeError(`openapi-ts.config.ts must configure the ${name} plugin as an object`);
  }

  return entry;
}

describe("openapi-ts.config.ts asks for the full generated query surface", () => {
  it("keeps the runtime config hook and turns on throwOnError for the client", async () => {
    const client: Record<string, unknown> = await resolvePlugin("@hey-api/client-fetch");

    expect(client.runtimeConfigPath).toBe("./src/runtime-config");
    // Every operation must reject on a non-2xx so the WallowError interceptor
    // (task 5.3) is the single error path; without this the generated query
    // functions resolve with an error payload and TanStack Query calls it a
    // success.
    expect(client.throwOnError).toBe(true);
  });

  it("configures the sdk plugin to return response data directly", async () => {
    const sdk: Record<string, unknown> = await resolvePlugin("@hey-api/sdk");

    expect(sdk.responseStyle).toBe("data");
  });

  it("generates query options, tagged query keys, and mutation options", async () => {
    const tanstack: Record<string, unknown> = await resolvePlugin(TANSTACK_PEER);

    expect(tanstack.queryOptions).toBe(true);
    expect(tanstack.mutationOptions).toBe(true);
    // Tags are what `invalidations.ts` sweeps by — hey-api emits no hierarchical
    // key prefix, so without them a subtree invalidation has nothing to match.
    expect(tanstack.queryKeys).toEqual({ tags: true });
  });
});

describe("the ./query entry exposes a generated artifact for every operation", () => {
  it("reads a non-trivial operation set out of the committed snapshot", () => {
    expect(operationsWithMethodIn(QUERY_METHODS).length).toBeGreaterThan(0);
    expect(operationsWithMethodIn(MUTATION_METHODS).length).toBeGreaterThan(0);
  });

  it("exports a query options factory for every GET operation", () => {
    expect(missingArtifacts(operationsWithMethodIn(QUERY_METHODS), "Options")).toEqual([]);
  });

  it("exports a query key builder for every GET operation", () => {
    expect(missingArtifacts(operationsWithMethodIn(QUERY_METHODS), "QueryKey")).toEqual([]);
  });

  it("exports a mutation options factory for every write operation", () => {
    expect(missingArtifacts(operationsWithMethodIn(MUTATION_METHODS), "Mutation")).toEqual([]);
  });
});

describe("(D4) a server instance and a browser instance emit identical query keys", () => {
  function browserSdk(): WallowSdk {
    return createWallowSdk({ baseUrl: BROWSER_BASE_URL });
  }

  function serverSdk(): WallowSdk {
    return createWallowSdk({
      baseUrl: BROWSER_BASE_URL,
      cookieHeader: "wallow_session=abc",
      internalOrigin: INTERNAL_ORIGIN,
    });
  }

  /**
   * The two keys for one operation, serialized. A generated key embeds the
   * client's `baseUrl` (V5), so anything that leaked `internalOrigin` or the
   * forwarded cookie into the client config would show up as a diff here — and
   * a diff means the SSR-primed cache entry and the browser's are two entries.
   */
  function keysForBothInstances(
    operationName: string,
    callOptions: Record<string, unknown> = {},
  ): [string, string] {
    const build: QueryKeyBuilder = queryKeyBuilder(operationName);

    return [
      JSON.stringify(build({ ...callOptions, client: browserSdk().client })),
      JSON.stringify(build({ ...callOptions, client: serverSdk().client })),
    ];
  }

  it("agrees on the key of a parameterless operation", () => {
    const [browser, server] = keysForBothInstances("usersGetCurrentUser");

    expect(server).toBe(browser);
  });

  it("agrees on the key of an operation with a path parameter", () => {
    const [browser, server] = keysForBothInstances("organizationsGetById", {
      path: { id: "org-1" },
    });

    expect(server).toBe(browser);
  });

  it("agrees on the key of an operation with query parameters", () => {
    const [browser, server] = keysForBothInstances("organizationsGetAll", {
      query: { first: 0, max: 25, search: "acme" },
    });

    expect(server).toBe(browser);
  });

  it("still distinguishes different arguments to the same operation", () => {
    const build: QueryKeyBuilder = queryKeyBuilder("organizationsGetById");
    const sdk: WallowSdk = browserSdk();

    expect(JSON.stringify(build({ client: sdk.client, path: { id: "org-1" } }))).not.toBe(
      JSON.stringify(build({ client: sdk.client, path: { id: "org-2" } })),
    );
  });
});

describe("curated invalidations sweep the flat generated keys", () => {
  // The tag every Organizations operation carries in the snapshot — read from
  // the document rather than typed in, so a backend rename fails loudly instead
  // of quietly matching nothing.
  const organizationsTag: string =
    operations.find((operation: GeneratedOperation) => operation.name === "organizationsGetById")
      ?.tags[0] ?? "";

  interface SeededCache {
    client: QueryClient;
    organizationDetail: readonly unknown[];
    organizationList: readonly unknown[];
    currentUser: readonly unknown[];
  }

  function seedCache(): SeededCache {
    const sdk: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    const organizationDetail: readonly unknown[] = queryKeyBuilder("organizationsGetById")({
      client: sdk.client,
      path: { id: "org-1" },
    });
    const organizationList: readonly unknown[] = queryKeyBuilder("organizationsGetAll")({
      client: sdk.client,
    });
    const currentUser: readonly unknown[] = queryKeyBuilder("usersGetCurrentUser")({
      client: sdk.client,
    });
    const client: QueryClient = new QueryClient();

    for (const key of [organizationDetail, organizationList, currentUser]) {
      client.setQueryData(key, { seeded: true });
    }

    return { client, currentUser, organizationDetail, organizationList };
  }

  function isInvalidated(cache: SeededCache, key: readonly unknown[]): boolean {
    return cache.client.getQueryState(key)?.isInvalidated ?? false;
  }

  it("names the Organizations tag in the snapshot", () => {
    expect(organizationsTag).not.toBe("");
  });

  it("sweeps every cache entry carrying a tag", async () => {
    const queriesWithTag = requireExport<(tag: string) => QueryFilters>("queriesWithTag");
    const cache: SeededCache = seedCache();

    await cache.client.invalidateQueries(queriesWithTag(organizationsTag));

    expect(isInvalidated(cache, cache.organizationDetail)).toBe(true);
    expect(isInvalidated(cache, cache.organizationList)).toBe(true);
    // An untagged sweep of the whole cache would be indistinguishable from a
    // targeted one without this.
    expect(isInvalidated(cache, cache.currentUser)).toBe(false);
  });

  it("sweeps every cache entry of one operation regardless of its arguments", async () => {
    const queriesForOperation =
      requireExport<(queryKey: readonly unknown[]) => QueryFilters>("queriesForOperation");
    const cache: SeededCache = seedCache();
    const sdk: WallowSdk = createWallowSdk({ baseUrl: BROWSER_BASE_URL });
    const otherDetail: readonly unknown[] = queryKeyBuilder("organizationsGetById")({
      client: sdk.client,
      path: { id: "org-2" },
    });
    cache.client.setQueryData(otherDetail, { seeded: true });

    // Matching is by the key's `_id`, so the arguments baked into the key handed
    // in must not narrow the sweep.
    await cache.client.invalidateQueries(queriesForOperation(cache.organizationDetail));

    expect(isInvalidated(cache, cache.organizationDetail)).toBe(true);
    expect(isInvalidated(cache, otherDetail)).toBe(true);
    expect(isInvalidated(cache, cache.organizationList)).toBe(false);
    expect(isInvalidated(cache, cache.currentUser)).toBe(false);
  });
});

/**
 * Adapts a real node-redis client to the {@link RedisLike} port that
 * {@link ValkeySessionStore} depends on.
 *
 * The SDK never imports `redis` itself — the consumer constructs and connects
 * the client, then hands it here. This keeps the SDK's dependency surface empty
 * for callers who only use the cookie store, while still giving Valkey-backed
 * BFF hosts a ready-made bridge:
 *
 * ```ts
 * import { createClient } from "redis";
 * import { createRedisAdapter, ValkeySessionStore } from "@bc-solutions-coder/sdk/server";
 *
 * const client = createClient({ url: process.env.REDIS_URL });
 * await client.connect();
 * const store = new ValkeySessionStore({
 *   client: createRedisAdapter(client),
 *   password: config.cookiePassword,
 * });
 * ```
 */

import { type RedisLike } from "./types";

/**
 * The subset of a node-redis v4+ client that the adapter calls. Declared
 * structurally so the SDK takes no dependency on `redis`; any client matching
 * this shape works.
 */
export interface NodeRedisClient {
  get: (key: string) => Promise<string | null>;
  set: (
    key: string,
    value: string,
    options?: { EX?: number; NX?: boolean },
  ) => Promise<string | null>;
  del: (key: string) => Promise<number>;
}

/**
 * Wrap a node-redis client as a {@link RedisLike}, translating the store's
 * lowercase `{ex, nx}` set options to node-redis's `{EX, NX}` and narrowing the
 * set result to `"OK" | null`.
 */
export function createRedisAdapter(client: NodeRedisClient): RedisLike {
  return {
    get(key: string): Promise<string | null> {
      return client.get(key);
    },
    async set(
      key: string,
      value: string,
      opts?: { ex?: number; nx?: boolean },
    ): Promise<"OK" | null> {
      let options: { EX?: number; NX?: boolean } | undefined;
      if (opts !== undefined) {
        options = {};
        if (opts.ex !== undefined) {
          options.EX = opts.ex;
        }
        if (opts.nx === true) {
          options.NX = true;
        }
      }
      const result: string | null = await client.set(key, value, options);
      return result === "OK" ? "OK" : null;
    },
    del(key: string): Promise<number> {
      return client.del(key);
    },
  };
}

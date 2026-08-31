/**
 * Adapts a real node-redis client to the {@link RedisLike} port that
 * {@link ValkeySessionStore} depends on.
 *
 * `redis` is an OPTIONAL peer of the SDK: a host that only uses the cookie store
 * never installs it, and this module never imports it — the port below is
 * declared structurally, wide enough that a real `RedisClientType` is assignable
 * without a hand-written bridge:
 *
 * ```ts
 * import { createClient } from "redis";
 * import { createRedisAdapter, ValkeySessionStore } from "@bc-solutions-coder/sdk/server";
 *
 * const client = createClient({ url: process.env.REDIS_URL });
 * await client.connect();
 * const store = new ValkeySessionStore({
 *   client: createRedisAdapter(client),
 *   password: config.cookiePasswords ?? config.cookiePassword,
 * });
 * ```
 *
 * A host that would rather not own the connection sets `REDIS_URL` and lets
 * `createWallowBffServer` connect through `createRedisFromUrl` instead.
 */

import { type RedisLike } from "./types";

/**
 * The subset of a node-redis v4+ client that the adapter calls. Declared
 * structurally so the SDK takes no hard dependency on `redis`; any client
 * matching this shape works.
 *
 * A reply is typed `unknown` exactly where node-redis's own answer varies:
 * `GET`/`SET` answer `string | Buffer | null` (a `Buffer` only under its
 * buffer-reply mode, which the store never enables) and `EXPIRE`'s reply type
 * has shifted between major versions, so a port narrower than the real client
 * would force hosts to bridge those methods by hand — the adapter narrows
 * once, here. `DEL` and the set operations answer the same concrete types in
 * every mode, so those stay precise.
 */
export interface NodeRedisClient {
  get: (key: string) => Promise<unknown>;
  set: (key: string, value: string, options?: { EX?: number; NX?: true }) => Promise<unknown>;
  del: (key: string) => Promise<number>;
  sAdd: (key: string, member: string) => Promise<number>;
  sRem: (key: string, member: string) => Promise<number>;
  sMembers: (key: string) => Promise<string[]>;
  expire: (key: string, seconds: number) => Promise<unknown>;
}

/**
 * Wrap a node-redis client as a {@link RedisLike}, translating the store's
 * lowercase `{ex, nx}` set options to node-redis's `{EX, NX}` and narrowing the
 * replies to the port's `string | null` / `"OK" | null` / `number`.
 */
export function createRedisAdapter(client: NodeRedisClient): RedisLike {
  return {
    async get(key: string): Promise<string | null> {
      const result: unknown = await client.get(key);
      return typeof result === "string" ? result : null;
    },
    async set(
      key: string,
      value: string,
      opts?: { ex?: number; nx?: boolean },
    ): Promise<"OK" | null> {
      let options: { EX?: number; NX?: true } | undefined;
      if (opts !== undefined) {
        options = {};
        if (opts.ex !== undefined) {
          options.EX = opts.ex;
        }
        if (opts.nx === true) {
          options.NX = true;
        }
      }
      const result: unknown = await client.set(key, value, options);
      return result === "OK" ? "OK" : null;
    },
    del(key: string): Promise<number> {
      return client.del(key);
    },
    sadd(key: string, member: string): Promise<number> {
      return client.sAdd(key, member);
    },
    srem(key: string, member: string): Promise<number> {
      return client.sRem(key, member);
    },
    smembers(key: string): Promise<string[]> {
      return client.sMembers(key);
    },
    async expire(key: string, seconds: number): Promise<void> {
      await client.expire(key, seconds);
    },
  };
}

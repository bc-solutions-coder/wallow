/**
 * Connect a RedisLike store lazily through the optional redis peer. Construction stays
 * synchronous and hosts without Redis do not load the peer. Failed connections are retried on the
 * next call.
 */

import { createRedisAdapter, type NodeRedisClient } from "./redis-adapter";
import { type RedisLike } from "./types";

/** Options for {@link createRedisFromUrl}. */
export interface RedisFromUrlOptions {
  /**
   * Receives the client's `error` events. node-redis emits them on connection
   * loss and reconnect attempts, and an `error` event with NO listener crashes
   * the process, so one is always attached; supply a logger to see them.
   * Defaults to `console.error`.
   */
  onError?: (error: unknown) => void;
}

/** The slice of node-redis's `createClient` result this module drives. */
interface ConnectableRedisClient extends NodeRedisClient {
  on: (event: "error", listener: (error: unknown) => void) => unknown;
  connect: () => Promise<unknown>;
}

/** The slice of the `redis` module this module loads. */
interface RedisModule {
  createClient: (options: { url: string }) => ConnectableRedisClient;
}

/** Load the optional `redis` peer, turning "not installed" into an actionable error. */
async function loadRedisModule(): Promise<RedisModule> {
  try {
    // The specifier is a plain literal so bundlers keep it external; the cast
    // narrows node-redis's very wide client type to the port that is used.
    return (await import("redis")) as unknown as RedisModule;
  } catch (error: unknown) {
    throw new Error(
      "REDIS_URL is set, so the BFF needs the `redis` package to connect to the session " +
        "store. Install it (`pnpm add redis`) or unset REDIS_URL to use cookie sessions.",
      { cause: error },
    );
  }
}

/**
 * Create a RedisLike client that connects on its first operation.
 *
 * Loads the optional redis package lazily and shares the pending connection across concurrent
 * calls. A failed connection is retried on the next operation. For explicit connection lifecycle
 * control, supply a connected client through createRedisAdapter.
 *
 * @param url Redis connection URL accepted by node-redis.
 *
 * @param options Error-event callback, defaulting to console.error.
 *
 * @returns Deferred Redis operations, which reject on package-loading, connection, or command
 * failures.
 */
export function createRedisFromUrl(url: string, options: RedisFromUrlOptions = {}): RedisLike {
  const onError: (error: unknown) => void = options.onError ?? console.error;
  let pending: Promise<RedisLike> | undefined;

  const connect = async (): Promise<RedisLike> => {
    const redis: RedisModule = await loadRedisModule();
    const client: ConnectableRedisClient = redis.createClient({ url });
    client.on("error", onError);
    await client.connect();
    return createRedisAdapter(client);
  };

  const ready = (): Promise<RedisLike> => {
    pending ??= connect().catch((error: unknown) => {
      pending = undefined;
      throw error;
    });
    return pending;
  };

  return {
    get: async (key: string): Promise<string | null> => {
      const client: RedisLike = await ready();
      return client.get(key);
    },
    set: async (
      key: string,
      value: string,
      opts?: { ex?: number; nx?: boolean },
    ): Promise<"OK" | null> => {
      const client: RedisLike = await ready();
      return client.set(key, value, opts);
    },
    del: async (key: string): Promise<number> => {
      const client: RedisLike = await ready();
      return client.del(key);
    },
    sadd: async (key: string, member: string): Promise<number> => {
      const client: RedisLike = await ready();
      return client.sadd(key, member);
    },
    srem: async (key: string, member: string): Promise<number> => {
      const client: RedisLike = await ready();
      return client.srem(key, member);
    },
    smembers: async (key: string): Promise<string[]> => {
      const client: RedisLike = await ready();
      return client.smembers(key);
    },
    expire: async (key: string, seconds: number): Promise<void> => {
      const client: RedisLike = await ready();
      return client.expire(key, seconds);
    },
  };
}

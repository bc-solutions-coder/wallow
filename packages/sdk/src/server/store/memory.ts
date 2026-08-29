/**
 * In-process {@link RedisLike} backed by a `Map`, honouring the `ex` and `nx`
 * flags the SDK's stores and locks rely on.
 *
 * It is the default token cache of the service client when no shared store is
 * supplied: one process, one cache. It is NOT a session store — a session
 * cached only in one process is exactly what `ValkeySessionStore` exists to
 * avoid — so it is deliberately absent from the `./server` barrel.
 */

import { type RedisLike } from "./types";

/** Milliseconds per second, for translating an `ex` expiry onto `Date.now()`. */
const MS_PER_SECOND = 1000;

interface MemoryEntry {
  value: string;
  /** Epoch milliseconds the entry expires at, or `null` for no expiry. */
  expiresAt: number | null;
}

/** `DEL` reply when the key existed, and when it did not. */
const DELETED_COUNT = 1;
const NOT_FOUND_COUNT = 0;

/** Build an in-process {@link RedisLike}. */
export function createMemoryRedis(): RedisLike {
  const entries = new Map<string, MemoryEntry>();

  const alive = (key: string): MemoryEntry | undefined => {
    const entry: MemoryEntry | undefined = entries.get(key);
    if (entry === undefined) {
      return undefined;
    }
    if (entry.expiresAt !== null && entry.expiresAt <= Date.now()) {
      entries.delete(key);
      return undefined;
    }
    return entry;
  };

  return {
    get(key: string): Promise<string | null> {
      return Promise.resolve(alive(key)?.value ?? null);
    },
    set(key: string, value: string, opts?: { ex?: number; nx?: boolean }): Promise<"OK" | null> {
      if (opts?.nx === true && alive(key) !== undefined) {
        return Promise.resolve(null);
      }
      entries.set(key, {
        value,
        expiresAt: opts?.ex === undefined ? null : Date.now() + opts.ex * MS_PER_SECOND,
      });
      return Promise.resolve("OK");
    },
    del(key: string): Promise<number> {
      const existed: boolean = alive(key) !== undefined;
      entries.delete(key);
      return Promise.resolve(existed ? DELETED_COUNT : NOT_FOUND_COUNT);
    },
  };
}

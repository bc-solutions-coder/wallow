/**
 * A fixed-window, per-key request limiter for the ingest route.
 *
 * In-process and per-instance by design. A shared limiter would mean a store,
 * and the thing being protected is an unauthenticated write endpoint on one app
 * server — the budget that matters is that server's, which is exactly what an
 * in-process counter measures. A multi-instance deployment gets N times the
 * limit, which is the right answer: each instance is protecting itself.
 */

/** How the limiter is configured. */
export interface RateLimitOptions {
  /** Requests allowed per key per window. */
  limit: number;
  /** Window length in milliseconds. */
  windowMs: number;
  /**
   * Ceiling on tracked keys.
   *
   * Load-bearing rather than tidy: the key is a client-derived address, so an
   * unbounded map is itself the memory-exhaustion primitive the limiter exists
   * to prevent.
   */
  maxTrackedKeys: number;
}

/** The limiter the ingest handler consults. */
export interface RateLimiter {
  /** Whether this request is allowed. Counts it when it is. */
  allow: (key: string, now: number) => boolean;
}

/** One key's window. */
interface Window {
  count: number;
  resetAt: number;
}

export const DEFAULT_RATE_LIMIT: RateLimitOptions = {
  limit: 60,
  windowMs: 60_000,
  maxTrackedKeys: 10_000,
};

/**
 * Drop expired windows, then — if the map is still at its ceiling — the oldest
 * entry.
 *
 * `Map` iterates in insertion order, so the first key is the least recently
 * ADMITTED one. Evicting it forgives whoever it belonged to, which is the safe
 * direction to fail: a limiter that evicts an active abuser is worse than one
 * that occasionally forgives an idle client.
 */
function makeRoom(windows: Map<string, Window>, now: number, maxTrackedKeys: number): void {
  for (const [key, window] of windows) {
    if (window.resetAt <= now) {
      windows.delete(key);
    }
  }

  while (windows.size >= maxTrackedKeys) {
    const oldest: string | undefined = windows.keys().next().value;
    if (oldest === undefined) {
      return;
    }
    windows.delete(oldest);
  }
}

/** Build a limiter. Each call owns its own state. */
export function createRateLimiter(options: RateLimitOptions = DEFAULT_RATE_LIMIT): RateLimiter {
  const windows = new Map<string, Window>();

  return {
    allow: (key: string, now: number): boolean => {
      const current: Window | undefined = windows.get(key);

      if (current === undefined || current.resetAt <= now) {
        if (windows.size >= options.maxTrackedKeys) {
          makeRoom(windows, now, options.maxTrackedKeys);
        }
        windows.set(key, { count: 1, resetAt: now + options.windowMs });
        return true;
      }

      if (current.count >= options.limit) {
        return false;
      }

      current.count += 1;
      return true;
    },
  };
}

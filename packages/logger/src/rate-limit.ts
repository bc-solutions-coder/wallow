/**
 * Fixed-window request limits held in memory by each limiter instance. Limits and tracked keys
 * are not shared between server replicas.
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
  /**
   * Count an accepted request in the key window and return true, or return false once its
   * allowance is exhausted. Pass now as epoch milliseconds; a missing or expired key starts a new
   * window.
   */
  allow: (key: string, now: number) => boolean;
}

/** One key's window. */
interface Window {
  count: number;
  resetAt: number;
}

/**
 * Default limit of 60 accepted requests per key per 60-second window, with at most 10000 tracked
 * keys.
 */
export const DEFAULT_RATE_LIMIT: RateLimitOptions = {
  limit: 60,
  windowMs: 60_000,
  maxTrackedKeys: 10_000,
};

/**
 * Remove expired windows, then evict the earliest inserted keys until a new key fits. Eviction
 * resets the removed callers allowance.
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

/**
 * Create an independent in-memory fixed-window limiter. Retain the returned instance across
 * requests. Expired windows are removed when capacity is needed, then the earliest inserted keys
 * are evicted; evicted callers start a new allowance. Supply positive limits and durations because
 * options are not validated.
 */
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

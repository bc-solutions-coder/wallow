import { describe, expect, it } from "vitest";

import { createRateLimiter, DEFAULT_RATE_LIMIT, type RateLimiter } from "./rate-limit";

/**
 * The fixed-window limiter guarding an unauthenticated write endpoint.
 *
 * `now` is a parameter rather than a clock read, so window rollover is asserted
 * by arithmetic instead of by waiting.
 */

const START = 1_000_000;

describe("counting a window", () => {
  it("admits up to the limit and refuses past it", () => {
    const limiter: RateLimiter = createRateLimiter({
      limit: 3,
      windowMs: 1000,
      maxTrackedKeys: 10,
    });

    expect([1, 2, 3].map(() => limiter.allow("a", START))).toEqual([true, true, true]);
    expect(limiter.allow("a", START)).toBe(false);
  });

  it("counts each key separately", () => {
    const limiter: RateLimiter = createRateLimiter({
      limit: 1,
      windowMs: 1000,
      maxTrackedKeys: 10,
    });

    expect(limiter.allow("a", START)).toBe(true);
    expect(limiter.allow("b", START)).toBe(true);
    expect(limiter.allow("a", START)).toBe(false);
  });

  it("starts a fresh window once the old one expires", () => {
    const limiter: RateLimiter = createRateLimiter({
      limit: 1,
      windowMs: 1000,
      maxTrackedKeys: 10,
    });

    expect(limiter.allow("a", START)).toBe(true);
    expect(limiter.allow("a", START + 999)).toBe(false);
    expect(limiter.allow("a", START + 1000)).toBe(true);
  });
});

describe("bounding the tracked keys", () => {
  it("keeps admitting new keys past the ceiling", () => {
    // The map is keyed on a client-derived address, so an unbounded one is itself
    // the memory-exhaustion primitive the limiter exists to prevent. Eviction has
    // to keep the route working, not start refusing everyone.
    const limiter: RateLimiter = createRateLimiter({
      limit: 1,
      windowMs: 60_000,
      maxTrackedKeys: 4,
    });

    for (let index = 0; index < 100; index += 1) {
      expect(limiter.allow(`client-${String(index)}`, START)).toBe(true);
    }
  });

  it("drops expired windows before evicting a live one", () => {
    const limiter: RateLimiter = createRateLimiter({ limit: 1, windowMs: 1000, maxTrackedKeys: 3 });

    limiter.allow("old-a", START);
    limiter.allow("old-b", START);
    limiter.allow("live", START + 2000);
    limiter.allow("new", START + 2000);

    // `live` was admitted inside the current window and the two stale keys were
    // reclaimed instead, so it is still being counted.
    expect(limiter.allow("live", START + 2000)).toBe(false);
  });
});

describe("the default window", () => {
  it("is a per-minute budget with a bounded map", () => {
    expect(DEFAULT_RATE_LIMIT).toEqual({ limit: 60, windowMs: 60_000, maxTrackedKeys: 10_000 });
  });
});

import { afterEach, describe, expect, it, vi } from "vitest";

import {
  assertNoNavigationEscape,
  clearNavigationEscapes,
  consumeNavigationEscapes,
  expectNavigationEscape,
  installNavigationEscapeGuard,
  navigationEscapes,
} from "./navigation-escape";

/**
 * Specs for the consuming half of `./navigation-escape` — what a spec asserting a
 * DELIBERATE cross-document hand-off calls instead of registering a second
 * `navigate` listener beside the project guard's.
 *
 * `.tsx` because these need a real document: `globalThis.location` is
 * `[Unforgeable]`, so the only honest way to raise the guard's own event is to
 * navigate for real and let the guard veto it. The guard is installed here rather
 * than in a setup file because this package wires none.
 */
installNavigationEscapeGuard();

afterEach(() => {
  clearNavigationEscapes();
});

/** Hand the browser off for real, and wait until the guard has recorded the veto. */
async function provokeEscape(path: string): Promise<void> {
  const recorded = navigationEscapes().length;

  globalThis.location.assign(path);

  await vi.waitFor(() => {
    expect(navigationEscapes()).toHaveLength(recorded + 1);
  });
}

/** The paths, in arrival order, of everything the guard is currently holding. */
function recordedPaths(escapes: readonly { url: string }[]): string[] {
  return escapes.map((escape) => new URL(escape.url).pathname);
}

describe("consumeNavigationEscapes", () => {
  it("resolves with the escape and leaves the shared record empty", async () => {
    globalThis.location.assign("/consumed");

    const consumed = await consumeNavigationEscapes();

    expect(recordedPaths(consumed)).toEqual(["/consumed"]);
    expect(navigationEscapes()).toHaveLength(0);
    expect(() => {
      assertNoNavigationEscape();
    }).not.toThrow();
  });

  it("rejects when nothing escaped", async () => {
    await expect(consumeNavigationEscapes({ timeout: 250 })).rejects.toThrow(
      /no navigation escaped/i,
    );
  });

  it("resolves with every escape in arrival order", async () => {
    await provokeEscape("/first");
    await provokeEscape("/second");

    expect(recordedPaths(await consumeNavigationEscapes())).toEqual(["/first", "/second"]);
  });
});

describe("expectNavigationEscape", () => {
  it("returns the one escape it consumed", async () => {
    globalThis.location.assign("/handed-off");

    const escape = await expectNavigationEscape();

    expect(new URL(escape.url).pathname).toBe("/handed-off");
    expect(navigationEscapes()).toHaveLength(0);
  });

  it("rejects naming every URL when more than one escaped", async () => {
    await provokeEscape("/first");
    await provokeEscape("/second");

    await expect(expectNavigationEscape()).rejects.toThrow(/\/first[\S\s]*\/second/);
  });

  it("rejects when nothing escaped", async () => {
    await expect(expectNavigationEscape({ timeout: 250 })).rejects.toThrow(
      /no navigation escaped/i,
    );
  });
});

describe("consumption is scoped to what it read", () => {
  it("still fails a test for an escape provoked after a consume", async () => {
    globalThis.location.assign("/consumed");
    await consumeNavigationEscapes();

    await provokeEscape("/late");

    expect(navigationEscapes()).toHaveLength(1);
    expect(() => {
      assertNoNavigationEscape();
    }).toThrow(/\/late/);
  });
});

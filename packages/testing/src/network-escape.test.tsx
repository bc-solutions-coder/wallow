/**
 * Specs for `./network-escape` — the guard that answers any request no harness
 * owns, instead of letting it reach the real network.
 *
 * `.tsx` so these run on the BROWSER project: the guard's passthrough rule is
 * written against `globalThis.location.origin`, which in the node project is a
 * fiction. The file renders nothing — same precedent as `navigation-escape.tsx`,
 * where a real page is the subject rather than a component.
 *
 * `uninstallNetworkEscapeGuard()` in `afterEach` is why this file may patch a
 * global at all: it is the one place that must put the real `fetch` back, so a
 * later case installs onto an unpatched global rather than onto its own wrapper.
 */
import { afterEach, describe, expect, it } from "vitest";

import {
  assertNoNetworkEscape,
  clearNetworkEscapes,
  consumeNetworkEscapes,
  installNetworkEscapeGuard,
  NETWORK_ESCAPE_MESSAGE,
  networkEscapes,
  NO_NETWORK_ESCAPE_MESSAGE,
  uninstallNetworkEscapeGuard,
} from "./network-escape";

const BLOCKED = 503;
const SHORT_TIMEOUT = 250;

afterEach(() => {
  uninstallNetworkEscapeGuard();
  clearNetworkEscapes();
});

describe("installNetworkEscapeGuard", () => {
  it("answers an off-origin request rather than letting it leave, and fails once", async () => {
    installNetworkEscapeGuard();

    // Resolving matters as much as recording: a hanging request fails as a
    // timeout blamed on the assertion that waited, not on the escape.
    const response = await fetch("http://evil.test/steal");

    expect(response.status).toBe(BLOCKED);
    expect(() => {
      assertNoNetworkEscape();
    }).toThrow(new RegExp(`${NETWORK_ESCAPE_MESSAGE}[\\S\\s]*GET[\\S\\s]*evil.test/steal`, "u"));
    expect(() => {
      assertNoNetworkEscape();
    }).not.toThrow();
  });

  it("records the method the request was issued with", async () => {
    installNetworkEscapeGuard();

    await fetch("http://evil.test/steal", { method: "POST" });

    expect(networkEscapes()).toEqual([{ method: "POST", url: "http://evil.test/steal" }]);
  });

  it("passes the Vite and Vitest machinery through untouched", async () => {
    installNetworkEscapeGuard();

    await fetch("/__vitest_api__/ping").catch(() => undefined);
    await fetch("/@id/some-module").catch(() => undefined);

    // Whatever the dev server answers is its own business; what matters is that
    // the guard neither recorded nor intercepted these.
    expect(networkEscapes()).toHaveLength(0);
  });

  it("records once when installed twice", async () => {
    installNetworkEscapeGuard();
    installNetworkEscapeGuard();

    await fetch("http://evil.test/steal");

    expect(networkEscapes()).toHaveLength(1);
  });
});

describe("consumeNetworkEscapes", () => {
  it("drains every entry, leaving nothing for the afterEach to fail on", async () => {
    installNetworkEscapeGuard();
    await fetch("http://evil.test/one");
    await fetch("http://evil.test/two");

    const consumed = await consumeNetworkEscapes();

    expect(consumed.map((escape) => escape.url)).toEqual([
      "http://evil.test/one",
      "http://evil.test/two",
    ]);
    expect(() => {
      assertNoNetworkEscape();
    }).not.toThrow();
  });

  it("rejects when nothing escaped", async () => {
    installNetworkEscapeGuard();

    await expect(consumeNetworkEscapes({ timeout: SHORT_TIMEOUT })).rejects.toThrow(
      new RegExp(NO_NETWORK_ESCAPE_MESSAGE, "u"),
    );
  });
});

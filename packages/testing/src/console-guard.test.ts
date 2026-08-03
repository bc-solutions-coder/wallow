/**
 * Specs for `./console-guard` — the guard a browser project installs in its setup
 * file so a `console.error` React writes fails the test that produced it.
 *
 * These run on the NODE project: the guard wraps `globalThis.console`, which is
 * the same object in either project, and nothing here needs a DOM.
 *
 * Every case restores `console` itself, because the subject IS the shared
 * console: `uninstallConsoleGuard()` puts the real methods back AND releases the
 * idempotence latch, without which a later `installConsoleGuard()` would be a
 * no-op and the case after this one would assert against an unguarded console.
 */
import { afterEach, describe, expect, it, vi } from "vitest";

import {
  assertNoConsoleNoise,
  clearConsoleNoise,
  CONSOLE_NOISE_MESSAGE,
  consoleNoise,
  consumeConsoleNoise,
  expectConsoleError,
  installConsoleGuard,
  NO_CONSOLE_NOISE_MESSAGE,
  uninstallConsoleGuard,
} from "./console-guard";

const SHORT_TIMEOUT = 250;

afterEach(() => {
  uninstallConsoleGuard();
  clearConsoleNoise();
});

describe("installConsoleGuard", () => {
  it("records a console.error and fails once, not on every test behind it", () => {
    installConsoleGuard();

    console.error("boom");

    expect(() => {
      assertNoConsoleNoise();
    }).toThrow(new RegExp(`${CONSOLE_NOISE_MESSAGE}[\\S\\s]*boom`, "u"));
    expect(() => {
      assertNoConsoleNoise();
    }).not.toThrow();
  });

  it("records a console.warn under the warn level", () => {
    installConsoleGuard();

    console.warn("careful");

    expect(consoleNoise()).toEqual([{ level: "warn", message: "careful" }]);
  });

  it("still calls through to the method it wrapped", () => {
    const spy = vi.fn();
    console.error = spy;
    installConsoleGuard();

    console.error("passed", "through");

    expect(spy).toHaveBeenCalledWith("passed", "through");
  });

  it("records once when installed twice", () => {
    installConsoleGuard();
    installConsoleGuard();

    console.error("x");

    expect(consoleNoise()).toHaveLength(1);
  });

  it("formats a non-string argument readably", () => {
    installConsoleGuard();

    console.error(new Error("kaput"));

    expect(consoleNoise()[0]?.message).toContain("kaput");
  });
});

describe("consumeConsoleNoise", () => {
  it("drains every entry, leaving nothing for the afterEach to fail on", async () => {
    installConsoleGuard();
    console.error("first");
    console.warn("second");

    const consumed = await consumeConsoleNoise();

    expect(consumed.map((entry) => entry.message)).toEqual(["first", "second"]);
    expect(() => {
      assertNoConsoleNoise();
    }).not.toThrow();
  });

  it("rejects when the test wrote nothing to console", async () => {
    installConsoleGuard();

    await expect(consumeConsoleNoise({ timeout: SHORT_TIMEOUT })).rejects.toThrow(
      new RegExp(NO_CONSOLE_NOISE_MESSAGE, "u"),
    );
  });
});

describe("expectConsoleError", () => {
  it("returns the matching entry and consumes the rest with it", async () => {
    installConsoleGuard();
    console.warn("surrounding noise");
    console.error("boom: the thing failed");

    const match = await expectConsoleError("boom");

    expect(match.level).toBe("error");
    expect(match.message).toContain("the thing failed");
    expect(consoleNoise()).toHaveLength(0);
  });

  it("rejects when nothing recorded matches", async () => {
    installConsoleGuard();
    console.error("something else entirely");

    await expect(expectConsoleError("boom", { timeout: SHORT_TIMEOUT })).rejects.toThrow(
      /something else entirely/u,
    );
  });
});

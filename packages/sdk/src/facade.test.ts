import { describe, expect, it, vi } from "vitest";

import { createConfiguredOnce, type SdkEnvelope, unwrap } from "./facade";

/**
 * Facade helper module (Wallow-0q2s.7.3). The SDK-owned home of the two pieces
 * of boilerplate both app facades (`getWallowSdk()` / `getWallowAuthSdk()`)
 * hand-rolled:
 *
 *   - `unwrap()` — return `data` on a `{ data }` envelope, THROW the RAW `error`
 *     (unchanged `ProblemDetails`) on a `{ error }` envelope. This preserves
 *     wallow-web's raw-throw semantics; it must NOT wrap the error in a typed
 *     `WallowError` the way `createAuthClient()`'s private unwrap does, because
 *     wallow-web components read the raw shape (`(err as ProblemDetails).detail`).
 *   - `createConfiguredOnce()` — the guarded singleton: run configure + build
 *     once, memoize, hand back the same instance thereafter, and do nothing at
 *     creation time.
 */

describe("unwrap", () => {
  it("returns the data on a { data } envelope", async () => {
    const data = { id: "o1", name: "Acme" };

    const result = await unwrap(Promise.resolve({ data }));

    expect(result).toBe(data);
  });

  it("returns the exact array reference on a list-shaped envelope", async () => {
    const rows = [{ id: "u1" }, { id: "u2" }];

    const result = await unwrap(Promise.resolve({ data: rows }));

    expect(result).toBe(rows);
  });

  it("resolves (does not throw) when error is absent even if data is undefined", async () => {
    const envelope: SdkEnvelope<undefined> = { data: undefined };

    await expect(unwrap(Promise.resolve(envelope))).resolves.toBeUndefined();
  });

  it("throws the RAW ProblemDetails object on a { error } envelope, by identity", async () => {
    const problem = { status: 403, title: "Forbidden", detail: "nope", errorCode: "CSRF_INVALID" };

    // rejects.toBe pins IDENTITY: the exact object is thrown, not a copy and not
    // a wrapping WallowError. This is the divergence the shared helper preserves.
    await expect(unwrap(Promise.resolve({ error: problem }))).rejects.toBe(problem);
  });

  it("does not transform the thrown error into a WallowError-shaped object", async () => {
    const problem = { status: 404, title: "Not Found", detail: "missing" };

    const thrown: unknown = await unwrap(Promise.resolve({ error: problem })).then(
      () => undefined,
      (error: unknown) => error,
    );

    // A WallowError-wrapping unwrap would hand back an Error instance with a
    // `.code`; the raw-throw helper hands back the plain object untouched.
    expect(thrown).toBe(problem);
    expect(thrown).not.toBeInstanceOf(Error);
  });

  it("throws a non-object error verbatim rather than coercing it", async () => {
    await expect(unwrap(Promise.resolve({ error: "boom" }))).rejects.toBe("boom");
  });
});

describe("createConfiguredOnce", () => {
  it("does nothing until the getter is first called", () => {
    const configure = vi.fn();
    const build = vi.fn(() => ({}));

    createConfiguredOnce(configure, build);

    expect(configure).not.toHaveBeenCalled();
    expect(build).not.toHaveBeenCalled();
  });

  it("runs configure exactly once across many getter calls", () => {
    const configure = vi.fn();
    const build = vi.fn(() => ({ facade: true }));
    const get = createConfiguredOnce(configure, build);

    get();
    get();
    get();

    expect(configure).toHaveBeenCalledTimes(1);
  });

  it("builds the facade exactly once and memoizes the same instance", () => {
    const facade = { facade: true };
    const configure = vi.fn();
    const build = vi.fn(() => facade);
    const get = createConfiguredOnce(configure, build);

    const first = get();
    const second = get();

    expect(build).toHaveBeenCalledTimes(1);
    expect(first).toBe(facade);
    expect(second).toBe(first);
  });

  it("runs configure before build on the first call", () => {
    const order: string[] = [];
    const configure = vi.fn(() => {
      order.push("configure");
    });
    const build = vi.fn(() => {
      order.push("build");
      return {};
    });

    createConfiguredOnce(configure, build)();

    expect(order).toEqual(["configure", "build"]);
  });
});

describe("facade helper exports", () => {
  it("keeps exporting unwrap and createConfiguredOnce as plain functions", () => {
    // Both app facades import these from the SDK's browser entry; the surface
    // must stay stable named value exports.
    expect(vi.isMockFunction(unwrap)).toBe(false);
    expect(typeof unwrap).toBe("function");
    expect(typeof createConfiguredOnce).toBe("function");
  });
});

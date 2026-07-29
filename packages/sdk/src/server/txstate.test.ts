import { describe, expect, it } from "vitest";

import { DEFAULT_COOKIE_KEY_ID, type CookiePasswordSet } from "./config";
import { type LoginTx, sealTx, unsealTx } from "./txstate";

const PASSWORD: string = "test-password-at-least-32-chars-long-000";

function makeTx(): LoginTx {
  return {
    state: "state-abc",
    nonce: "nonce-xyz",
    verifier: "verifier-1234567890",
    returnTo: "/dashboard",
  };
}

describe("sealTx / unsealTx", () => {
  it("round-trips a login transaction to an equal object", async () => {
    const tx: LoginTx = {
      state: "state-abc",
      nonce: "nonce-xyz",
      verifier: "verifier-1234567890",
      returnTo: "/dashboard",
    };

    const sealed: string = await sealTx(tx, PASSWORD);
    const result: LoginTx | null = await unsealTx(sealed, PASSWORD);

    expect(result).toEqual(tx);
  });

  it("returns null for a garbage sealed value", async () => {
    const result: LoginTx | null = await unsealTx("garbage", PASSWORD);

    expect(result).toBeNull();
  });
});

/**
 * The login-transaction cookie rotates on the same keys as the session cookie
 * (finding L3). Leaving it on the single-secret path would abort every login
 * that was mid-flight when a rotation deployed: the authorize redirect seals the
 * tx cookie, and the callback a few seconds later has to unseal it.
 */
describe("sealTx / unsealTx — keyed password rotation", () => {
  const V1: string = "v1-cookie-password-of-at-least-32-chars";
  const V2: string = "v2-cookie-password-of-at-least-32-chars";

  const ONLY_V1: CookiePasswordSet = { activeKeyId: "v1", keys: { v1: V1 } };
  const V2_THEN_V1: CookiePasswordSet = { activeKeyId: "v2", keys: { v2: V2, v1: V1 } };
  const ONLY_V2: CookiePasswordSet = { activeKeyId: "v2", keys: { v2: V2 } };

  it("unseals a tx cookie sealed under v1 with a set that still carries v1", async () => {
    const tx: LoginTx = makeTx();

    const sealed: string = await sealTx(tx, ONLY_V1);

    expect(await unsealTx(sealed, V2_THEN_V1)).toEqual(tx);
  });

  it("seals a new tx cookie under the active key", async () => {
    const tx: LoginTx = makeTx();

    const sealed: string = await sealTx(tx, V2_THEN_V1);

    expect(await unsealTx(sealed, ONLY_V2)).toEqual(tx);
    expect(await unsealTx(sealed, ONLY_V1)).toBeNull();
  });

  it("unseals a tx cookie sealed by the OLD bare-string API using the wrapped set", async () => {
    const tx: LoginTx = makeTx();

    const sealed: string = await sealTx(tx, PASSWORD);
    const wrapped: CookiePasswordSet = {
      activeKeyId: DEFAULT_COOKIE_KEY_ID,
      keys: { [DEFAULT_COOKIE_KEY_ID]: PASSWORD },
    };

    expect(await unsealTx(sealed, wrapped)).toEqual(tx);
  });

  it("still round-trips a plain string password, unchanged", async () => {
    const tx: LoginTx = makeTx();

    const sealed: string = await sealTx(tx, PASSWORD);

    expect(await unsealTx(sealed, PASSWORD)).toEqual(tx);
  });
});

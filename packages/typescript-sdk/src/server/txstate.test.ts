import { describe, expect, it } from "vitest";

import { type LoginTx, sealTx, unsealTx } from "./txstate";

const PASSWORD: string = "test-password-at-least-32-chars-long-000";

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

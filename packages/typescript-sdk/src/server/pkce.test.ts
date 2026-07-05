import { describe, expect, it } from "vitest";

import {
  createPkcePair,
  randomUrlSafe,
  sha256Challenge,
  type PkcePair,
} from "./pkce";

const urlSafe: RegExp = /^[A-Za-z0-9_-]+$/;

describe("randomUrlSafe", () => {
  it("returns a URL-safe string with no base64 padding", () => {
    const value: string = randomUrlSafe();

    expect(value).toMatch(urlSafe);
    expect(value).not.toContain("+");
    expect(value).not.toContain("/");
    expect(value).not.toContain("=");
  });

  it("returns distinct values across calls", () => {
    const a: string = randomUrlSafe();
    const b: string = randomUrlSafe();

    expect(a).not.toBe(b);
  });

  it("produces a longer string for more requested bytes", () => {
    const small: string = randomUrlSafe(16);
    const large: string = randomUrlSafe(48);

    expect(large.length).toBeGreaterThan(small.length);
  });
});

describe("sha256Challenge", () => {
  it("is deterministic for the same verifier", async () => {
    const verifier: string = "test-verifier-value";

    const first: string = await sha256Challenge(verifier);
    const second: string = await sha256Challenge(verifier);

    expect(first).toBe(second);
  });

  it("returns a URL-safe challenge that differs from the verifier", async () => {
    const verifier: string = "test-verifier-value";

    const challenge: string = await sha256Challenge(verifier);

    expect(challenge).toMatch(urlSafe);
    expect(challenge).not.toBe(verifier);
  });

  it("produces a known S256 digest for a fixed verifier", async () => {
    // RFC 7636 Appendix B worked example.
    const verifier: string = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

    const challenge: string = await sha256Challenge(verifier);

    expect(challenge).toBe("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
  });
});

describe("createPkcePair", () => {
  it("returns a verifier and its matching S256 challenge", async () => {
    const pair: PkcePair = await createPkcePair();

    expect(pair.verifier).not.toBe(pair.challenge);
    expect(await sha256Challenge(pair.verifier)).toBe(pair.challenge);
  });

  it("returns a verifier of at least the PKCE minimum length", async () => {
    const pair: PkcePair = await createPkcePair();

    expect(pair.verifier.length).toBeGreaterThanOrEqual(43);
  });

  it("returns a distinct pair on each call", async () => {
    const first: PkcePair = await createPkcePair();
    const second: PkcePair = await createPkcePair();

    expect(first.verifier).not.toBe(second.verifier);
    expect(first.challenge).not.toBe(second.challenge);
  });
});

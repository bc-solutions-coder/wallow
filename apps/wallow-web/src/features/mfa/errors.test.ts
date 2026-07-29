/**
 * MFA error surfacing under the unified error contract (Wallow-pu6a.5.3).
 *
 * Before this task the MFA endpoints' raw `{ succeeded: false, error }` body was
 * thrown unchanged, so `problemDetail` read the machine code straight off the
 * thrown object. The SDK's error interceptor now normalizes EVERY failure —
 * RFC 7807 and raw-shape alike — into a `WallowError`, so the code arrives as
 * `error.code` and the human sentence as `error.detail`. These specs pin that
 * migration: the raw shape is no longer consulted, and an unmapped placeholder
 * code never reaches the user.
 */

import { WallowError } from "@bc-solutions-coder/sdk";
import { describe, expect, it } from "vitest";

import { mapMfaError, problemDetail } from "./errors";

const FALLBACK: string = "Could not complete that step.";

/** A WallowError as the SDK's interceptor would build one. */
function wallowError(init: {
  status?: number;
  code: string;
  title?: string;
  detail?: string;
}): WallowError {
  return new WallowError({
    status: init.status ?? 400,
    code: init.code,
    title: init.title ?? "Bad Request",
    detail: init.detail,
  });
}

describe("mapMfaError", () => {
  it("maps a known machine code to friendly copy", () => {
    expect(mapMfaError("invalid_code")).toBe("That verification code is not valid.");
  });

  it("falls back to the raw code for an unmapped one", () => {
    expect(mapMfaError("totp_replay")).toBe("totp_replay");
  });

  it("returns undefined for an absent code so callers can defer", () => {
    expect(mapMfaError(undefined)).toBeUndefined();
    expect(mapMfaError(null)).toBeUndefined();
    expect(mapMfaError("")).toBeUndefined();
  });
});

describe("problemDetail", () => {
  it("prefers the WallowError detail when the endpoint produced problem details", () => {
    const error = wallowError({ code: "INVALID_PASSWORD", detail: "That password is incorrect." });

    expect(problemDetail(error, FALLBACK)).toBe("That password is incorrect.");
  });

  it("maps the WallowError code when there is no detail", () => {
    // The MFA controllers emit no problem details at all, so the interceptor
    // produces a WallowError whose ONLY useful member is the code.
    const error = wallowError({ code: "invalid_code" });

    expect(problemDetail(error, FALLBACK)).toBe("That verification code is not valid.");
  });

  it("surfaces an unmapped MFA code rather than the fallback", () => {
    const error = wallowError({ code: "totp_replay" });

    expect(problemDetail(error, FALLBACK)).toBe("totp_replay");
  });

  it("falls back rather than showing the placeholder UNKNOWN code", () => {
    // A failure the API named no code for normalizes to UNKNOWN; that is an
    // internal placeholder, never user-facing copy.
    const error = wallowError({ code: "UNKNOWN", status: 500, title: "Unknown error" });

    expect(problemDetail(error, FALLBACK)).toBe(FALLBACK);
  });

  it("ignores a raw { error } body — nothing throws that shape any more", () => {
    // The pre-interceptor contract. Reading it would keep the deleted raw-throw
    // path alive and let an unbranded object dictate user-facing copy.
    expect(problemDetail({ succeeded: false, error: "invalid_code" }, FALLBACK)).toBe(FALLBACK);
  });

  it("ignores a plain object that merely looks like problem details", () => {
    expect(problemDetail({ detail: "spoofed" }, FALLBACK)).toBe(FALLBACK);
  });

  it("falls back for a non-error value", () => {
    expect(problemDetail("boom", FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(null, FALLBACK)).toBe(FALLBACK);
    expect(problemDetail(undefined, FALLBACK)).toBe(FALLBACK);
  });
});

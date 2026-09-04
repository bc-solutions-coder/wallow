/**
 * MFA error surfacing under the unified error contract.
 *
 * The SDK's error interceptor normalizes EVERY failure — RFC 7807 and raw-shape
 * alike — into an `ApiFailure`. A raw `{ succeeded, error }` body parses under
 * the OAuth grammar (code `OAuth.<Token>`, title = the raw token); a problem
 * body keeps its own code. Neither the raw body nor the parser's
 * unrecognized-response placeholder ever reaches the user.
 */

import { ApiFailure, ClientErrorCode } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";

import { mapMfaError, problemDetail } from "./errors";

const FALLBACK: string = "Could not complete that step.";

/** An ApiFailure as the SDK's interceptor would build one. */
function apiFailure(init: {
  status?: number;
  code: string;
  title?: string;
  detail?: string;
}): ApiFailure {
  return new ApiFailure({
    status: init.status ?? 400,
    code: init.code,
    title: init.title ?? "Bad Request",
    detail: init.detail,
  });
}

/** The failure the parser builds from the MFA controllers' `{ error: token }` body. */
function rawTokenFailure(token: string, pascal: string): ApiFailure {
  return apiFailure({ code: `OAuth.${pascal}`, title: token });
}

describe("mapMfaError", () => {
  it("maps a known machine token to friendly copy", () => {
    expect(mapMfaError("invalid_code")).toBe("That verification code is not valid.");
  });

  it("falls back to the raw token for an unmapped one", () => {
    expect(mapMfaError("totp_replay")).toBe("totp_replay");
  });

  it("returns undefined for an absent token so callers can defer", () => {
    expect(mapMfaError(undefined)).toBeUndefined();
    expect(mapMfaError(null)).toBeUndefined();
    expect(mapMfaError("")).toBeUndefined();
  });
});

describe("problemDetail", () => {
  it("prefers the ApiFailure detail when the endpoint produced problem details", () => {
    const error = apiFailure({
      code: "Mfa.InvalidPassword",
      detail: "That password is incorrect.",
    });

    expect(problemDetail(error, FALLBACK)).toBe("That password is incorrect.");
  });

  it("maps the raw token a { succeeded, error } body parsed into when there is no detail", () => {
    // The MFA controllers emit no problem details at all, so the parser's
    // OAuth grammar leaves the raw token as the title.
    const error = rawTokenFailure("invalid_code", "InvalidCode");

    expect(problemDetail(error, FALLBACK)).toBe("That verification code is not valid.");
  });

  it("surfaces an unmapped MFA token rather than the fallback", () => {
    const error = rawTokenFailure("totp_replay", "TotpReplay");

    expect(problemDetail(error, FALLBACK)).toBe("totp_replay");
  });

  it("resolves a problem without detail through the package, never showing its code", () => {
    const error = apiFailure({ code: "Mfa.TotpReplay", status: 403 });

    expect(problemDetail(error, FALLBACK)).toBe("You don't have permission to do that.");
  });

  it("resolves a transport fault to the package's sentence rather than its code", () => {
    // api-errors sets no `detail` on a request that never landed.
    const error = apiFailure({ status: 503, code: ClientErrorCode.TRANSPORT_NETWORK_ERROR });

    expect(problemDetail(error, FALLBACK)).toBe(
      "Unable to reach the server. Check your connection and try again.",
    );
  });

  it("falls back rather than showing the unrecognized-response placeholder", () => {
    const error = apiFailure({
      code: ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE,
      status: 500,
      title: "Unrecognized response",
    });

    expect(problemDetail(error, FALLBACK)).toBe(FALLBACK);
  });

  it("ignores a raw { error } body — nothing throws that shape any more", () => {
    // An unbranded object must never dictate user-facing copy.
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

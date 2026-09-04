import { failureFromResponse, resolveFailureMessage } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";

import { failureMessages } from "./failure-messages";

/**
 * The registry is keyed on the codes the parser actually produces for the MFA
 * controllers' raw `{ succeeded: false, error }` bodies, so each case goes
 * through `failureFromResponse` rather than hand-building the failure — a key
 * that drifted from the OAuth grammar would pass a hand-built spec and miss in
 * the app.
 */
function rawBodyFailure(token: string, status: number) {
  const body = JSON.stringify({ succeeded: false, error: token });
  return failureFromResponse(new Response(body, { status }), body);
}

describe("failureMessages", () => {
  it.each([
    ["no_auth_session", 401, "Your session has expired. Please sign in again."],
    ["invalid_password", 400, "That password is incorrect."],
    ["invalid_code", 400, "That verification code is not valid."],
  ])("resolves the raw MFA token %s to the registry's sentence", (token, status, sentence) => {
    const failure = rawBodyFailure(token, status);

    expect(resolveFailureMessage(failure, { registry: failureMessages })).toBe(sentence);
  });

  it("leaves a token it has no sentence for to the package and the call site", () => {
    const failure = rawBodyFailure("update_failed", 400);

    expect(
      resolveFailureMessage(failure, { registry: failureMessages, fallback: "Try again." }),
    ).toBe("Try again.");
  });
});

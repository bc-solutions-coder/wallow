import { failureFromResponse, resolveFailureMessage } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";

import { failureMessages } from "./failure-messages";

/**
 * Each case goes through `failureFromResponse` on the RFC 7807 body the MFA
 * endpoints write, rather than hand-building the failure — a key that drifted
 * from the parsed `code` would pass a hand-built spec and miss in the app. The
 * catalog's `detail` rides along so the spec proves the registry WINS over it.
 */
function problemFailure(code: string, status: number) {
  const body = JSON.stringify({
    type: "about:blank",
    title: "Unknown error",
    status,
    code,
    detail: "The catalog's own sentence.",
  });
  return failureFromResponse(new Response(body, { status }), body);
}

describe("failureMessages", () => {
  it.each([
    ["Mfa.SessionMissing", 401, "Your session has expired. Please sign in again."],
    ["Mfa.PasswordInvalid", 400, "That password is incorrect."],
    ["Mfa.CodeInvalid", 400, "That verification code is not valid."],
  ])("resolves %s to the registry's sentence ahead of the detail", (code, status, sentence) => {
    const failure = problemFailure(code, status);

    expect(resolveFailureMessage(failure, { registry: failureMessages })).toBe(sentence);
  });

  it("leaves a code it has no sentence for to the package and the call site", () => {
    const body = JSON.stringify({
      type: "about:blank",
      title: "Bad Request",
      status: 400,
      code: "Mfa.SomeFutureCode",
    });
    const failure = failureFromResponse(new Response(body, { status: 400 }), body);

    expect(
      resolveFailureMessage(failure, { registry: failureMessages, fallback: "Try again." }),
    ).toBe("Try again.");
  });
});

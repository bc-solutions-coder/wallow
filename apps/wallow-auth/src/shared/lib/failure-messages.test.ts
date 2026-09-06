import { ApiFailure, resolveFailureMessage } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";
import { failureMessages } from "./failure-messages";

const BAD_REQUEST = 400;

function problem(code: string, detail: string): ApiFailure {
  return new ApiFailure({ status: BAD_REQUEST, code, title: code, detail });
}

describe("failureMessages", () => {
  it.each([
    ["Auth.EmailTaken", "An account with this email already exists. Please sign in instead."],
    [
      "Mfa.EnrollmentTokenInvalid",
      "This enrollment link has expired. Please start setup again from your account settings.",
    ],
  ])("overrides the catalog detail for %s", (code, expected) => {
    const message = resolveFailureMessage(problem(code, "catalog detail"), {
      registry: failureMessages,
    });

    expect(message).toBe(expected);
  });

  it("leaves the token codes to the screens, which each name their own link", () => {
    const message = resolveFailureMessage(problem("Auth.TokenInvalid", "The token is invalid."), {
      registry: failureMessages,
    });

    expect(message).toBe("The token is invalid.");
  });

  it("lets the catalog detail through for a code the app has no sentence for", () => {
    const message = resolveFailureMessage(problem("Auth.LockedOut", "This account is locked."), {
      registry: failureMessages,
    });

    expect(message).toBe("This account is locked.");
  });
});

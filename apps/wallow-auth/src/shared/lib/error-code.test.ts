import { describe, expect, it } from "vitest";

import { readErrorCode, readMember } from "./error-code";

/**
 * The structural readers every screen narrows a rejection with. Four features
 * carried byte-identical copies, which `wallow/zone-dag` made unavoidable — a
 * feature cannot import a sibling feature, so `features/login/auth-result.ts`'s
 * export was reachable only from inside `features/login/`.
 *
 * Node project: pure narrowing, no DOM.
 */

describe("readMember", () => {
  it("reads a member off an object", () => {
    expect(readMember({ code: "email_taken" }, "code")).toBe("email_taken");
  });

  it("reads a member holding a falsy value rather than reporting it absent", () => {
    // `succeeded: false` is the whole point of these bodies — an `in` check
    // rather than a truthiness one is what keeps a real `false` distinguishable
    // from a missing member.
    expect(readMember({ succeeded: false }, "succeeded")).toBe(false);
  });

  it("returns undefined for a non-object", () => {
    // A network-level rejection is a bare Error or a string, so narrowing is
    // structural: there is no class to instanceof against.
    expect(readMember("boom", "code")).toBeUndefined();
    expect(readMember(null, "code")).toBeUndefined();
    expect(readMember(undefined, "code")).toBeUndefined();
  });

  it("returns undefined for an absent member", () => {
    expect(readMember({}, "code")).toBeUndefined();
  });
});

describe("readErrorCode", () => {
  it("returns the API's machine token when there is one", () => {
    expect(readErrorCode({ code: "invalid_client_id" })).toBe("invalid_client_id");
  });

  it("ignores a code that is not a string", () => {
    // The token is compared against a constant, never rendered, so a non-string
    // has to read as absent rather than stringify into a screen's copy.
    expect(readErrorCode({ code: 400 })).toBeUndefined();
    expect(readErrorCode({ code: { nested: "email_taken" } })).toBeUndefined();
  });

  it("reads no code off a rejection that never reached the server", () => {
    expect(readErrorCode(new Error("network"))).toBeUndefined();
  });
});

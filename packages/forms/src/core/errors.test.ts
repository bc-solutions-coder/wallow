import { describe, expect, it } from "vitest";

import { firstErrorMessage } from "./errors";

// Acceptance-criteria guard for Wallow-ov6w.2.1 (core/errors.ts, field error
// normalization). Pure logic with no rendering, so this spec runs in the node
// vitest project (.test.ts, not .test.tsx).
//
// A TanStack field's `state.meta.errors` is `unknown[]` because its entries
// come from different producers: function validators push plain strings,
// standard-schema (zod) validators push `{ message }` issue objects, and server
// errors arrive as plain strings through `form.setErrorMap({ onServer: ... })`.
// Every catalog field renders whichever landed first, so both shapes have to
// normalize to the same string here — and anything else must be swallowed
// rather than rendered as `[object Object]`.

describe("firstErrorMessage", () => {
  it("returns undefined for no errors", () => {
    expect(firstErrorMessage([])).toBeUndefined();
  });

  it("returns a string error as-is (function and server validators)", () => {
    expect(firstErrorMessage(["Email is required"])).toBe("Email is required");
  });

  it("unwraps a standard-schema issue's message (zod validators)", () => {
    expect(firstErrorMessage([{ message: "This field is required" }])).toBe(
      "This field is required",
    );
  });

  it("reports only the first error when a field has several", () => {
    // Fields show one message; validators (and setErrorMap's flattened server
    // strings) routinely leave more than one behind.
    expect(firstErrorMessage(["Email is required", "Email is invalid"])).toBe("Email is required");
  });

  it("returns undefined for unrecognizable shapes", () => {
    expect(firstErrorMessage([42])).toBeUndefined();
    expect(firstErrorMessage([null])).toBeUndefined();
    expect(firstErrorMessage([undefined])).toBeUndefined();
  });

  it("returns undefined for an object whose message is not a string", () => {
    expect(firstErrorMessage([{ message: 42 }])).toBeUndefined();
    expect(firstErrorMessage([{}])).toBeUndefined();
  });
});

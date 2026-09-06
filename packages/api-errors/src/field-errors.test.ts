import { describe, expect, it } from "vitest";

import { ApiFailure } from "./failure";
import { splitFieldErrors } from "./field-errors";

/**
 * Distributing a problem's field errors across the fields a form actually has:
 * exact keys, PascalCase keys, dotted nested keys, and what happens to the rest.
 */

const BAD_REQUEST: number = 400;

function validationFailure(errors?: Record<string, readonly string[]>): ApiFailure {
  return new ApiFailure({
    status: BAD_REQUEST,
    code: "Validation.Failed",
    title: "The request is invalid.",
    fieldErrors: errors,
  });
}

describe("splitFieldErrors", () => {
  it("matches a known field by its exact key", () => {
    expect(splitFieldErrors(validationFailure({ name: ["Required"] }), ["name"])).toEqual({
      fieldErrors: { name: ["Required"] },
      unmatched: [],
    });
  });

  it("matches a PascalCase key to its camelCase field", () => {
    expect(
      splitFieldErrors(validationFailure({ DisplayName: ["Too long"] }), ["displayName"]),
    ).toEqual({
      fieldErrors: { displayName: ["Too long"] },
      unmatched: [],
    });
  });

  it("folds a dotted key onto a flattened field", () => {
    expect(
      splitFieldErrors(validationFailure({ "branding.displayName": ["Too long"] }), [
        "brandingDisplayName",
      ]),
    ).toEqual({
      fieldErrors: { brandingDisplayName: ["Too long"] },
      unmatched: [],
    });
  });

  it("folds a dotted PascalCase key", () => {
    expect(
      splitFieldErrors(validationFailure({ "Branding.DisplayName": ["Too long"] }), [
        "brandingDisplayName",
      ]),
    ).toEqual({
      fieldErrors: { brandingDisplayName: ["Too long"] },
      unmatched: [],
    });
  });

  it("prefers the exact key over the folded one", () => {
    expect(
      splitFieldErrors(validationFailure({ "branding.displayName": ["x"] }), [
        "branding.displayName",
        "brandingDisplayName",
      ]),
    ).toEqual({
      fieldErrors: { "branding.displayName": ["x"] },
      unmatched: [],
    });
  });

  it("collects the messages of unknown keys in order", () => {
    expect(
      splitFieldErrors(
        validationFailure({ name: ["Required"], "": ["Body is empty"], Other: ["A", "B"] }),
        ["name"],
      ),
    ).toEqual({
      fieldErrors: { name: ["Required"] },
      unmatched: ["Body is empty", "A", "B"],
    });
  });

  it("returns nothing for a failure without field errors", () => {
    expect(splitFieldErrors(validationFailure(), ["name"])).toEqual({
      fieldErrors: {},
      unmatched: [],
    });
  });
});

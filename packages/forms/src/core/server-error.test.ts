import { WallowError } from "@bc-solutions-coder/sdk";
import { describe, expect, it } from "vitest";

import { splitServerError } from "./server-error";

/*
 * The failure-splitting contract, in the node project (pure logic, no DOM).
 *
 * The real `WallowError` is constructed here rather than a look-alike object:
 * `isWallowError` is a brand check on a global symbol the class sets in its
 * constructor (packages/sdk/src/errors.ts), so a hand-rolled duck type would be
 * rejected by the implementation and the spec would prove nothing.
 *
 * What each case pins:
 *
 *   1. The PascalCase -> camelCase fold. The API's property names come from
 *      FluentValidation/`ValidationProblemDetails` ("Name"), the form's values
 *      are camelCase ("name"), and nothing renders next to an input until the
 *      two are reconciled.
 *   2. Unmatched names are ROUTED, not dropped. A message keyed by a property
 *      the form does not hold (a computed or nested one) would otherwise
 *      disappear, leaving a form that failed with no visible reason.
 *   3. A non-validation `WallowError` contributes its RFC 7807 `detail`.
 *   4./5. Anything else contributes the caller's fallback, except that a plain
 *      `Error` carrying a message contributes that message.
 */

/** The camelCase names a form built from `{ name, email }` values would report. */
const KNOWN_FIELDS: readonly string[] = ["name", "email"];

const FALLBACK = "Something went wrong.";

describe("splitServerError", () => {
  it("maps matching field errors, folding the API's PascalCase onto camelCase names", () => {
    const error = new WallowError({
      status: 400,
      code: "VALIDATION_ERROR",
      title: "Validation failed",
      fieldErrors: { Name: ["'Name' must not be empty."] },
    });

    const result = splitServerError(error, KNOWN_FIELDS, FALLBACK);

    expect(result.fieldErrors).toEqual({ name: ["'Name' must not be empty."] });
    // Everything landed on a field, so there is nothing left for the banner.
    expect(result.formError).toBeNull();
  });

  it("routes unmatched field names to the form-level error instead of dropping them", () => {
    const error = new WallowError({
      status: 400,
      code: "VALIDATION_ERROR",
      title: "Validation failed",
      fieldErrors: { Surprise: ["Nope."] },
    });

    const result = splitServerError(error, KNOWN_FIELDS, FALLBACK);

    expect(result.fieldErrors).toEqual({});
    expect(result.formError).toBe("Nope.");
  });

  it("uses the RFC 7807 detail for a WallowError carrying no field errors", () => {
    // The 409-conflict shape: a real, specific reason that is not about one
    // field, so it belongs in the banner rather than being replaced by the
    // generic fallback.
    const error = new WallowError({
      status: 409,
      code: "CONFLICT",
      title: "Conflict",
      detail: "Name taken.",
    });

    const result = splitServerError(error, KNOWN_FIELDS, FALLBACK);

    expect(result.fieldErrors).toEqual({});
    expect(result.formError).toBe("Name taken.");
  });

  it("falls back for an error that carries no message at all", () => {
    const result = splitServerError(new Error(""), KNOWN_FIELDS, FALLBACK);

    expect(result.fieldErrors).toEqual({});
    expect(result.formError).toBe(FALLBACK);
  });

  it("uses a non-Wallow error's own message when it has one", () => {
    const result = splitServerError(new Error("The network dropped."), KNOWN_FIELDS, FALLBACK);

    expect(result.fieldErrors).toEqual({});
    expect(result.formError).toBe("The network dropped.");
  });
});

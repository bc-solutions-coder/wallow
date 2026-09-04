import { ApiFailure, ClientErrorCode } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";

import { splitServerError, splitSubmitFailure } from "./server-error";

/*
 * The failure-splitting contract, in the node project (pure logic, no DOM).
 *
 * The real `ApiFailure` is constructed because `isApiFailure` is a brand check
 * the class sets in its constructor; a duck type would prove nothing. The
 * `splitSubmitFailure` cases pin the current split: matched keys land on
 * fields, an unmatched key or no field errors leaves the failure for the
 * banner, and a thrown `Error` is classified as a transport failure. The
 * deprecated `splitServerError` cases pin its unchanged behaviour until it goes.
 */

/** The camelCase names a form built from `{ name, email }` values would report. */
const KNOWN_FIELDS: readonly string[] = ["name", "email"];

const FALLBACK = "Something went wrong.";

describe("splitSubmitFailure", () => {
  it("keeps the failure for the banner when a matched field carries no message", () => {
    const error = new ApiFailure({
      status: 400,
      code: "Validation.Failed",
      title: "Bad Request",
      fieldErrors: { Name: [] },
    });

    const split = splitSubmitFailure(error, KNOWN_FIELDS);

    expect(split.fieldErrors).toEqual({ name: [] });
    expect(split.bannerFailure).toBe(error);
  });

  it("lands matching field errors on the form's names and leaves no banner", () => {
    const error = new ApiFailure({
      status: 400,
      code: "Validation.Failed",
      title: "Validation failed",
      detail: "One or more validation errors occurred.",
      fieldErrors: { Name: ["'Name' must not be empty."], "branding.displayName": ["Reserved."] },
    });

    const result = splitSubmitFailure(error, ["name", "brandingDisplayName"]);

    expect(result.fieldErrors).toEqual({
      name: ["'Name' must not be empty."],
      brandingDisplayName: ["Reserved."],
    });
    // Everything landed on a field, so a banner would only repeat the inputs.
    expect(result.bannerFailure).toBeNull();
  });

  it("keeps the failure for the banner when a message matched no field", () => {
    const error = new ApiFailure({
      status: 400,
      code: "Validation.Failed",
      title: "Validation failed",
      fieldErrors: { Name: ["'Name' must not be empty."], Surprise: ["Nope."] },
    });

    const result = splitSubmitFailure(error, KNOWN_FIELDS);

    // The matched half still reaches its field; the unmatched half is not
    // joined into a string — the banner resolves the failure's own sentence.
    expect(result.fieldErrors).toEqual({ name: ["'Name' must not be empty."] });
    expect(result.bannerFailure).toBe(error);
  });

  it("hands a failure without field errors to the banner unchanged", () => {
    const error = new ApiFailure({ status: 409, code: "Orders.Closed", title: "Conflict" });

    const result = splitSubmitFailure(error, KNOWN_FIELDS);

    expect(result.fieldErrors).toEqual({});
    expect(result.bannerFailure).toBe(error);
  });

  it("classifies a thrown Error as a transport failure instead of echoing it", () => {
    const result = splitSubmitFailure(new TypeError("Failed to fetch"), KNOWN_FIELDS);

    expect(result.fieldErrors).toEqual({});
    expect(result.bannerFailure?.code).toBe(ClientErrorCode.TRANSPORT_NETWORK_ERROR);
    // The message never carries the transport text; the resolver shows the
    // shipped network sentence for the code instead.
    expect(result.bannerFailure?.detail).toBeUndefined();
  });
});

describe("splitServerError (deprecated)", () => {
  it("maps matching field errors, folding the API's PascalCase onto camelCase names", () => {
    const error = new ApiFailure({
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

  it("folds a nested wire path onto the flattened field a stepper holds it as", () => {
    const error = new ApiFailure({
      status: 400,
      code: "VALIDATION_ERROR",
      title: "Validation failed",
      fieldErrors: { "branding.displayName": ["'Wallow' is reserved for the platform itself."] },
    });

    const result = splitServerError(error, ["name", "brandingDisplayName"], FALLBACK);

    expect(result.fieldErrors).toEqual({
      brandingDisplayName: ["'Wallow' is reserved for the platform itself."],
    });
    expect(result.formError).toBeNull();
  });

  it("routes unmatched field names to the form-level error instead of dropping them", () => {
    const error = new ApiFailure({
      status: 400,
      code: "VALIDATION_ERROR",
      title: "Validation failed",
      fieldErrors: { Surprise: ["Nope."] },
    });

    const result = splitServerError(error, KNOWN_FIELDS, FALLBACK);

    expect(result.fieldErrors).toEqual({});
    expect(result.formError).toBe("Nope.");
  });

  it("uses the RFC 7807 detail for a ApiFailure carrying no field errors", () => {
    // The 409-conflict shape: a real, specific reason that is not about one
    // field, so it belongs in the banner rather than being replaced by the
    // generic fallback.
    const error = new ApiFailure({
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

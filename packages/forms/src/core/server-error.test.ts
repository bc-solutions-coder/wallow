import { ApiFailure, ClientErrorCode } from "@bc-solutions-coder/api-errors";
import { describe, expect, it } from "vitest";

import { splitSubmitFailure } from "./server-error";

/*
 * The field split preserves unmatched messages and the original branded
 * failure. Unclassified exceptions become transport failures.
 */

/** Uses real branded failures to cover the field split and transport classification. */
const KNOWN_FIELDS: readonly string[] = ["name", "email"];

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
    expect(result.unmatched).toEqual([]);
  });

  it("keeps the failure for the banner when a message matched no field", () => {
    const error = new ApiFailure({
      status: 400,
      code: "Validation.Failed",
      title: "Validation failed",
      fieldErrors: {
        Name: ["'Name' must not be empty."],
        Surprise: ["Nope.", "Another rule failed."],
        Captcha: ["Try the captcha again."],
      },
    });

    const result = splitSubmitFailure(error, KNOWN_FIELDS);

    expect(result.fieldErrors).toEqual({ name: ["'Name' must not be empty."] });
    expect(result.unmatched).toEqual(["Nope.", "Another rule failed.", "Try the captcha again."]);
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

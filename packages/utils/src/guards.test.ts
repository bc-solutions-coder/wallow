import { describe, expect, it } from "vitest";

import { asString, scalarToString } from "./guards";

/**
 * The two narrowings, and the one difference between them: `scalarToString`
 * re-stringifies a number or boolean where `asString` drops it.
 *
 * That difference is the whole reason both exist. A router hands `?error=true`
 * over as the boolean `true`, and a caller comparing it to a literal token needs
 * the text the URL said.
 */

/** Values that are neither a string nor a JSON scalar. */
const NON_SCALARS: readonly unknown[] = [
  undefined,
  null,
  [1, 2],
  ["true"],
  { value: "true" },
  () => "true",
];

describe("asString", () => {
  it("passes a string through", () => {
    expect(asString("invalid_credentials")).toBe("invalid_credentials");
  });

  it("passes the empty string through", () => {
    // Absent and present-but-empty are different facts; only the caller can say
    // whether an empty value is meaningful.
    expect(asString("")).toBe("");
  });

  it.each([true, false, 0, 1, Number.NaN])("reads %o as absent", (value) => {
    expect(asString(value)).toBeUndefined();
  });

  it.each(NON_SCALARS)("reads %o as absent", (value) => {
    expect(asString(value)).toBeUndefined();
  });
});

describe("scalarToString", () => {
  it("passes a string through", () => {
    expect(scalarToString("access_denied")).toBe("access_denied");
  });

  it.each([
    [true, "true"],
    [false, "false"],
    [1, "1"],
    [0, "0"],
    [-2.5, "-2.5"],
  ])("re-stringifies the scalar %o the URL carried", (value, expected) => {
    expect(scalarToString(value)).toBe(expected);
  });

  it.each(NON_SCALARS)("reads %o as absent rather than throwing", (value) => {
    expect(scalarToString(value)).toBeUndefined();
  });
});

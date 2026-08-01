import { describe, expect, it } from "vitest";

import { formatLongDate } from "./format";

/**
 * The rendered wording, and that the three input forms agree.
 *
 * Every `Date` here is built from LOCAL components. `toLocaleDateString` renders
 * in the host's timezone, so a UTC instant asserted against a literal would read
 * as the previous day west of Greenwich and pass only on the machines that wrote
 * it.
 */

/** 5 January 2026, local midnight. */
const JANUARY_5 = new Date(2026, 0, 5);

describe("formatLongDate", () => {
  it("renders the month in full, unpadded day, four-digit year", () => {
    expect(formatLongDate(JANUARY_5)).toBe("January 5, 2026");
  });

  it("orders the parts the way en-US does, whatever the host says", () => {
    // de-DE would render `5. Januar 2026` for the same instant; the month-first
    // order and the comma are what pin the locale to a literal.
    expect(formatLongDate(new Date(2026, 11, 25))).toBe("December 25, 2026");
  });

  it("does not pad a single-digit day", () => {
    expect(formatLongDate(new Date(2026, 8, 1))).toBe("September 1, 2026");
  });

  it("reads epoch millis as the same instant a Date carries", () => {
    expect(formatLongDate(JANUARY_5.getTime())).toBe(formatLongDate(JANUARY_5));
  });

  it("reads the ISO string an API payload carries", () => {
    expect(formatLongDate(JANUARY_5.toISOString())).toBe(formatLongDate(JANUARY_5));
  });

  it("surfaces an unparseable value instead of hiding it", () => {
    expect(formatLongDate("not a date")).toBe("Invalid Date");
  });
});

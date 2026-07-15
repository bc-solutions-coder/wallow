import { describe, expect, it } from "vitest";

import { isSafeMethod } from "./csrf";

describe("isSafeMethod", () => {
  it("treats RFC 9110 safe methods as safe, case-insensitively", () => {
    expect(isSafeMethod("GET")).toBe(true);
    expect(isSafeMethod("head")).toBe(true);
    expect(isSafeMethod("Options")).toBe(true);
  });

  it("treats state-changing methods as unsafe", () => {
    expect(isSafeMethod("POST")).toBe(false);
    expect(isSafeMethod("PUT")).toBe(false);
    expect(isSafeMethod("PATCH")).toBe(false);
    expect(isSafeMethod("DELETE")).toBe(false);
  });
});

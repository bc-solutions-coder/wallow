import { describe, expect, it } from "vitest";

import { fieldErrorTestId, fieldTestId } from "./test-id";

// Acceptance-criteria guard for Wallow-ov6w.2.1 (core/test-id.ts, testid
// derivation). Pure logic with no rendering, so this spec runs in the node
// vitest project (.test.ts, not .test.tsx).
//
// The derivation is what lets a migrated form keep its Playwright selectors
// byte-identical: the suites already select `inquiry-name` and
// `inquiry-project-type-error`, built by hand today from a bare `inquiry`
// prefix. These cases pin the two halves of that shape — the prefix passes
// through untouched (it is authored kebab-case already) and the camelCase
// TanStack field name becomes kebab-case.

describe("fieldTestId", () => {
  it("joins prefix and field name", () => {
    expect(fieldTestId("inquiry", "name")).toBe("inquiry-name");
  });

  it("kebab-cases camelCase field names", () => {
    expect(fieldTestId("inquiry", "projectType")).toBe("inquiry-project-type");
    expect(fieldTestId("inquiry", "budgetRange")).toBe("inquiry-budget-range");
  });

  it("uses a multi-word prefix verbatim", () => {
    // Auth forms prefix with the page slug, which is already kebab-case: the
    // derivation must not re-process it.
    expect(fieldTestId("forgot-password", "email")).toBe("forgot-password-email");
  });
});

describe("fieldErrorTestId", () => {
  it("appends -error to the control testid", () => {
    expect(fieldErrorTestId("inquiry", "projectType")).toBe("inquiry-project-type-error");
  });

  it("stays derived from the control testid", () => {
    // The pair must never drift: a field's message id is always its control id
    // plus the suffix, whatever the control id turns out to be.
    expect(fieldErrorTestId("inquiry", "budgetRange")).toBe(
      `${fieldTestId("inquiry", "budgetRange")}-error`,
    );
  });
});

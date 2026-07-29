/**
 * Testid derivation for the repo's `{page}-{element}` kebab-case convention
 * (.claude/rules/E2E.md), which every Playwright selector goes through.
 *
 * Testids are DERIVED, not hand-written: a form's `testIdPrefix` of `"inquiry"`
 * plus the TanStack field name `"projectType"` produces `"inquiry-project-type"`
 * for the control and `"inquiry-project-type-error"` for its message, which is
 * what the existing suites already select. A field's explicit `testId` prop
 * overrides the derivation so a migrated form keeps its E2E ids byte-identical.
 *
 * Layer 0 of the package: `src/core/` imports nothing from `src/fields/` or
 * `src/form/`.
 */

/** `projectType` -> `project-type`. */
function kebab(fieldName: string): string {
  return fieldName.replaceAll(/[A-Z]/gu, (letter) => `-${letter.toLowerCase()}`);
}

/**
 * The control testid for a field: the form's prefix (used verbatim — it is
 * already kebab-case) joined to the kebab-cased field name.
 */
export function fieldTestId(prefix: string, fieldName: string): string {
  return `${prefix}-${kebab(fieldName)}`;
}

/** The testid of a field's error message: its control testid plus `-error`. */
export function fieldErrorTestId(prefix: string, fieldName: string): string {
  return `${fieldTestId(prefix, fieldName)}-error`;
}

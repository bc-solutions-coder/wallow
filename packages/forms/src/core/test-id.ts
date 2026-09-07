/**
 * Derive control and error test IDs from the form prefix and field name.
 */

/** `projectType` -> `project-type`. */
function kebab(fieldName: string): string {
  return fieldName.replaceAll(/[A-Z]/gu, (letter) => `-${letter.toLowerCase()}`);
}

/**
 * Join the prefix, unchanged, to the field name with each uppercase letter converted to a hyphen
 * and lowercase letter. For example, inquiry and projectType produce inquiry-project-type.
 */
export function fieldTestId(prefix: string, fieldName: string): string {
  return `${prefix}-${kebab(fieldName)}`;
}

/** The testid of a field's error message: its control testid plus `-error`. */
export function fieldErrorTestId(prefix: string, fieldName: string): string {
  return `${fieldTestId(prefix, fieldName)}-error`;
}

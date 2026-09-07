/**
 * Narrow unknown values to strings without requiring a browser, Node, or another package.
 */

/** `value` when it is a string, `undefined` for anything else. */
export function asString(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

/**
 * Return strings unchanged and convert numbers or booleans with String. Other values, including
 * null, objects, and arrays, return undefined. Useful for router search values that were parsed as
 * JSON scalars; this is not validation of an allowed token.
 */
export function scalarToString(value: unknown): string | undefined {
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  return asString(value);
}

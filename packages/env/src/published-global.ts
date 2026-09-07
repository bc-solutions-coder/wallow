/**
 * Serialize deployment values as inline browser script source and read them back from a supplied
 * scope. The server renders text without assigning shared server globals; callers validate the
 * recovered value.
 */

/** `<` as a JavaScript string escape — the one character an inline script must not carry. */
const LT_ESCAPE = String.raw`\u003c`;

/**
 * Serialize a value as inline script source assigning window[name]. Escapes every less-than
 * character, including those in the property name, so data cannot close the script element. Values
 * JSON cannot serialize publish null; no global is assigned until the returned script runs.
 */
export function publishedGlobalScript(name: string, value: unknown): string {
  let payload: string;
  try {
    payload = JSON.stringify(value) ?? "null";
  } catch {
    payload = "null";
  }
  // Escape the whole statement, not just the payload: the name is a JSON string
  // literal too, and `<` can occur nowhere else in the emitted source.
  return `window[${JSON.stringify(name)}]=${payload};`.replaceAll("<", LT_ESCAPE);
}

/**
 * Read a named property from an object scope without validating its value. Returns undefined for
 * null or a non-object scope. Use globalThis in the browser and apply the application-specific
 * shape check before using the result.
 */
export function readPublishedGlobal(name: string, scope: unknown): unknown {
  if (typeof scope !== "object" || scope === null) {
    return undefined;
  }
  return (scope as Record<string, unknown>)[name];
}

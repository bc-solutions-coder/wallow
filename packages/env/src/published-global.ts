/**
 * The document channel for per-deployment values: a server render states its
 * answer in `<head>` as an inline `<script>` assigning one global, and the
 * hydrating browser reads the global back — so both renders agree and nothing
 * ships in the bundle. Only the BROWSER ever holds the global: the server
 * renders the script as text and never assigns it, because a server global is
 * shared by every concurrent request.
 *
 * These two halves must stay symmetric for the channel to be safe — the
 * escaping in {@link publishedGlobalScript} and the guard in
 * {@link readPublishedGlobal} used to live as per-caller copies that could
 * drift apart. What stays with each caller is VALIDATION: the reader hands back
 * whatever was published, and the caller narrows it to its own shape with its
 * own fallback, so a malformed global costs the deployment's override rather
 * than the page.
 */

/** `<` as a JavaScript string escape — the one character an inline script must not carry. */
const LT_ESCAPE = String.raw`\u003c`;

/**
 * The source of the inline `<script>` publishing `value` on `window[name]`,
 * rendered in `<head>` so it runs before hydration.
 *
 * The returned source contains no `<`: React does not escape a text child of
 * `<script>`, so a value containing `</script` would otherwise end the element
 * early. Escaping every `<` to its `\u003c` sequence keeps the JSON literal
 * valid and the element intact. A value JSON cannot express — `undefined`, a
 * circular object — publishes `null`, which every caller's read-back
 * validation already rejects.
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
 * The value {@link publishedGlobalScript} published on `scope` — `globalThis`
 * in a browser — or `undefined` when the scope is not an object or nothing
 * published one. The value comes back UNVALIDATED: the caller narrows it to
 * its own shape and keeps its own fallback.
 */
export function readPublishedGlobal(name: string, scope: unknown): unknown {
  if (typeof scope !== "object" || scope === null) {
    return undefined;
  }
  return (scope as Record<string, unknown>)[name];
}

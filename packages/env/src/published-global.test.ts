import { runInNewContext } from "node:vm";

import { describe, expect, it } from "vitest";

import { publishedGlobalScript, readPublishedGlobal } from "./published-global";

/**
 * The document channel's two halves, exercised together the way a deployment
 * uses them: the script source is EXECUTED against a stand-in `window`, and the
 * value is read back off that same scope. Executing rather than string-matching
 * is what makes the escaping test honest — an over-escaped payload would still
 * contain no `<` but would no longer parse back to the value published.
 */

const KEY = "__TEST_GLOBAL__";

/** Run the published script against a fresh scope and read the value back. */
function roundTrip(value: unknown): unknown {
  const scope: Record<string, unknown> = {};
  runInNewContext(publishedGlobalScript(KEY, value), { window: scope });
  return readPublishedGlobal(KEY, scope);
}

describe("publishedGlobalScript", () => {
  it("round-trips a string", () => {
    expect(roundTrip("https://wallow.dev/auth")).toBe("https://wallow.dev/auth");
  });

  it("round-trips an object", () => {
    expect(roundTrip({ repositoryUrl: "https://x.test", docsUrl: "https://y.test" })).toEqual({
      repositoryUrl: "https://x.test",
      docsUrl: "https://y.test",
    });
  });

  it("round-trips null", () => {
    expect(roundTrip(null)).toBeNull();
  });

  // React does not escape a text child of `<script>`, so a raw `</script` in the
  // payload would end the element early and hand the rest of the value to the
  // parser as markup.
  it("emits no `<`, and the escaped payload still round-trips", () => {
    const hostile = "https://x.test/</script><img src=x onerror=alert(1)>";
    expect(publishedGlobalScript(KEY, hostile)).not.toContain("<");
    expect(roundTrip(hostile)).toBe(hostile);
  });

  it("names the global as a quoted property, hostile key included", () => {
    const script: string = publishedGlobalScript(KEY, "v");
    expect(script).toContain(JSON.stringify(KEY));
    expect(publishedGlobalScript("</script>", "v")).not.toContain("<");
  });

  // The charter: total functions. A value JSON cannot express publishes `null`,
  // which every caller's read-back validation already rejects — costing the
  // deployment's override, never the page.
  it("publishes null for a value JSON cannot express", () => {
    expect(roundTrip(undefined)).toBeNull();

    const circular: Record<string, unknown> = {};
    circular["self"] = circular;
    expect(roundTrip(circular)).toBeNull();
  });
});

describe("readPublishedGlobal", () => {
  it("reads nothing from a scope that is not an object", () => {
    expect(readPublishedGlobal(KEY, undefined)).toBeUndefined();
    expect(readPublishedGlobal(KEY, null)).toBeUndefined();
    expect(readPublishedGlobal(KEY, "scope")).toBeUndefined();
  });

  it("reads nothing from a scope no script has written", () => {
    expect(readPublishedGlobal(KEY, {})).toBeUndefined();
  });

  it("hands back whatever was published, unvalidated", () => {
    // Validation is deliberately the caller's: each caller knows its own shape
    // and keeps its own fallback.
    expect(readPublishedGlobal(KEY, { [KEY]: 7 })).toBe(7);
  });
});

import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { textRecipe } from "../text/text.styles";
import { mutedTextRecipe } from "./muted-text.styles";

/*
 * RECONCILE SPEC (Wallow-lrlm.2.2). MutedText is exactly `Text` at the `bodySm`
 * scale in the `muted` colour, so after this task ONE recipe backs both: the
 * component composes over `Text` instead of assembling a class string of its
 * own.
 *
 * The reconcile is deliberately invisible at the DOM — both spellings render the
 * same `<p class="text-sm text-muted-foreground">`, which is the proof it is
 * behaviour-preserving and the reason `muted-text.test.tsx` must pass with zero
 * edits. Nothing observable in a render can therefore distinguish "composes over
 * Text" from "still has its own recipe", so the delegation is asserted against
 * the component's source, the way `src/core/package-scaffold.test.ts` asserts the
 * package's shape. Class-level behaviour stays where it already lives:
 * `muted-text.test.tsx` for the rendered paragraph, `text.test.tsx` for Text.
 *
 * This spec runs on the `node` project — it reads a file and calls two recipes,
 * and renders nothing.
 */

const componentDir = dirname(fileURLToPath(import.meta.url));

/** MutedText's class string, byte-for-byte — the `./muted-text` subpath contract. */
const MUTED_CLASS_STRING = "text-sm text-muted-foreground";

/**
 * A component file's code, with its comments removed. The component's own
 * docstring quotes the muted utilities as prose, and prose is documentation
 * rather than something the component does.
 */
function readComponentCode(fileName: string): string {
  return readFileSync(join(componentDir, fileName), "utf8")
    .replaceAll(/\/\*[\s\S]*?\*\//gu, "")
    .replaceAll(/^\s*\/\/.*$/gmu, "");
}

/** Every module specifier a source file imports or re-exports from. */
function moduleSpecifiers(source: string): string[] {
  const specifiers: string[] = [];

  for (const match of source.matchAll(/(?:\bfrom|^\s*import)\s+["']([^"']+)["']/gmu)) {
    const specifier = match[1];
    if (specifier !== undefined) {
      specifiers.push(specifier);
    }
  }

  return specifiers;
}

describe("MutedText composed over Text", () => {
  it("renders through the Text primitive", () => {
    const source = readComponentCode("muted-text.tsx");

    // A sibling import, i.e. the catalog's "deliberate reuse" case — the same
    // shape as Label over Field or ContextMenu over Menu.
    expect(moduleSpecifiers(source).some((specifier) => specifier.startsWith("../text"))).toBe(
      true,
    );
    expect(source).toMatch(/<Text\b/u);
  });

  it("asks Text for the bodySm scale in the muted colour", () => {
    const source = readComponentCode("muted-text.tsx");

    // The props are the whole point: a `<Text>` left at its defaults renders the
    // body scale in the default colour, and only a caller className would drag
    // it back to the muted paragraph — which is the recipe duplication this task
    // removes, not a reconcile.
    expect(source).toMatch(/as="p"/u);
    expect(source).toMatch(/variant="bodySm"/u);
    expect(source).toMatch(/color="muted"/u);
  });

  it("assembles no class string of its own", () => {
    const source = readComponentCode("muted-text.tsx");
    const specifiers = moduleSpecifiers(source);

    // Text owns the merge now: no local recipe call, and no `cn()` over it.
    expect(specifiers).not.toContain("./muted-text.styles");
    expect(specifiers).not.toContain("../../core/cn");
    expect(source).not.toMatch(/mutedTextRecipe\s*\(/u);
    expect(source).not.toMatch(/\bcn\s*\(/u);
  });

  it("hard-codes none of the muted utilities in its JSX", () => {
    const source = readComponentCode("muted-text.tsx");

    // The other way to satisfy the rendered-class contract while still bypassing
    // Text's recipe: pass the utilities down as a className. The type scale and
    // the colour are `Text`'s decisions, spelled as `variant` and `color`.
    expect(source).not.toContain("text-sm");
    expect(source).not.toContain("text-muted-foreground");
  });

  it("keeps mutedTextRecipe's class string byte-for-byte", () => {
    // The `@bc-solutions-coder/ui/muted-text` subpath ships this recipe, so the
    // string is a published contract regardless of what the component renders
    // through. Exact string, not an order-free set: callers concatenate it.
    expect(mutedTextRecipe()).toBe(MUTED_CLASS_STRING);
  });

  it("keeps the folder barrel exporting the component and the recipe", async () => {
    const barrel = (await import("./index")) as Record<string, unknown>;

    expect(typeof barrel.MutedText).toBe("function");
    expect(typeof barrel.mutedTextRecipe).toBe("function");
  });

  it("backs both components with one recipe", () => {
    // The reconcile's load-bearing invariant, and the reason `muted-text.test.tsx`
    // survives untouched: Text's bodySm + muted IS the muted paragraph. A future
    // base class or a `leading-*`/`font-*` folded into `bodySm` breaks MutedText,
    // and this is where it gets caught.
    expect(textRecipe({ variant: "bodySm", color: "muted" })).toBe(MUTED_CLASS_STRING);
  });
});

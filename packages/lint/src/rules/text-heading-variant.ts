import { defineRule, type Context, type ESTree } from "@oxlint/plugins";

/**
 * The heading contracts `react/forbid-elements` cannot state.
 *
 * Banning raw `<h1>`..`<h6>` moves every heading onto the catalog's `Text`, but says
 * nothing about what `Text` then renders. Three things can still go wrong, and all
 * three used to be asserted by a 523-line disk sweep in `wallow-auth`:
 *
 *   1. `Text` DERIVES its scale from `as` when the caller names no `variant`, and those
 *      defaults are far larger than a card ships: `as="h2"` derives `title` (`text-3xl`)
 *      against the `text-xl` these headings wear. Leaning on the default silently
 *      triples a card heading.
 *   2. A card heading has ONE spelling app-wide (`variant="subheading"`, the 20px step),
 *      and the weight rides along with the step — a `weight` prop is the same decision
 *      made a second time.
 *   3. A screen must not open a SECOND level-1 heading: `AuthLayout` owns the page's one
 *      `<h1>` and it is `FocusOnNavigate`'s route-change focus target.
 *
 * Options are `{ levels: { "<h1..h6>": "<variant>" | false } }`. A level mapped to a
 * variant must carry exactly that variant and no `weight`; a level mapped to `false` is
 * not allowed at that path at all; every level, named or not, must name SOME variant.
 * `auth-layout.tsx` — the one file that legitimately opens the page `<h1>` — relaxes the
 * `h1: false` entry through an override rather than turning the whole rule off, so its
 * heading is still held to naming a variant.
 *
 * Why the sweep is not simply kept: it read files off disk with a regex and a
 * hand-rolled comment stripper, so it judged the app once per `pnpm test` and could be
 * defeated by any spelling its regex did not model. A rule reads the parsed JSX, fires
 * in the editor, and cannot be out-run by formatting.
 */
const HEADING_LEVELS: ReadonlySet<string> = new Set(["h1", "h2", "h3", "h4", "h5", "h6"]);

type Level = string | false | undefined;

/**
 * The configured levels map.
 *
 * Read per node rather than once in `createOnce`: `createOnce` runs a single time per
 * PROCESS, while `context.options` is the options for the rule on the CURRENT file, and
 * this rule is deliberately configured differently by different override blocks
 * (`auth-layout.tsx` relaxes `h1`). Hoisting this read would freeze whichever file
 * happened to be linted first.
 */
function levelsOf(context: Context): Record<string, Level> {
  const options = context.options[0] as { levels?: Record<string, Level> } | undefined;

  return options?.levels ?? {};
}

/** The `JSXAttribute`s of `node`, by name. Spreads and namespaced names are skipped. */
function attributesByName(node: ESTree.JSXOpeningElement): Map<string, ESTree.JSXAttribute> {
  const found = new Map<string, ESTree.JSXAttribute>();

  for (const attribute of node.attributes) {
    if (attribute.type === "JSXAttribute" && attribute.name.type === "JSXIdentifier") {
      found.set(attribute.name.name, attribute);
    }
  }

  return found;
}

/** The string an attribute is set to, or null when it is dynamic, absent or a shorthand. */
function stringValue(attribute: ESTree.JSXAttribute | undefined): string | null {
  const value = attribute?.value;

  return value?.type === "Literal" && typeof value.value === "string" ? value.value : null;
}

export const textHeadingVariant = defineRule({
  meta: {
    type: "problem",
    docs: {
      description: "Require an explicit Text variant on every heading, at the app's scale.",
    },
    schema: [
      {
        type: "object",
        properties: { levels: { type: "object" } },
        additionalProperties: false,
      },
    ],
  },

  createOnce(context) {
    return {
      JSXOpeningElement(node) {
        if (node.name.type !== "JSXIdentifier" || node.name.name !== "Text") {
          return;
        }

        const attributes = attributesByName(node);
        const level = stringValue(attributes.get("as"));

        if (level === null || !HEADING_LEVELS.has(level)) {
          return;
        }

        const expected = levelsOf(context)[level];

        if (expected === false) {
          context.report({
            node,
            message:
              `\`<Text as="${level}">\` is not this app's to open. AuthLayout owns the page's one ` +
              "level-1 heading and it is FocusOnNavigate's route-change focus target, so a second " +
              'one inside a card is an accessibility defect. Open the card at `as="h2"` instead.',
          });

          return;
        }

        const variant = attributes.get("variant");

        if (variant === undefined) {
          context.report({
            node,
            message:
              `\`<Text as="${level}">\` must name its \`variant\`. Text DERIVES the scale from ` +
              `\`as\` when none is given, and the derived step is much larger than a card heading ` +
              '— `as="h2"` derives `title` (text-3xl) against the text-xl these headings wear.',
          });

          return;
        }

        if (typeof expected !== "string") {
          return;
        }

        if (stringValue(variant) !== expected) {
          context.report({
            node: variant,
            message:
              `\`<Text as="${level}">\` renders a card heading, which is \`variant="${expected}"\` ` +
              "app-wide (20px, the catalog-wide card-heading step). A second scale on the same " +
              "card slot is the split this standard closed.",
          });
        }

        const weight = attributes.get("weight");

        if (weight !== undefined) {
          context.report({
            node: weight,
            message:
              `\`variant="${expected}"\` already carries its own font weight, so a \`weight\` prop ` +
              "here decides the same thing a second time. Delete it and take the weight from the " +
              "step.",
          });
        }
      },
    };
  },
});

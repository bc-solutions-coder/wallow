import { defineRule, type Node } from "@oxlint/plugins";

const TINTABLE_TOKENS: readonly string[] = [
  "foreground",
  "background",
  "card",
  "card-foreground",
  "primary",
  "primary-foreground",
  "secondary",
  "muted",
  "muted-foreground",
  "accent",
  "accent-foreground",
  "destructive",
  "border",
  "ring",
  "sidebar",
  "sidebar-foreground",
  "sidebar-accent",
  "success",
];

/**
 * `text-<theme token>/<alpha>`, with any variant prefix and the `!` marker allowed.
 * The prefix admits `:` — `hover:text-primary/80` and `dark:md:text-foreground/60` are
 * the same offence as the unprefixed spelling, and a prefix pattern that stopped at the
 * colon would silently pass every one of them.
 */
const TINTED_TEXT = new RegExp(
  String.raw`(?:^|\s)[a-z0-9:-]*!?text-(?:${TINTABLE_TOKENS.join("|")})\/\d{1,3}(?=\s|$)`,
  "gu",
);

/**
 * Reject alpha modifiers on configured theme text colors in string literals.
 * Variant prefixes and important markers are recognized. Background colors are
 * outside this rule; use semantic text colors for muted or secondary content.
 */
export const noTintedText = defineRule({
  meta: {
    type: "problem",
    docs: {
      description: "Ban alpha-modified text colours in favour of a named theme token.",
    },
    schema: [],
  },

  createOnce(context) {
    function check(node: Node, value: string): void {
      const found = [...new Set((value.match(TINTED_TEXT) ?? []).map((match) => match.trim()))];

      if (found.length === 0) {
        return;
      }

      context.report({
        node,
        message:
          `\`${found.join("` and `")}\` tints a text colour instead of naming one, so a fork ` +
          "editing branding.json cannot reach the result and a second file reaching for the same " +
          "alpha has agreed on a meaning nobody wrote down. Muted copy is `text-muted-foreground` " +
          '(`Text color="muted"`). A translucent SURFACE — `bg-foreground/40` — is not this, and ' +
          "stays allowed.",
      });
    }

    return {
      Literal(node) {
        if (typeof node.value === "string") {
          check(node, node.value);
        }
      },

      TemplateElement(node) {
        check(node, node.value.cooked ?? node.value.raw);
      },
    };
  },
});

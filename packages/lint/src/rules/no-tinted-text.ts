import { defineRule, type Node } from "@oxlint/plugins";

/**
 * Tinted text.
 *
 * `text-foreground/60` is a colour this theme cannot name: a fork editing
 * `branding.json` cannot reach it, and two files reaching for the same `/70` have
 * agreed on a design meaning without ever writing it down. Muted body copy is
 * `text-muted-foreground` — `Text color="muted"` — which a fork CAN retheme.
 *
 * Only the `text-` family is judged. A translucent SURFACE is a different thing and
 * a legitimate one: `DashboardLayout`'s `bg-foreground/40` drawer scrim has no opaque
 * spelling, and the catalog's backdrops are alpha by construction. Drawing the line at
 * text is what lets this rule run with no per-file exemption list — the retired sweep
 * banned every family and then had to name two files back out again.
 */
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

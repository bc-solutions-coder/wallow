import { defineRule, type Node } from "@oxlint/plugins";

/**
 * The retired sidebar inversion.
 *
 * These two do not name a surface — they swap the page's own two colours, which
 * means a fork editing `branding.json` cannot reach the result. The rail is painted
 * with the named `sidebar-*` family instead.
 *
 * `border-foreground` is deliberately absent: the landing page's outline CTA draws
 * its border in the page's own ink, which was adjudicated correct and is not an
 * inversion.
 */
const INVERSION_UTILITIES: ReadonlySet<string> = new Set(["bg-foreground", "text-background"]);

/**
 * The retired utilities named by `value`, if any.
 *
 * Only the BARE form counts. `bg-foreground/40` is the drawer scrim: translucency
 * cannot be expressed by an opaque token, so an alpha modifier is categorically not
 * an inversion — an inversion swaps two OPAQUE colours. Comparing the whole utility
 * rather than searching for a substring is what draws that line, and it also keeps
 * `bg-background`/`text-foreground` (the page painting itself) out of the ban.
 *
 * Variant prefixes are stripped, so `hover:bg-foreground` and `dark:md:bg-foreground`
 * are the same offence as the unprefixed one, as is the `!` important marker.
 */
function offenders(value: string): string[] {
  const found = new Set<string>();

  for (const token of value.split(/\s+/u)) {
    const utility = token.slice(token.lastIndexOf(":") + 1).replace(/^!/u, "");

    if (INVERSION_UTILITIES.has(utility)) {
      found.add(utility);
    }
  }

  return [...found];
}

/**
 * Every string in the file, not just `className="..."`.
 *
 * The surviving colour in this app is written as a hoisted `const` and interpolated
 * into a template literal (`DashboardLayout`'s `BACKDROP_SCRIM`), so that is the
 * shape a reintroduction would copy. A rule that only visited JSX attributes would
 * miss it, and would miss the template-literal form too.
 */
export const noSidebarInversion = defineRule({
  meta: {
    type: "problem",
    docs: {
      description: "Ban the retired bg-foreground/text-background sidebar inversion.",
    },
    schema: [],
  },

  createOnce(context) {
    function check(node: Node, value: string): void {
      const found = offenders(value);

      if (found.length === 0) {
        return;
      }

      const verb = found.length === 1 ? "is" : "are";

      context.report({
        node,
        message:
          `\`${found.join("` and `")}\` ${verb} the retired sidebar inversion. It swaps the page's two ` +
          "colours instead of naming a surface, so a fork editing branding.json cannot reach it. " +
          "Paint the rail with the named sidebar tokens instead (bg-sidebar, text-sidebar-foreground, " +
          "hover:bg-sidebar-accent). A translucent scrim such as bg-foreground/40 is not an inversion " +
          "and stays allowed.",
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

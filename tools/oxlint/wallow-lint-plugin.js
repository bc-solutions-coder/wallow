/**
 * Wallow's own oxlint rules — the ones with no native equivalent.
 *
 * oxlint has no rule that reads Tailwind class strings, and `no-restricted-syntax`
 * (which an ESLint config would reach for here) is not implemented: naming it makes
 * oxlint refuse to parse the whole config. A JS plugin is the only mechanism that
 * can judge a class name, so the class-string half of the F5 migration gate lives
 * here while its element half stays on the native `react/forbid-elements`.
 *
 * WHERE THIS IS REGISTERED. A loose file under `tools/`, loaded by
 * `apps/wallow-web/.oxlintrc.json` and `apps/wallow-auth/.oxlintrc.json` — the nested
 * configs that also switch the rules on — through the relative specifier
 * `../../tools/oxlint/wallow-lint-plugin.js`. It is NOT registered from the root
 * `.oxlintrc.json`, and it must not be moved there.
 *
 * WHY NOT THE ROOT CONFIG. `packages/sdk/src/oxlint-guardrails.test.ts` proves the root
 * config's import bans by COPYING it to a temp directory and running the real binary
 * there, and any `jsPlugins` entry makes that copy unloadable: oxlint answers `Failed to
 * parse oxlint configuration file`, the spec's `JSON.parse` throws on it, and the whole
 * file fails to collect — 0 tests run, an unrelated suite down. Both root placements
 * were tried and both fail this way: a top-level `jsPlugins`, and a `jsPlugins` scoped
 * to the root's `apps/wallow-web` override block (the schema does permit it there).
 *
 * NO SPECIFIER FORM RESCUES THAT — measured on oxlint 1.74.0, not assumed. A `jsPlugins`
 * specifier is resolved from the CONFIG FILE's own directory, whichever form it takes:
 *   - RELATIVE: the temp-dir copy looks for `<tmp>/tools/...` and reports `Cannot find
 *     module`.
 *   - BARE: `jsPlugins: ["oxfmt"]` RESOLVES from a config at the repo root — where
 *     `node_modules/oxfmt` is reachable — and reports `Cannot find module 'oxfmt'` from
 *     a config in a temp directory, with the cwd at the repo root either way. (`["oxlint"]`
 *     resolves from anywhere, but only because oxlint can reach itself.)
 * So the temp copy cannot load ANY form, relative or bare, loose file or workspace
 * package: it resolves from a directory with no `node_modules` in it at all. Keeping the
 * entry in the nested config keeps it out of the file that gets copied.
 *
 * The nesting is load-bearing a second time: `packages/ui` legitimately paints an
 * animated backdrop with a bare `bg-foreground`, so this rule must never reach the
 * catalog. Living under `apps/wallow-web/` makes that structural rather than a per-glob
 * exemption that can rot.
 *
 * THE COST. Because oxlint matches an override's globs and `ignorePatterns` relative to
 * the config's own directory, each nested config has to RESTATE the root's
 * `apps/<app>/**` override block and the root's `ignorePatterns` — their repo-rooted
 * prefixes match nothing once matching starts at `apps/<app>/`. So a future edit to a
 * root app block silently stops applying to that app. Do not delete the restatement.
 * (A `lint-gate.test.ts` used to model both configs and prove they had not drifted; it
 * was 1009 lines re-implementing oxlint's own resolution and was removed in 4d3fb6ee.)
 *
 * BOTH APPS ARE GATED, not identically. `apps/wallow-auth/.oxlintrc.json` carries the
 * same rules plus `button` on the forbid list and the `text-heading-variant` levels below
 * — wallow-web's `bff-demo` route deliberately keeps four raw `<button>`s as the
 * un-catalogued control of the BFF demo, so the button ban cannot be lifted to both, and
 * the same route deliberately takes `Text`'s DERIVED scale (no `variant` anywhere), which
 * is why `text-heading-variant` is wallow-auth's alone. wallow-web's headings are not one
 * shape: `LandingPage` runs `display`/`title`/`h3`, the detail routes run `title`. Do not
 * switch the rule on there without first deciding what those should be.
 */

/**
 * The retired sidebar inversion (Wallow-lrlm.5.4).
 *
 * These two do not name a surface — they swap the page's own two colours, which
 * means a fork editing `branding.json` cannot reach the result. The rail is painted
 * with the named `sidebar-*` family instead.
 *
 * `border-foreground` is deliberately absent: the landing page's outline CTA draws
 * its border in the page's own ink, which was adjudicated correct and is not an
 * inversion.
 */
const INVERSION_UTILITIES = new Set(["bg-foreground", "text-background"]);

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
function offenders(value) {
  const found = new Set();

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
const noSidebarInversion = {
  meta: {
    type: "problem",
    docs: {
      description: "Ban the retired bg-foreground/text-background sidebar inversion.",
    },
    schema: [],
  },

  create(context) {
    function check(node, value) {
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
};

/**
 * The heading contracts `react/forbid-elements` cannot state (Wallow-l5x2).
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
const HEADING_LEVELS = new Set(["h1", "h2", "h3", "h4", "h5", "h6"]);

/** The `JSXAttribute`s of `node`, by name. Spreads and namespaced names are skipped. */
function attributesByName(node) {
  const found = new Map();

  for (const attribute of node.attributes) {
    if (attribute.type === "JSXAttribute" && attribute.name?.type === "JSXIdentifier") {
      found.set(attribute.name.name, attribute);
    }
  }

  return found;
}

/** The string an attribute is set to, or null when it is dynamic, absent or a shorthand. */
function stringValue(attribute) {
  const value = attribute?.value;

  return value?.type === "Literal" && typeof value.value === "string" ? value.value : null;
}

const textHeadingVariant = {
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

  create(context) {
    const levels = context.options?.[0]?.levels ?? {};

    return {
      JSXOpeningElement(node) {
        if (node.name?.type !== "JSXIdentifier" || node.name.name !== "Text") {
          return;
        }

        const attributes = attributesByName(node);
        const level = stringValue(attributes.get("as"));

        if (level === null || !HEADING_LEVELS.has(level)) {
          return;
        }

        const expected = levels[level];

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
};

/**
 * Tinted text (Wallow-lrlm.5.3, retired from a disk sweep by Wallow-l5x2).
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
const TINTABLE_TOKENS = [
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

const noTintedText = {
  meta: {
    type: "problem",
    docs: {
      description: "Ban alpha-modified text colours in favour of a named theme token.",
    },
    schema: [],
  },

  create(context) {
    function check(node, value) {
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
};

export default {
  meta: {
    name: "wallow",
  },
  rules: {
    "no-sidebar-inversion": noSidebarInversion,
    "no-tinted-text": noTintedText,
    "text-heading-variant": textHeadingVariant,
  },
};

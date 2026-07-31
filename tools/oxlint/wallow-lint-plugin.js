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
 * `apps/wallow-web/.oxlintrc.json` — the nested config that also switches the rule on —
 * through the relative specifier `../../tools/oxlint/wallow-lint-plugin.js`. It is NOT
 * registered from the root `.oxlintrc.json`, and it must not be moved there.
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
 * THE COST, AND WHAT GUARDS IT. Because oxlint matches an override's globs and
 * `ignorePatterns` relative to the config's own directory, the nested config has to
 * RESTATE the root's `apps/wallow-web/**` override block and the root's
 * `ignorePatterns` — their repo-rooted prefixes match nothing once matching starts at
 * `apps/wallow-web/`. So a future edit to the root's wallow-web block silently stops
 * applying to this app. `apps/wallow-web/src/lint-gate.test.ts` ("the nested config does
 * not drift from the root") fails when that happens: it resolves both configs the way
 * oxlint does — match globs against real PATHS in this app, fold the matches, last one
 * wins — so a root block re-spelled (`apps/{wallow-web,wallow-auth}/**`) and a nested
 * block appended AFTER the restatement are both caught, and a glob spelling it cannot
 * model fails loudly rather than being skipped. Do not delete the restatement, and do
 * not delete that guard.
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

export default {
  meta: {
    name: "wallow",
  },
  rules: {
    "no-sidebar-inversion": noSidebarInversion,
  },
};

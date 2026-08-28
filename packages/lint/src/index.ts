import { definePlugin, eslintCompatPlugin } from "@oxlint/plugins";

import { moduleListsInSync } from "./rules/module-lists-in-sync.ts";
import { noHandRolledMutation } from "./rules/no-hand-rolled-mutation.ts";
import { noSidebarInversion } from "./rules/no-sidebar-inversion.ts";
import { noSourceTests } from "./rules/no-source-tests.ts";
import { noTintedText } from "./rules/no-tinted-text.ts";
import { textHeadingVariant } from "./rules/text-heading-variant.ts";
import { zoneDag } from "./rules/zone-dag.ts";

/**
 * Wallow's own oxlint rules — the ones with no native equivalent.
 *
 * Registration constraints (which config may load this, and why not the root one)
 * live in `packages/lint/CLAUDE.md`. Read that before moving this entry anywhere.
 *
 * The `.ts` extensions above are MANDATORY. oxlint loads this file as plain Node ESM,
 * which rejects an extensionless relative specifier with `ERR_MODULE_NOT_FOUND` — and
 * TypeScript typechecks either spelling clean, so the failure only appears at lint time.
 *
 * `eslintCompatPlugin` adds a delegating `create` to every rule defined with
 * `createOnce`, which is what keeps this plugin usable from ESLint as well as oxlint.
 */
export default eslintCompatPlugin(
  definePlugin({
    meta: { name: "wallow" },
    rules: {
      "module-lists-in-sync": moduleListsInSync,
      "no-hand-rolled-mutation": noHandRolledMutation,
      "no-sidebar-inversion": noSidebarInversion,
      "no-source-tests": noSourceTests,
      "no-tinted-text": noTintedText,
      "text-heading-variant": textHeadingVariant,
      "zone-dag": zoneDag,
    },
  }),
);

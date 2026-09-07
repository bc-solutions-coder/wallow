import { definePlugin, eslintCompatPlugin } from "@oxlint/plugins";

import { loggerNoNodeBuiltins } from "./rules/logger-no-node-builtins.ts";
import { moduleListsInSync } from "./rules/module-lists-in-sync.ts";
import { noHandRolledMutation } from "./rules/no-hand-rolled-mutation.ts";
import { noSidebarInversion } from "./rules/no-sidebar-inversion.ts";
import { noSourceTests } from "./rules/no-source-tests.ts";
import { noTintedText } from "./rules/no-tinted-text.ts";
import { textHeadingVariant } from "./rules/text-heading-variant.ts";
import { zoneDag } from "./rules/zone-dag.ts";

/**
 * Wallow lint rules for oxlint and ESLint-compatible consumers.
 *
 * Register this plugin once as wallow in the root oxlint configuration. Nested
 * configs inherit that registration. Relative imports retain .ts extensions
 * because oxlint loads this entry directly through Node ESM.
 */
export default eslintCompatPlugin(
  definePlugin({
    meta: { name: "wallow" },
    rules: {
      "logger-no-node-builtins": loggerNoNodeBuiltins,
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

import { fileURLToPath } from "node:url";

import { assertBrowserStylesWiring } from "@bc-solutions-coder/testing/browser-styles-wiring";

/**
 * On-disk guard for this app's browser-project styling wiring.
 *
 * The rendered half is `theme-wiring.test.tsx`; this one names the pieces that
 * have to stay wired so removing one fails with a message saying WHICH.
 */
assertBrowserStylesWiring({ appDir: fileURLToPath(new URL("../", import.meta.url)) });

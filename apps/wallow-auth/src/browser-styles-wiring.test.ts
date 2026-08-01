import { fileURLToPath } from "node:url";

import { assertBrowserStylesWiring } from "@bc-solutions-coder/testing/browser-styles-wiring";

/**
 * On-disk guard for this app's browser-project styling wiring.
 *
 * The rendered half is `theme-wiring.test.tsx`; this one names the pieces that
 * have to stay wired so removing one fails with a message saying WHICH. The
 * extra spec list is this app's checkbox-bearing screens, which are the ones
 * that grow a focus+Space workaround when the stylesheet goes missing.
 */
assertBrowserStylesWiring({
  appDir: fileURLToPath(new URL("../", import.meta.url)),
  extraSpecs: [
    "src/features/accept-terms/components/AcceptTermsScreen.test.tsx",
    "src/features/register/components/RegisterForm.test.tsx",
    "src/features/login/components/LoginScreen.test.tsx",
    "src/features/login/components/OtpLoginForm.test.tsx",
  ],
});

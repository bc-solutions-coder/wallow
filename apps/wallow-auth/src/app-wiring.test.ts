import { fileURLToPath } from "node:url";

import {
  accountLoginMutation,
  accountRegisterMutation,
  accountSendMagicLinkMutation,
  accountSendOtpMutation,
  accountVerifyMagicLinkOptions,
  accountVerifyMfaChallengeMutation,
  accountVerifyOtpMutation,
  invitationsAcceptMutation,
  mfaConfirmEnrollmentMutation,
  mfaEnrollTotpMutation,
} from "@bc-solutions-coder/sdk/query";
import {
  browserPreBundleList,
  describeBrowserPreBundleList,
} from "@bc-solutions-coder/testing/browser-deps";
import { assertBrowserStylesWiring } from "@bc-solutions-coder/testing/browser-styles-wiring";
import { afterEach, describe, expect, it, vi } from "vitest";

import viteConfig from "../vite.config";
import vitestConfig from "../vitest.config";

/**
 * Everything holding wallow-auth together that no rendered spec can reach: the build
 * config, the vitest harness, and the generated write surface its screens name. Each
 * `describe` below names the guard it is — with these merged into one file, the
 * `describe` is what tells a reader what broke.
 *
 * The browser half is `app-wiring.browser.test.tsx`. Both stay directly under `src/`:
 * `wallow/zone-dag` exempts single-segment paths as `ROOT_ZONE`, and that exemption
 * is the only thing permitting the `../vite.config` imports above.
 *
 * Node project: mounts nothing.
 */

/** This app's root — the directory holding `package.json`, `vite.config.ts` and `src/`. */
const appDir: string = fileURLToPath(new URL("..", import.meta.url));

/** The one place react-query is allowed to enter this workspace. */
const FACADE = "@bc-solutions-coder/query";
/** The shared authn layer, and the only door this app's auth reads come through. */
const AUTH = "@bc-solutions-coder/auth";

describe("the base path Vite resolves from AUTH_BASE_PATH", () => {
  // Scoped inside this describe rather than at file level: only these three cases
  // stub the environment, and the other guards here must not inherit the cleanup.
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  async function loadConfig(basePath?: string): Promise<Record<string, unknown>> {
    vi.stubEnv("AUTH_BASE_PATH", basePath ?? "");
    vi.resetModules();
    const module = (await import("../vite.config")) as { default: Record<string, unknown> };
    return module.default;
  }

  it("serves at the root when AUTH_BASE_PATH is unset — the default fork behavior", async () => {
    const config = await loadConfig();

    // Either spelling is the Vite default; what must NOT happen is a prefix.
    expect(config.base ?? "/").toBe("/");
  });

  it("bases the build on AUTH_BASE_PATH so every emitted asset URL carries the prefix", async () => {
    const config = await loadConfig("/auth");

    expect(config.base).toBe("/auth/");
  });

  it("tolerates the prefix written without slashes, as a compose file will spell it", async () => {
    const config = await loadConfig("auth");

    expect(config.base).toBe("/auth/");
  });
});

describe("the wallow-auth client build", () => {
  it("re-enables copyPublicDir on the client environment", () => {
    // Start builds through nitro/vite's two named environments, and nitro does
    // `config.build.copyPublicDir ??= false` on the CLIENT one. That silently
    // drops the publicDir the brand-assets plugin contributes, so `/piggy-icon.svg`
    // 404s in the BUILT app only — the dev server serves publicDir itself and
    // looks fine.
    //
    // That the plugin points `publicDir` at the shared package is asserted where
    // the plugin lives, in `packages/styles/src/vite.test.ts`. This side reads the
    // config an app actually hands Vite, which an app is free to override.
    expect(viteConfig.environments?.client?.build?.copyPublicDir).toBe(true);
  });
});

// The pre-bundle guard: every `optimizeDeps.include` entry has to be declared and
// has to resolve. An unresolvable one is a WARNING Vite ignores, so the list can
// look complete while pre-bundling nothing — the machinery and the full
// explanation live in `@bc-solutions-coder/testing/browser-deps`.
describeBrowserPreBundleList({ packageDir: appDir, config: vitestConfig });

// The on-disk half of the browser project's styling wiring. The rendered half is in
// `app-wiring.browser.test.tsx`; this one names the pieces that have to stay wired,
// so removing one fails with a message saying WHICH. The extra spec list is this
// app's checkbox-bearing screens, the ones that grow a focus+Space workaround when
// the stylesheet goes missing.
assertBrowserStylesWiring({
  appDir,
  extraSpecs: [
    "src/features/accept-terms/components/AcceptTermsScreen.test.tsx",
    "src/features/register/components/RegisterForm.test.tsx",
    "src/features/login/components/LoginScreen.test.tsx",
    "src/features/login/components/OtpLoginForm.test.tsx",
  ],
});

describe("browser-mode pre-bundling covers the linked workspace packages", () => {
  // A linked workspace package is not pre-bundled by default, and a dependency
  // discovered mid-run triggers a Vite reload that DROPS the runner instead of
  // failing a test — the worst failure mode in this app, whose specs are the auth
  // flow's safety net.
  //
  // Only PRESENCE is asserted here. That the list is non-empty, that every entry is
  // declared, and that every entry resolves from the app root under Vite's own
  // conditions are the shared guard's three cases, run above.
  it("registers the query facade with the browser project", () => {
    expect(browserPreBundleList(vitestConfig)).toContain(FACADE);
  });

  it("registers the auth package with the browser project or inlines it for SSR", () => {
    const noExternal = vitestConfig.ssr?.noExternal;
    const inlinedForSsr: boolean = Array.isArray(noExternal) && noExternal.includes(AUTH);

    expect(inlinedForSsr || browserPreBundleList(vitestConfig).includes(AUTH)).toBe(true);
  });
});

/**
 * Every generated mutation factory wallow-auth's screens reach, bound to its real
 * export.
 *
 * NAMED imports, not a namespace one: `no-restricted-imports` bans `import *` from
 * this entry, and naming each binding is the stronger check anyway — a factory the
 * generator does not export is a module-load crash before it is ever an assertion.
 *
 * `() => unknown` is the widest shape all nine share: each takes one OPTIONAL options
 * argument, so calling with none is valid for every one of them.
 */
const MUTATION_FACTORIES: readonly (readonly [string, () => unknown])[] = [
  ["accountLoginMutation", accountLoginMutation],
  ["accountRegisterMutation", accountRegisterMutation],
  ["accountSendMagicLinkMutation", accountSendMagicLinkMutation],
  ["accountSendOtpMutation", accountSendOtpMutation],
  ["accountVerifyMfaChallengeMutation", accountVerifyMfaChallengeMutation],
  ["accountVerifyOtpMutation", accountVerifyOtpMutation],
  ["invitationsAcceptMutation", invitationsAcceptMutation],
  ["mfaConfirmEnrollmentMutation", mfaConfirmEnrollmentMutation],
  ["mfaEnrollTotpMutation", mfaEnrollTotpMutation],
];

describe("the generated artifacts wallow-auth's screens name", () => {
  // `wallow/no-hand-rolled-mutation` enforces the rule at the offending line, and
  // `packages/sdk` proves an artifact EXISTS for every operation. Neither says what
  // a factory hands back, which is what these two cases cover.
  it.each(MUTATION_FACTORIES)(
    "%s hands back a mutationFn and nothing else",
    (name: string, factory: () => unknown) => {
      // bd memory `when-migrating-a-form-to-bc-solutions-coder`: the factories bake
      // in NO `onSuccess`. That is why `InvitationScreen`, whose success arm is a
      // full-page navigation declared on the hook rather than at the call site, has
      // to SPREAD the factory into its `useMutation` options instead of passing it
      // straight through — a replacement would silently drop the navigation.
      expect(Object.keys(factory() as object).toSorted(), name).toEqual(["mutationFn"]);
    },
  );

  it("gives the magic-link verify a queryFn and a key under its Options factory", () => {
    // THE SPECIAL CASE. `GET /passwordless/magic-link/verify` is a read as far as the
    // generator is concerned, so there is no `accountVerifyMagicLinkMutation` to
    // convert to and the screen gets a read artifact instead: the redemption runs
    // through the query client.
    const options = accountVerifyMagicLinkOptions({ query: { token: "t" } });

    expect(typeof options.queryFn).toBe("function");
    expect(Array.isArray(options.queryKey)).toBe(true);
  });
});

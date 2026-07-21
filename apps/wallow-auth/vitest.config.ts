import { playwright } from "@vitest/browser-playwright";
import { configDefaults, defineConfig } from "vitest/config";

/**
 * Vitest harness for wallow-auth — multi-project split (Wallow-xzha.2.2, widened
 * to the whole component suite in Wallow-xzha.3.1).
 *
 * Two runtimes, one per project:
 *
 *   node    — pure-logic specs only: every `src/lib/*.test.ts` plus the
 *             repo-config guards, and the ONE pure-logic `*.test.tsx`
 *             (`routes/index.test.tsx`, which only reads a route's `beforeLoad`
 *             and renders nothing). No DOM, no browser overhead.
 *   browser — every component spec (`*.test.tsx`) now that F3 has migrated them
 *             off `@testing-library/react` + jsdom onto `vitest-browser-react`.
 *             They run in headless Chromium via Playwright with ZERO jsdom
 *             involvement, so `getBoundingClientRect`, focus, and computed styles
 *             report browser-true values.
 *
 * NAVIGATION SEAM (Wallow-xzha.3.1): `window.location` is `[Unforgeable]` in a
 * real browser, so the jsdom-only `vi.stubGlobal("location", …)` hack cannot
 * shadow it and assigning `location.href` navigates the Chromium iframe and tears
 * down the runner. Screens that hand off with `globalThis.location.href = url`
 * are therefore tested by asserting the URL-builder seam
 * (`buildExchangeTicketUrl`/`buildConsentSubmitUrl`/…) was called with the exact
 * origin + ticket + returnUrl — an equivalent to pinning the assigned string,
 * since the builder is deterministic — and the builder mock returns a
 * non-navigating sentinel so the assignment stays put. See each migrated spec.
 *
 * NOTE (Vitest 4 provider API): v4 replaced the v3 string form
 * `provider: "playwright"` with the factory `provider: playwright()` from
 * `@vitest/browser-playwright` — passing the bare string now throws
 * ("configuration was changed to accept a factory instead of a string").
 */
const componentSpecs = "src/**/*.test.tsx";

// The one pure-logic `*.test.tsx`: it asserts a route's `beforeLoad` redirect
// and renders no DOM, so it belongs on node, not in the browser.
const pureLogicTsx = "src/routes/index.test.tsx";

export default defineConfig({
  test: {
    projects: [
      {
        test: {
          name: "node",
          environment: "node",
          include: ["src/**/*.test.ts", pureLogicTsx],
          exclude: [...configDefaults.exclude],
        },
      },
      {
        // Pre-bundle the browser render helpers so Vitest does not discover and
        // re-optimize them mid-run (a reload after the first import otherwise
        // drops the test runner — "Vitest failed to find the runner").
        optimizeDeps: {
          include: ["vitest-browser-react", "react/jsx-dev-runtime", "react-dom/client"],
        },
        test: {
          name: "browser",
          include: [componentSpecs],
          exclude: [...configDefaults.exclude, pureLogicTsx],
          browser: {
            enabled: true,
            provider: playwright(),
            headless: true,
            instances: [{ browser: "chromium" }],
          },
        },
      },
    ],
  },
});

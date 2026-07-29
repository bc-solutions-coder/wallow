import { chromium, type Browser, type FullConfig, type Page } from "@playwright/test";

/**
 * Warm the app under test to hydration before any spec runs. NOT a spec file —
 * its name is outside Playwright's `*.spec.ts` glob, so the runner never treats
 * it as a test; `playwright.config.ts` wires it in as `globalSetup`. Mirrors
 * apps/wallow-auth/e2e/global-setup.ts, for the same reason.
 *
 * Playwright's `webServer` reports ready as soon as the port accepts a
 * connection, but `pnpm dev` is not finished at that moment: on a cold
 * `node_modules/.vite` the first request still waits on Vite's pre-bundle of the
 * whole browser graph — every `@base-ui/react/*` subpath `@bc-solutions-coder/ui`
 * reaches for. Paying that cost once here, on a budget generous enough for the
 * pre-bundle, makes the readiness marker mean the same thing for the first spec
 * as for the last.
 *
 * This is the suite's guard, not the fix for a broken dev server: the
 * mid-request re-optimisation that used to kill the first page outright (a wall
 * of 504 "Outdated Optimize Dep", no hydration, no auto-reload) is fixed at
 * source by the TanStack Start Vite plugin, which points Vite's dependency scan
 * at the client entry.
 */
const WARMUP_TIMEOUT_MS = 120_000;

export default async function warmUpAppUnderTest(config: FullConfig): Promise<void> {
  const baseURL: string | undefined = config.projects[0]?.use.baseURL;

  if (baseURL === undefined) {
    throw new Error("e2e global setup: no baseURL is configured");
  }

  const browser: Browser = await chromium.launch();

  try {
    const page: Page = await browser.newPage();
    await page.goto(baseURL);
    // The same readiness marker every spec waits on (.claude/rules/E2E.md), so
    // the warm-up proves precisely the condition the suite goes on to assume.
    await page
      .locator("[data-app-ready='true']")
      .waitFor({ state: "attached", timeout: WARMUP_TIMEOUT_MS });
  } finally {
    await browser.close();
  }
}

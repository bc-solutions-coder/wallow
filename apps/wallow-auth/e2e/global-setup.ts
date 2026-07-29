import { chromium, type Browser, type FullConfig, type Page } from "@playwright/test";

/**
 * Warm the app under test to hydration before any spec runs. NOT a spec file —
 * its name is outside Playwright's `*.spec.ts` glob, so the runner never treats
 * it as a test; `playwright.config.ts` wires it in as `globalSetup`.
 *
 * Playwright's `webServer` reports ready as soon as the port accepts a
 * connection, but `pnpm dev` is not finished at that moment: on a cold
 * `node_modules/.vite` the first request still waits on Vite's pre-bundle of the
 * whole browser graph — every `@base-ui/react/*` subpath `@bc-solutions-coder/ui`
 * reaches for. Most specs here wait for `data-app-ready` on the DEFAULT 5s
 * `expect` budget, which that first request can outlast, and the tests that lose
 * the race are simply whichever ones the parallel scheduler started first.
 *
 * Paying the cost once here, on a budget generous enough for the pre-bundle,
 * makes the readiness marker mean the same thing for the first spec as for the
 * last. Against the prebuilt container `E2E_BASE_URL` drives in CI it is a
 * sub-second no-op — there is no Vite there.
 *
 * This is the suite's guard, not the fix for a broken dev server: the
 * mid-request re-optimisation that used to kill the first page outright (a wall
 * of 504 "Outdated Optimize Dep", no hydration, no auto-reload) is fixed at
 * source by the TanStack Start Vite plugin, which points Vite's dependency scan
 * at the client entry.
 */
const WARMUP_TIMEOUT_MS = 120_000;

/**
 * Prove the app under test proxies to the API this run was pointed at, before
 * any backend-dependent spec assumes it.
 *
 * `playwright.config.ts` sets `reuseExistingServer: true`, so a `pnpm dev`
 * already listening on the port is adopted as-is — including its OWN
 * `WALLOW_API_INTERNAL_URL`, because `webServer.env` only reaches a server
 * Playwright actually starts. An orphaned dev server left over from ordinary
 * development therefore silently serves the whole suite against the DEV API
 * instead of the isolated stack scripts/e2e.sh just brought up, and the run
 * fails with a scatter of unrelated-looking symptoms: the email specs poll the
 * test Mailpit while their mail lands in the dev one, and logout.spec.ts asks
 * for a `post_logout_redirect_uri` only the test API's configured AuthUrl
 * allow-lists. Nothing in that failure set points at the server.
 *
 * The listener is also easy to miss by hand: Vite binds `[::1]` only, so a
 * `curl http://localhost:3002` that resolves to IPv4 reports the port closed
 * while Node — which prefers `::1` — connects happily.
 *
 * The discriminator is the OIDC discovery `issuer`, which wallow-auth's
 * passthrough proxy forwards verbatim from whichever API it targets. Comparing
 * it against the issuer read straight from `WALLOW_API_INTERNAL_URL` identifies
 * the backend without hardcoding any URL, so a dev server that IS correctly
 * wired to the test API passes and is reused exactly as intended.
 */
const DISCOVERY_PATH = "/.well-known/openid-configuration";
const DISCOVERY_TIMEOUT_MS = 15_000;

async function readIssuer(origin: string): Promise<string> {
  const url = new URL(DISCOVERY_PATH, origin);
  const response: Response = await fetch(url, {
    signal: AbortSignal.timeout(DISCOVERY_TIMEOUT_MS),
  });

  if (!response.ok) {
    throw new Error(`GET ${url.href} answered ${response.status}`);
  }

  const document: unknown = await response.json();
  const issuer: unknown = (document as { issuer?: unknown }).issuer;

  if (typeof issuer !== "string") {
    throw new TypeError(`GET ${url.href} returned no string "issuer"`);
  }

  return issuer;
}

async function assertAppTargetsTheExpectedApi(baseURL: string): Promise<void> {
  const apiURL: string | undefined = process.env.WALLOW_API_INTERNAL_URL;

  // Unset means the caller never pinned an API for this run (the container mode
  // E2E_BASE_URL selects, where the app owns its own upstream), so there is no
  // expectation to check against.
  if (apiURL === undefined || apiURL === "") {
    return;
  }

  let expected: string;
  try {
    expected = await readIssuer(apiURL);
  } catch (error: unknown) {
    // The API named by WALLOW_API_INTERNAL_URL is not reachable from here — a
    // container-internal hostname, most likely. Nothing to compare, and the
    // suite's own failures will say so far better than a guess would.
    console.warn(`e2e global setup: skipping the backend-identity check — ${String(error)}`);
    return;
  }

  const actual: string = await readIssuer(baseURL);

  if (actual !== expected) {
    throw new Error(
      [
        "e2e global setup: the app under test is proxying to the WRONG API.",
        `  app under test  ${baseURL} -> issuer ${actual}`,
        `  expected        ${apiURL} -> issuer ${expected}`,
        "",
        "A dev server was almost certainly already listening on that port and was",
        "reused (playwright.config.ts sets reuseExistingServer), keeping its own",
        "WALLOW_API_INTERNAL_URL. Stop it and re-run:",
        `  lsof -nP -iTCP:${new URL(baseURL).port} -sTCP:LISTEN`,
        "(Vite may bind IPv6 only, so curl over IPv4 can wrongly report it closed.)",
      ].join("\n"),
    );
  }
}

export default async function warmUpAppUnderTest(config: FullConfig): Promise<void> {
  const baseURL: string | undefined = config.projects[0]?.use.baseURL;

  if (baseURL === undefined) {
    throw new Error("e2e global setup: no baseURL is configured");
  }

  // Before the warm-up: it is cheap, and no amount of warming fixes a run aimed
  // at the wrong backend.
  await assertAppTargetsTheExpectedApi(baseURL);

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

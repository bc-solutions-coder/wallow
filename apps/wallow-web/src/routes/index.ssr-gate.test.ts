import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { type AnyRedirect, isRedirect } from "@tanstack/react-router";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { Route } from "./index";

/**
 * SSR-safety spec for the public home route's `beforeLoad` gate (Wallow-fqw9),
 * the sibling of the `/dashboard` fix made in Wallow-zyxe.
 *
 * When the landing page is disabled and no user is cached, the gate must
 * **throw** a TanStack `redirect({ href, reloadDocument })` to the BFF login
 * rather than calling the SDK's browser-only `login()`. That helper assigns to
 * the bare global `location`, which does not exist under Node, so a full-page
 * SSR load of `/` would throw a `ReferenceError` and the request handler would
 * surface it as HTTP 500 instead of a redirect.
 *
 * This file deliberately does NOT mock `@bc-solutions-coder/sdk` — the `login`
 * spy in `index.gate.test.tsx` masks exactly that defect. It is a `.test.ts`, so
 * it runs in the vitest NODE project (`src/**\/*.test.ts`, see the shared
 * `createVitestProjects` preset): no global `location`, i.e. the same conditions
 * as a full-page SSR render. Only `../lib/branding` is mocked, so each test can
 * flip `landingPage.enabled` — the flag that decides whether the gate runs at
 * all (it defaults to `true`, which is why this defect is dormant in the
 * default fork configuration).
 */

/** The BFF login target the gate must send a forced-login visitor to. */
const EXPECTED_LOGIN_HREF: string = "/bff/login?returnTo=%2Fdashboard%2Fapps";

// Mutable branding stand-in so each test can flip `landingPage.enabled`. Mirrors
// the shape `routes/index.tsx` and `components/PublicLayout.tsx` read.
const branding = vi.hoisted(() => ({
  forkBranding: {
    appName: "Wallow",
    appIcon: "piggy-icon.svg",
    tagline: "Wallow in it",
    repositoryUrl: "https://github.com/wallowapp/wallow",
    landingPage: { enabled: true },
  },
  appIconUrl: "/piggy-icon.svg",
}));

vi.mock("../lib/branding", () => branding);

/**
 * Drive the route's `beforeLoad` with a minimal TanStack-shaped context whose
 * `queryClient.ensureQueryData` is a spy resolving the seeded user.
 */
async function runGate(user: unknown): Promise<unknown> {
  const ensureQueryData = vi.fn().mockResolvedValue(user);
  const beforeLoad = Route.options.beforeLoad as (opts: unknown) => Promise<unknown>;
  return beforeLoad({
    location: { pathname: "/", href: "/" },
    context: { queryClient: { ensureQueryData } },
  });
}

/**
 * Run the gate expecting it to throw, and hand back whatever it threw so callers
 * can assert on the redirect (or catch a browser-only `ReferenceError`).
 */
async function catchGate(user: unknown): Promise<unknown> {
  try {
    await runGate(user);
  } catch (error: unknown) {
    return error;
  }
  throw new Error("expected the home gate to throw a redirect, but it returned normally");
}

/**
 * Assert the caught value is a TanStack redirect, narrowing it so callers can
 * read `options.href` / `options.reloadDocument` without a cast.
 */
function assertRedirect(thrown: unknown): asserts thrown is AnyRedirect {
  expect(isRedirect(thrown)).toBe(true);
}

describe("routes/index (SSR-safe home gate)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    branding.forkBranding.landingPage.enabled = false;
  });

  it("throws a TanStack redirect to the BFF login when the landing page is disabled and no user is cached", async () => {
    const thrown: unknown = await catchGate(null);

    assertRedirect(thrown);
    expect(thrown.options.href).toBe(EXPECTED_LOGIN_HREF);
    expect(thrown.headers.get("Location")).toBe(EXPECTED_LOGIN_HREF);
  });

  it("marks the BFF login redirect as a full-document navigation", async () => {
    // `/bff/login` is a BFF endpoint, not a route in the TanStack tree. Without
    // `reloadDocument`, a client-side redirect with a RELATIVE href is committed
    // through the router (`buildAndCommitLocation`) and lands on a not-found
    // match instead of reaching the BFF.
    const thrown: unknown = await catchGate(null);

    assertRedirect(thrown);
    expect(thrown.options.reloadDocument).toBe(true);
  });

  it("never touches the browser-only location global when forcing a login (SSR safety)", async () => {
    // Precondition: this spec runs in the node project, i.e. under the same
    // no-global-`location` conditions as a full-page SSR render.
    expect("location" in globalThis).toBe(false);

    const thrown: unknown = await catchGate(null);

    // A ReferenceError here is the 500: the SSR request handler surfaces any
    // non-redirect throw from `beforeLoad` as an HTTP 500.
    expect(thrown).not.toBeInstanceOf(ReferenceError);
    expect(isRedirect(thrown)).toBe(true);
  });

  it("still shows the marketing page (no throw) when the landing page is enabled", async () => {
    branding.forkBranding.landingPage.enabled = true;

    await expect(runGate(null)).resolves.toBeUndefined();
  });

  it("still redirects an authenticated visitor to the dashboard", async () => {
    branding.forkBranding.landingPage.enabled = true;

    const thrown: unknown = await catchGate({ sub: "u1", email: "user@test.local" });

    assertRedirect(thrown);
    expect(thrown.options.to).toBe("/dashboard/apps");
  });

  it("does not import the SDK's browser-only login helper", () => {
    // `login()` assigns to the bare global `location`, so it must not be
    // reachable from a hook that also runs server-side. The gate redirects
    // through TanStack's `redirect()` instead, which works on SSR and CSR alike.
    const source: string = readFileSync(
      fileURLToPath(new URL("./index.tsx", import.meta.url)),
      "utf8",
    );
    const importLines: string = (source.match(/^import .*$/gmu) ?? []).join("\n");

    expect(importLines).not.toMatch(/\blogin\b/u);
    expect(importLines).toMatch(/\bredirect\b/u);
  });
});

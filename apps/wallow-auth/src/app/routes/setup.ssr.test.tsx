import { QueryClient } from "@bc-solutions-coder/query";
import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { type AnyRedirect, isRedirect } from "@tanstack/react-router";
import { beforeEach, describe, expect, it } from "vitest";

import { Route } from "./setup";

/**
 * The `/setup` already-complete gate: `beforeLoad` reads setup status through
 * the real SDK over a fake transport and redirects to `/login` ONLY when setup
 * is definitely complete. An unreachable status endpoint renders the form —
 * the submit is where that failure belongs, where the visitor can retry it.
 */

// Under the harness's default `/api`-prefixed base URL.
const STATUS_ENDPOINT = "/api/v1/identity/setup/status";
const SERVICE_UNAVAILABLE = 503;

let harness: SdkHarness;

beforeEach(() => {
  harness = createSdkHarness();
});

/** Drive the gate with a fresh per-request query cache, returning what it threw. */
async function runGate(): Promise<unknown> {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const beforeLoad = Route.options.beforeLoad as (opts: unknown) => Promise<unknown>;
  try {
    await beforeLoad({ context: { queryClient, sdk: harness.sdk } });
  } catch (error: unknown) {
    return error;
  }
  return undefined;
}

describe("/setup route (already-complete gate)", () => {
  it("renders the setup form while setup is still required", async () => {
    harness.resolveJson({ setupRequired: true });

    const thrown: unknown = await runGate();

    expect(thrown).toBeUndefined();
    expect(harness.calls[0]?.path).toBe(STATUS_ENDPOINT);
  });

  it("redirects to /login when setup is already complete", async () => {
    harness.resolveJson({ setupRequired: false });

    const thrown: unknown = await runGate();

    expect(isRedirect(thrown)).toBe(true);
    expect((thrown as AnyRedirect).options.to).toBe("/login");
  });

  it("renders the form rather than redirecting when the status call fails", async () => {
    harness.rejectJson({ detail: "starting up" }, SERVICE_UNAVAILABLE);

    expect(await runGate()).toBeUndefined();
  });

  it("renders the form when the status call cannot reach the API at all", async () => {
    harness.respond(() => Promise.reject(new Error("connection refused")));

    expect(await runGate()).toBeUndefined();
  });

  it("has a component of its own — the gate protects a page, not a pure redirect", () => {
    expect(Route.options.component).toBeDefined();
  });
});

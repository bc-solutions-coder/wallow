import { QueryClient } from "@bc-solutions-coder/query";
import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { type AnyRedirect, isRedirect } from "@tanstack/react-router";
import { beforeEach, describe, expect, it } from "vitest";

import { Route } from "./login";

/**
 * The `/login` first-run gate: `beforeLoad` reads setup status through the
 * real SDK over a fake transport and redirects to `/setup` ONLY when setup is
 * definitely still required. Complete, failed, and unreachable all render the
 * login page — a status hiccup must never take sign-in down.
 */

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

describe("/login route (first-run gate)", () => {
  it("redirects to /setup while setup is still required", async () => {
    harness.resolveJson({ setupRequired: true });

    const thrown: unknown = await runGate();

    expect(isRedirect(thrown)).toBe(true);
    expect((thrown as AnyRedirect).options.to).toBe("/setup");
  });

  it("renders the login page once setup is complete", async () => {
    harness.resolveJson({ setupRequired: false });

    expect(await runGate()).toBeUndefined();
  });

  it("renders the login page rather than redirecting when the status call fails", async () => {
    harness.rejectJson({ detail: "starting up" }, SERVICE_UNAVAILABLE);

    expect(await runGate()).toBeUndefined();
  });

  it("renders the login page when the status call cannot reach the API at all", async () => {
    harness.respond(() => Promise.reject(new Error("connection refused")));

    expect(await runGate()).toBeUndefined();
  });
});

import { setCsrfToken } from "@bc-solutions-coder/sdk";
import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { page, userEvent } from "vitest/browser";

import { getRouter } from "../router";
import { Route } from "./bff-demo";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Route spec for the dedicated BFF smoke/demo route (Wallow-8w1h.8.2).
 *
 * The C# `BffFlowTests` (api/tests/Wallow.E2E.Tests/Flows/BffFlowTests.cs) drives
 * the BFF example through a `data-testid` DOM contract:
 *   - bff-user-status   ("anonymous" | "authenticated")
 *   - bff-user-email    (authenticated user's email)
 *   - bff-login         (button -> login("/"))
 *   - bff-logout        (button -> logout())
 *   - bff-call-api      (button -> GET usersGetCurrentUser() through /api)
 *   - bff-mutate        (button -> POST organizationsCreate() with CSRF)
 *   - bff-api-result    (result of the last safe /api call)
 *   - bff-mutate-result (result of the last mutation)
 *
 * This route is the React port of the deleted vanilla `src/app.ts` demo. It lives
 * at a DEDICATED `/bff-demo` route rather than overwriting `src/routes/index.tsx`,
 * whose `home-heading` SSR contract (Wallow-8w1h.2.2) must remain intact.
 *
 * NOTHING here mocks the SDK's generated operations any more (Wallow-pu6a.5.5).
 * The route takes its client off the router context, so `renderWithWallow` hands
 * it a REAL `createWallowSdk()` instance over the harness transport: the CSRF
 * interceptor, the error interceptor and the response parsing the app ships all
 * execute, and the spec asserts on the request that actually went out.
 *
 * Two seams remain, both of them the browser rather than the SDK:
 *   - `/bff/user` is not a generated operation — `getUser()` reads it with the
 *     global `fetch`, so the global is what gets stubbed;
 *   - `login()`/`logout()` navigate by assigning to `location`, which would take
 *     the test iframe with them. They are overridden as render-nothing sentinels
 *     over `importOriginal`, the same narrow navigation/SSR-isolation exception
 *     `.claude/rules/TESTING.md` grants the `__root*.test.tsx` specs — every other
 *     export, generated ops included, stays real.
 */

const navigationMocks = vi.hoisted(() => ({ login: vi.fn(), logout: vi.fn() }));

vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, ...navigationMocks };
});

/** A signed-in `/bff/user` body: identity claims plus the session's CSRF token. */
const SIGNED_IN_USER = { sub: "u1", email: "user@test.local", csrfToken: "csrf-abc" };

const UNAUTHORIZED = 401;
const FORBIDDEN = 403;

/** Stub the global `fetch` that `getUser()` uses for `/bff/user`. */
function stubBffUser(body: unknown, status: number = 200): void {
  vi.spyOn(globalThis, "fetch").mockResolvedValue(
    status === UNAUTHORIZED ? new Response(null, { status }) : Response.json(body, { status }),
  );
}

const ALL_TESTIDS: readonly string[] = [
  "bff-user-status",
  "bff-user-email",
  "bff-login",
  "bff-logout",
  "bff-call-api",
  "bff-mutate",
  "bff-api-result",
  "bff-mutate-result",
];

describe("routes/bff-demo (BFF smoke surface)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // The token store is module-global; a token armed by one test must not leak
    // into the next one's "anonymous" assertions.
    setCsrfToken(null);
    harness = createSdkHarness();
    stubBffUser(null, UNAUTHORIZED);
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  function renderDemo() {
    const Page = Route.options.component!;
    return renderWithWallow(<Page />, { harness });
  }

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("renders the full bff-* testid contract the E2E BffFlowTests drives", async () => {
    renderDemo();
    for (const testId of ALL_TESTIDS) {
      await expect.element(page.getByTestId(testId)).toBeInTheDocument();
    }
  });

  it("shows 'anonymous' status when /bff/user answers 401", async () => {
    renderDemo();
    await expect.element(page.getByTestId("bff-user-status")).toHaveTextContent("anonymous");
  });

  it("paints 'authenticated' + email for a signed-in user", async () => {
    stubBffUser(SIGNED_IN_USER);
    renderDemo();

    await expect.element(page.getByTestId("bff-user-status")).toHaveTextContent("authenticated");
    await expect.element(page.getByTestId("bff-user-email")).toHaveTextContent("user@test.local");
  });

  it('clicking bff-login triggers login("/")', async () => {
    renderDemo();

    await userEvent.click(page.getByTestId("bff-login"));
    expect(navigationMocks.login).toHaveBeenCalledWith("/");
  });

  it("clicking bff-logout triggers logout()", async () => {
    renderDemo();

    await userEvent.click(page.getByTestId("bff-logout"));
    expect(navigationMocks.logout).toHaveBeenCalled();
  });

  it("clicking bff-call-api GETs the user through /api and renders the resolved body", async () => {
    harness.resolveJson({ id: "u1", email: "user@test.local" });
    renderDemo();

    await userEvent.click(page.getByTestId("bff-call-api"));
    await expect.element(page.getByTestId("bff-api-result")).toHaveTextContent("user@test.local");
    expect(harness.last?.method).toBe("GET");
  });

  it("clicking bff-mutate posts an org, echoing the CSRF token /bff/user handed out", async () => {
    stubBffUser(SIGNED_IN_USER);
    harness.resolveJson({ organizationId: "org-123" });
    renderDemo();
    // The token is armed by the mount effect, so wait for it to land before the
    // POST — this is the whole point of the demo's CSRF path.
    await expect.element(page.getByTestId("bff-user-status")).toHaveTextContent("authenticated");

    await userEvent.click(page.getByTestId("bff-mutate"));

    await expect
      .element(page.getByTestId("bff-mutate-result"))
      .toHaveTextContent("created org org-123");
    expect(harness.last?.method).toBe("POST");
    expect(harness.last?.headers["x-csrf-token"]).toBe("csrf-abc");
  });

  it("renders the status and title of a refused mutation", async () => {
    harness.rejectJson({ title: "CSRF token missing", status: FORBIDDEN }, FORBIDDEN);
    renderDemo();

    await userEvent.click(page.getByTestId("bff-mutate"));
    await expect
      .element(page.getByTestId("bff-mutate-result"))
      .toHaveTextContent("403 CSRF token missing");
  });
});

/**
 * Router-registration spec: `src/router.tsx` must bind the `/bff-demo` route
 * under the root (like the other manually-bound routes), so the smoke surface is
 * reachable. Mirrors the registration assertions in the dashboard route tests.
 */
describe("routes/bff-demo (router registration)", () => {
  it("registers /bff-demo in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/bff-demo");
  });
});

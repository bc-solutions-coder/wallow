import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { page, userEvent } from "vitest/browser";

import { getRouter } from "@app/router";
import { Route } from "./bff-demo";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * The `/bff-demo` smoke surface and its `bff-*` testid contract.
 *
 * Nothing here mocks the SDK's generated operations: the route takes its client
 * off the router context, so `renderWithWallow` hands it a REAL SDK over the
 * harness transport and the spec asserts on the request that actually went out.
 * Two browser seams stay stubbed — `/bff/user` is read with the global `fetch`,
 * and `logout()` assigns to `location`, taking the test iframe with it. Login
 * is a plain anchor, asserted by its `href`.
 */

const navigationMocks = vi.hoisted(() => ({ logout: vi.fn() }));

vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, ...navigationMocks };
});

/** A signed-in `/bff/user` body: identity claims plus the session's CSRF token. */
const SIGNED_IN_USER = { sub: "u1", email: "user@test.local", csrfToken: "csrf-abc" };

const UNAUTHORIZED = 401;
const FORBIDDEN = 403;

/** Stub the global `fetch` the mount effect reads `/bff/user` with. */
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
    harness = createSdkHarness();
    stubBffUser(null, UNAUTHORIZED);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    // This suite runs in a REAL browser, so the double-submit cookie the mutate
    // spec writes lives in a real jar — clear it so no other test inherits it.
    document.cookie = "wallow_bff-csrf=; max-age=0";
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

  it('renders bff-login as a document link to the BFF login with returnTo "/"', async () => {
    // Asserted by href rather than clicked: following the link would navigate
    // the test iframe away.
    renderDemo();

    await expect
      .element(page.getByTestId("bff-login"))
      .toHaveAttribute("href", "/bff/login?returnTo=%2F");
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

  it("clicking bff-mutate posts an org, echoing the double-submit cookie's CSRF token", async () => {
    stubBffUser(SIGNED_IN_USER);
    harness.resolveJson({ organizationId: "org-123" });
    // The BFF writes this non-HttpOnly cookie at login; the SDK's interceptor
    // reads it back at request time. Written into the REAL jar this suite runs
    // against — there is no module store to arm (Wallow-j7qk).
    document.cookie = "wallow_bff-csrf=csrf-abc";
    renderDemo();
    await expect.element(page.getByTestId("bff-user-status")).toHaveTextContent("authenticated");

    await userEvent.click(page.getByTestId("bff-mutate"));

    await expect
      .element(page.getByTestId("bff-mutate-result"))
      .toHaveTextContent("created org org-123");
    expect(harness.last?.method).toBe("POST");
    expect(harness.last?.headers["x-csrf-token"]).toBe("csrf-abc");
  });

  it("renders the status and title of a refused mutation", async () => {
    harness.rejectJson(
      { code: "Bff.CsrfInvalid", title: "CSRF token missing", status: FORBIDDEN },
      FORBIDDEN,
    );
    renderDemo();

    await userEvent.click(page.getByTestId("bff-mutate"));
    await expect
      .element(page.getByTestId("bff-mutate-result"))
      .toHaveTextContent("403 CSRF token missing");
  });
});

describe("routes/bff-demo (router registration)", () => {
  it("registers /bff-demo in the router tree", () => {
    const router = getRouter();
    const paths = Object.keys(router.routesByPath);
    expect(paths).toContain("/bff-demo");
  });
});

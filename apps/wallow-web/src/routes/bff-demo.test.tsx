import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { createRouter } from "../router";
import { Route } from "./bff-demo";

/**
 * Route spec for the dedicated BFF smoke/demo route (Wallow-8w1h.8.2).
 *
 * The C# `BffFlowTests` (api/tests/Wallow.E2E.Tests/Flows/BffFlowTests.cs) drives
 * the BFF example through a `data-testid` DOM contract:
 *   - bff-user-status   ("anonymous" | "authenticated")
 *   - bff-user-email    (authenticated user's email)
 *   - bff-login         (button -> login("/"))
 *   - bff-logout        (button -> logout())
 *   - bff-call-api      (button -> GET getV1IdentityUsersMe() through /api)
 *   - bff-mutate        (button -> POST postV1IdentityOrganizations() with CSRF)
 *   - bff-api-result    (result of the last safe /api call, contains "200")
 *   - bff-mutate-result (result of the last mutation, contains "201 created org")
 *
 * This route is the React port of `src/app.ts` + `public/index.html`. It lives at
 * a DEDICATED `/bff-demo` route rather than overwriting `src/routes/index.tsx`,
 * whose `home-heading` SSR contract (Wallow-8w1h.2.2) must remain intact. The old
 * `server.ts`/`public/` static path that the Docker `bff-example` container still
 * serves is left untouched (guarded by `src/bff-surface.test.ts`); retargeting
 * the container to this route is Phase 8's job, not 8.2's.
 *
 * The demo port imports directly from `@bc-solutions-coder/sdk` (as `src/app.ts`
 * does — the raw BFF example is exempt from the `getWallowSdk()`-only convention),
 * so we mock those generated ops here.
 */

// Hoisted spies shared between the mock factories and the test bodies.
const sdkMocks = vi.hoisted(() => ({
  configureBffClient: vi.fn(),
  getUser: vi.fn(),
  login: vi.fn(),
  logout: vi.fn(),
  getV1IdentityUsersMe: vi.fn(),
  postV1IdentityOrganizations: vi.fn(),
  client: { interceptors: { request: { use: vi.fn() } } },
}));

const csrfMocks = vi.hoisted(() => ({
  setCsrfToken: vi.fn(),
  wireCsrfInterceptor: vi.fn(),
}));

// Override only the SDK ops the demo drives; keep every other export intact so
// the rest of the route graph (built by `createRouter`) still resolves. The CSRF
// token store/interceptor (now SDK-owned) is a no-op under test; `refreshUser`
// must still arm it via `setCsrfToken(user.csrfToken)` on a non-null user.
vi.mock("@bc-solutions-coder/sdk", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@bc-solutions-coder/sdk")>();
  return { ...actual, ...sdkMocks, ...csrfMocks };
});

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderDemo(ui: ReactElement) {
  return render(<QueryClientProvider client={newClient()}>{ui}</QueryClientProvider>);
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
    // Anonymous by default; individual tests override.
    sdkMocks.getUser.mockResolvedValue(null);
    sdkMocks.getV1IdentityUsersMe.mockResolvedValue({
      data: { id: "u1", email: "user@test.local" },
      error: undefined,
      response: { status: 200, statusText: "OK" },
    });
    sdkMocks.postV1IdentityOrganizations.mockResolvedValue({
      data: { organizationId: "org-123" },
      error: undefined,
      response: { status: 201, statusText: "Created" },
    });
  });

  it("exposes a route component", () => {
    expect(Route.options.component).toBeDefined();
  });

  it("renders the full bff-* testid contract the E2E BffFlowTests drives", async () => {
    const Page = Route.options.component!;
    renderDemo(<Page />);
    for (const testId of ALL_TESTIDS) {
      await expect.element(page.getByTestId(testId)).toBeInTheDocument();
    }
  });

  it("shows 'anonymous' status when getUser resolves null", async () => {
    sdkMocks.getUser.mockResolvedValue(null);
    const Page = Route.options.component!;
    renderDemo(<Page />);
    await expect.element(page.getByTestId("bff-user-status")).toHaveTextContent("anonymous");
  });

  it("paints 'authenticated' + email and arms the CSRF token for a signed-in user", async () => {
    sdkMocks.getUser.mockResolvedValue({
      sub: "u1",
      email: "user@test.local",
      csrfToken: "csrf-abc",
    });
    const Page = Route.options.component!;
    renderDemo(<Page />);

    await expect.element(page.getByTestId("bff-user-status")).toHaveTextContent("authenticated");
    await expect.element(page.getByTestId("bff-user-email")).toHaveTextContent("user@test.local");
    expect(csrfMocks.setCsrfToken).toHaveBeenCalledWith("csrf-abc");
  });

  it('clicking bff-login triggers login("/")', async () => {
    const Page = Route.options.component!;
    renderDemo(<Page />);

    await userEvent.click(page.getByTestId("bff-login"));
    expect(sdkMocks.login).toHaveBeenCalledWith("/");
  });

  it("clicking bff-logout triggers logout()", async () => {
    const Page = Route.options.component!;
    renderDemo(<Page />);

    await userEvent.click(page.getByTestId("bff-logout"));
    expect(sdkMocks.logout).toHaveBeenCalled();
  });

  it("clicking bff-call-api renders the 200 status in bff-api-result", async () => {
    const Page = Route.options.component!;
    renderDemo(<Page />);

    await userEvent.click(page.getByTestId("bff-call-api"));
    await expect.element(page.getByTestId("bff-api-result")).toHaveTextContent("200");
    expect(sdkMocks.getV1IdentityUsersMe).toHaveBeenCalled();
  });

  it("clicking bff-mutate posts an org and renders '201 created org' in bff-mutate-result", async () => {
    const Page = Route.options.component!;
    renderDemo(<Page />);

    await userEvent.click(page.getByTestId("bff-mutate"));
    await expect
      .element(page.getByTestId("bff-mutate-result"))
      .toHaveTextContent("201 created org");
    expect(sdkMocks.postV1IdentityOrganizations).toHaveBeenCalled();
  });
});

/**
 * Router-registration spec: `src/router.tsx` must bind the `/bff-demo` route
 * under the root (like the other manually-bound routes), so the smoke surface is
 * reachable. Mirrors the registration assertions in the dashboard route tests.
 */
describe("routes/bff-demo (router registration)", () => {
  it("registers /bff-demo in the router tree", () => {
    const router = createRouter();
    const paths = Object.keys((router as { routesByPath: Record<string, unknown> }).routesByPath);
    expect(paths).toContain("/bff-demo");
  });
});

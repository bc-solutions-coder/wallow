import { beforeEach, describe, expect, it, vi } from "vitest";

import type { WallowAuthSdk } from "./wallow-auth-sdk";

/**
 * `getWallowAuthSdk()` facade (Wallow-vec7.2.3) — the guarded singleton that
 * composes the SDK auth facade (Wallow-vec7.2.1) and the OIDC URL helpers
 * (Wallow-vec7.2.2) behind one same-origin client, with CSRF wired.
 *
 * The SDK is mocked here because this facade is the ONLY module in the app
 * permitted to import it; these tests assert COMPOSITION (same instance, both
 * slices present, client configured once, interceptor wired once) and the CSRF
 * header contract — not the wire, and not the SDK's own behaviour, which
 * .2.1/.2.2 already pin.
 *
 * The SDK's csrf module (`wireCsrfInterceptor`/`setCsrfToken`/`isSafeMethod`) is
 * deliberately delegated to the REAL implementation inside the SDK mock: the
 * acceptance criterion is that the interceptor is really attached and really
 * injects the header on a mutating request, which only the real interceptor can
 * show.
 */

/** Every method the SDK's `AuthClient` exposes; `.auth` must surface them all. */
const AUTH_METHODS: readonly string[] = [
  "login",
  "register",
  "forgotPassword",
  "resetPassword",
  "verifyEmail",
  "sendMagicLink",
  "verifyMagicLink",
  "sendOtp",
  "verifyOtp",
  "verifyMfa",
  "useBackupCode",
  "enrollTotp",
  "confirmEnrollment",
  "exchangeEnrollmentToken",
  "getConsentInfo",
  "getExternalProviders",
  "getClientTenant",
  "getClientBranding",
  "verifyInvitation",
  "acceptInvitation",
  "validateRedirectUri",
  // The auth-state seam (Wallow-vec7.2.4). `.auth` is the SDK's AuthClient
  // wholesale, so this rides along with no wiring change — pinned here so the
  // inventory stays honest and screens can rely on reaching it through `.auth`.
  "getCurrentUser",
];

/** The five non-spec OIDC helpers the SDK exports; `.oidc` must surface them all. */
const OIDC_HELPERS: readonly string[] = [
  "isSafeReturnUrl",
  "buildConnectAuthorizeUrl",
  "buildConsentSubmitUrl",
  "buildExchangeTicketUrl",
  "buildConnectLogoutUrl",
];

// Hoisted so the vi.mock factory and the test bodies share the same spies. The
// factory re-runs after each vi.resetModules() but hands back these same
// objects, so identity assertions below stay meaningful across fresh graphs.
const mocks = vi.hoisted(() => {
  const authClient: Record<string, unknown> = {};
  for (const name of [
    "login",
    "register",
    "forgotPassword",
    "resetPassword",
    "verifyEmail",
    "sendMagicLink",
    "verifyMagicLink",
    "sendOtp",
    "verifyOtp",
    "verifyMfa",
    "useBackupCode",
    "enrollTotp",
    "confirmEnrollment",
    "exchangeEnrollmentToken",
    "getConsentInfo",
    "getExternalProviders",
    "getClientTenant",
    "getClientBranding",
    "verifyInvitation",
    "acceptInvitation",
    "validateRedirectUri",
    "getCurrentUser",
  ]) {
    authClient[name] = vi.fn();
  }

  // Real-behaviour CSRF store + interceptor, delegated into the SDK mock so the
  // facade wires a GENUINELY working interceptor that reads the token live (the
  // SDK's own `csrf.test.ts` pins the header contract in isolation; here we only
  // need to prove the facade attaches it and keeps the store in sync). The token
  // lives in this hoisted closure so `setCsrfToken` and the wired interceptor
  // share one instance across `vi.resetModules()`; `resetCsrf` clears it so each
  // `freshFacade()` starts from the anonymous, token-less state.
  const safeMethods: ReadonlySet<string> = new Set(["GET", "HEAD", "OPTIONS"]);
  let csrfToken: string | null = null;

  return {
    authClient,
    createAuthClient: vi.fn(() => authClient),
    // Real (passthrough) guard: the collapsed facade builds getWallowAuthSdk via
    // the SDK's createConfiguredOnce, so the mock supplies a working lazy
    // singleton. A fresh closure per module graph is what freshFacade()'s
    // vi.resetModules relies on for a clean singleton each test.
    createConfiguredOnce: <TFacade>(
      configure: () => void,
      build: () => TFacade,
    ): (() => TFacade) => {
      let facade: TFacade | undefined;
      let ready = false;
      return (): TFacade => {
        if (!ready) {
          configure();
          facade = build();
          ready = true;
        }
        return facade as TFacade;
      };
    },
    configureBffClient: vi.fn(),
    client: { interceptors: { request: { use: vi.fn() } } },
    isSafeReturnUrl: vi.fn(),
    buildConnectAuthorizeUrl: vi.fn(),
    buildConsentSubmitUrl: vi.fn(),
    buildExchangeTicketUrl: vi.fn(),
    buildConnectLogoutUrl: vi.fn(),
    isSafeMethod: (method: string): boolean => safeMethods.has(method.toUpperCase()),
    setCsrfToken: (token: string | null): void => {
      csrfToken = token;
    },
    wireCsrfInterceptor: (client: {
      interceptors: { request: { use: (interceptor: (request: Request) => Request) => void } };
    }): void => {
      client.interceptors.request.use((request: Request): Request => {
        if (csrfToken !== null && !safeMethods.has(request.method.toUpperCase())) {
          request.headers.set("x-csrf-token", csrfToken);
        }
        return request;
      });
    },
    resetCsrf: (): void => {
      csrfToken = null;
    },
  };
});

vi.mock("@bc-solutions-coder/sdk", () => ({
  createAuthClient: mocks.createAuthClient,
  createConfiguredOnce: mocks.createConfiguredOnce,
  configureBffClient: mocks.configureBffClient,
  client: mocks.client,
  isSafeReturnUrl: mocks.isSafeReturnUrl,
  buildConnectAuthorizeUrl: mocks.buildConnectAuthorizeUrl,
  buildConsentSubmitUrl: mocks.buildConsentSubmitUrl,
  buildExchangeTicketUrl: mocks.buildExchangeTicketUrl,
  buildConnectLogoutUrl: mocks.buildConnectLogoutUrl,
  isSafeMethod: mocks.isSafeMethod,
  setCsrfToken: mocks.setCsrfToken,
  wireCsrfInterceptor: mocks.wireCsrfInterceptor,
}));

/**
 * Re-evaluate the facade module so its guarded-singleton state starts fresh,
 * then hand back the entry point AND the `setCsrfToken` from the SAME module
 * graph.
 *
 * The CSRF store now lives in the SDK mock's hoisted closure (stable across
 * `vi.resetModules()`), so `mocks.setCsrfToken` and the interceptor the facade
 * wires always share one token instance — no cross-graph trap. Each call clears
 * that token via `resetCsrf()` so every test starts anonymous/token-less.
 */
async function freshFacade(): Promise<{
  getWallowAuthSdk: () => WallowAuthSdk;
  setCsrfToken: (token: string | null) => void;
}> {
  vi.resetModules();
  mocks.resetCsrf();
  const facade = await import("./wallow-auth-sdk");
  return { getWallowAuthSdk: facade.getWallowAuthSdk, setCsrfToken: mocks.setCsrfToken };
}

/**
 * The request interceptor the facade registered on the shared client, read back
 * off the `use` spy. Fails loudly when nothing was wired, so the "interceptor is
 * attached" criterion cannot pass by accident.
 */
function registeredInterceptor(): (request: Request) => Request {
  const calls = mocks.client.interceptors.request.use.mock.calls;
  if (calls.length === 0) {
    throw new Error("no request interceptor was registered on the SDK client");
  }
  return calls[0][0] as (request: Request) => Request;
}

beforeEach(() => {
  vi.clearAllMocks();
});

describe("getWallowAuthSdk", () => {
  it("returns the same instance on repeat calls", async () => {
    const { getWallowAuthSdk } = await freshFacade();

    expect(getWallowAuthSdk()).toBe(getWallowAuthSdk());
  });

  it("exposes an auth slice and an oidc slice", async () => {
    const { getWallowAuthSdk } = await freshFacade();

    const sdk = getWallowAuthSdk();

    expect(sdk.auth).toBeDefined();
    expect(sdk.oidc).toBeDefined();
  });

  it("builds the auth slice from the SDK's createAuthClient", async () => {
    const { getWallowAuthSdk } = await freshFacade();

    expect(getWallowAuthSdk().auth).toBe(mocks.authClient);
    expect(mocks.createAuthClient).toHaveBeenCalledTimes(1);
  });

  it.each(AUTH_METHODS)("exposes auth.%s", async (method: string) => {
    const { getWallowAuthSdk } = await freshFacade();

    expect(typeof (getWallowAuthSdk().auth as unknown as Record<string, unknown>)[method]).toBe(
      "function",
    );
  });

  it.each(OIDC_HELPERS)("exposes oidc.%s, delegating to the SDK export", async (helper: string) => {
    const { getWallowAuthSdk } = await freshFacade();

    const oidc = getWallowAuthSdk().oidc as unknown as Record<string, unknown>;

    expect(oidc[helper]).toBe((mocks as unknown as Record<string, unknown>)[helper]);
  });

  it("does not create a second auth client on repeat calls", async () => {
    const { getWallowAuthSdk } = await freshFacade();

    getWallowAuthSdk();
    getWallowAuthSdk();
    getWallowAuthSdk();

    expect(mocks.createAuthClient).toHaveBeenCalledTimes(1);
  });
});

describe("client configuration", () => {
  it("configures the shared client for the same origin ('/')", async () => {
    // wallow-auth's h3 server reverse-proxies /v1/** and /connect/** at the
    // ROOT — it is not a BFF mounted under /api like wallow-web, so the SDK's
    // '/api' default would send every call to a path that does not exist.
    const { getWallowAuthSdk } = await freshFacade();

    getWallowAuthSdk();

    expect(mocks.configureBffClient).toHaveBeenCalledWith({ baseUrl: "/" });
  });

  it("configures the client exactly once across repeat calls", async () => {
    const { getWallowAuthSdk } = await freshFacade();

    getWallowAuthSdk();
    getWallowAuthSdk();

    expect(mocks.configureBffClient).toHaveBeenCalledTimes(1);
  });

  it("does not configure the client until the facade is first used", async () => {
    await freshFacade();

    expect(mocks.configureBffClient).not.toHaveBeenCalled();
  });
});

describe("CSRF wiring", () => {
  it("registers exactly one request interceptor on the shared SDK client", async () => {
    const { getWallowAuthSdk } = await freshFacade();

    getWallowAuthSdk();

    expect(mocks.client.interceptors.request.use).toHaveBeenCalledTimes(1);
  });

  it("wires the interceptor exactly once across repeat calls", async () => {
    const { getWallowAuthSdk } = await freshFacade();

    getWallowAuthSdk();
    getWallowAuthSdk();
    getWallowAuthSdk();

    expect(mocks.client.interceptors.request.use).toHaveBeenCalledTimes(1);
  });

  it("injects the x-csrf-token header on a mutating request once a token is set", async () => {
    const { getWallowAuthSdk, setCsrfToken } = await freshFacade();

    getWallowAuthSdk();
    setCsrfToken("tok-abc");

    const request = new Request("https://auth.test/v1/identity/auth/login", { method: "POST" });
    const result = registeredInterceptor()(request);

    expect(result.headers.get("x-csrf-token")).toBe("tok-abc");
  });

  it("leaves safe requests untouched even when a token is set", async () => {
    const { getWallowAuthSdk, setCsrfToken } = await freshFacade();

    getWallowAuthSdk();
    setCsrfToken("tok-abc");

    const request = new Request("https://auth.test/v1/identity/auth/external-providers", {
      method: "GET",
    });
    const result = registeredInterceptor()(request);

    expect(result.headers.get("x-csrf-token")).toBeNull();
  });

  it("leaves mutating requests unstamped while no token is set", async () => {
    // The pre-login state wallow-auth spends most of its life in: there is no
    // session yet, so there is no token to echo and the request must still go.
    const { getWallowAuthSdk } = await freshFacade();

    getWallowAuthSdk();

    const request = new Request("https://auth.test/v1/identity/auth/login", { method: "POST" });
    const result = registeredInterceptor()(request);

    expect(result.headers.get("x-csrf-token")).toBeNull();
  });

  it("applies a token set after the singleton was created", async () => {
    const { getWallowAuthSdk, setCsrfToken } = await freshFacade();

    getWallowAuthSdk();
    getWallowAuthSdk();
    setCsrfToken("tok-late");

    const request = new Request("https://auth.test/v1/identity/auth/mfa/verify", {
      method: "POST",
    });

    expect(registeredInterceptor()(request).headers.get("x-csrf-token")).toBe("tok-late");
  });
});

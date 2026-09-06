/**
 * Public export-surface contract for the package's two entry points.
 *
 * The browser entry (`.` -> src/index.ts) and the server entry (`./server` ->
 * src/server/index.ts) are what consumers import. Deep internal paths are not
 * part of the contract, so everything a consumer needs must be reachable from
 * these two modules. These tests pin that surface: value exports at runtime,
 * type exports via `tsc --noEmit`, and the browser/server split (no server-only
 * symbol may leak into the browser bundle).
 */

import { execFileSync } from "node:child_process";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import * as browserEntry from "./index";
import * as serverEntry from "./server/index";

// Type-only imports from the public entry points. These are erased at runtime,
// so a missing type export does not break these tests directly — it breaks the
// `tsc --noEmit` test at the bottom of this file, which is the assertion that
// actually pins the type surface.
import type { CreateWallowSdkOptions, CsrfInterceptorClient, WallowSdk, WallowUser } from "./index";
import type {
  BffConfig,
  BffHandlers,
  BffSession,
  BffUserResponse,
  CookieSessionStoreOptions,
  ForwardRequest,
  ForwardResult,
  RedisLike,
  SessionStore,
  ValkeySessionStoreOptions,
} from "./server/index";

const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");

/** Value exports the browser entry must expose. */
const BROWSER_VALUE_EXPORTS: readonly string[] = [
  "buildConnectAuthorizeUrl",
  "buildConnectLogoutUrl",
  "buildConsentSubmission",
  "buildExchangeTicketUrl",
  "CONSENT_DECISION_FIELD",
  "CONSENT_DENIED",
  "CONSENT_GRANTED",
  "CONSENT_TOKEN_FIELD",
  "createWallowSdk",
  "getCurrentUser",
  "isSafeMethod",
  "isSafeReturnUrl",
  "loginRedirect",
  "logout",
  "readCsrfCookie",
  "requireAuth",
  "validateRedirectUriArgs",
  "wireCsrfInterceptor",
];

/**
 * Symbols the hand-written query layer / client singleton used to export
 * (Wallow-pu6a.5.5). They are deleted, not deprecated: a consumer reaching for
 * one must get a build error rather than a silently unconfigured module-global
 * client, so the browser entry has to keep NOT exporting them.
 */
const DELETED_LEGACY_SYMBOLS: readonly string[] = [
  "client",
  "configureBffClient",
  "configureWallowClient",
  "configureSsrClient",
  "createConfiguredOnce",
  "createMfaClient",
  "getSsrRequestContext",
  // The imperative navigation/fetch helpers Wallow-j7qk retired: login is a
  // plain `loginRedirect()` link and the current user comes from
  // `getCurrentUser`/`currentUserQuery`. Only `logout` remains imperative —
  // `/bff/logout` is CSRF-gated, so it cannot be a link.
  "getUser",
  "login",
  // The browser claim-bag readers (`claims.ts`), deleted with them (Wallow-j7qk
  // item 2): the ONE user model at the app boundary is the typed `CurrentUser`,
  // so role gating goes through `@bc-solutions-coder/auth`'s
  // `hasRole`/`isAdmin`, and OIDC claim decoding is internal to the server
  // entry (`server/claims.ts`).
  "getOrgId",
  "getOrgName",
  "getRoles",
  "hasRole",
  "isAdmin",
  "isGlobalAdmin",
  "isOperator",
  // The module-scope CSRF token store (Wallow-j7qk item 5): process-global
  // during SSR — the exact cross-user hazard `create-sdk.ts` exists to prevent
  // — and strictly redundant in the browser, where the BFF's non-HttpOnly
  // double-submit cookie is the ONE token source (`readCsrfCookie`).
  "getCsrfToken",
  "setCsrfToken",
  "setSsrRequestContextResolver",
  // Every operation's failure path raises an `ApiFailure`, so nothing unwraps.
  "unwrap",
  "wireSsrCookieInterceptor",
  // The SDK has no error type of its own: `ApiFailure` / `isApiFailure` come
  // from `@bc-solutions-coder/api-errors`. Deleted, not aliased: a consumer
  // importing these must get a build error, not a second failure type that the
  // package's brand check would not recognise.
  "isWallowError",
  "WallowError",
];

/**
 * Server-entry symbols the SDK must not export: an upstream body is parsed by
 * `@bc-solutions-coder/api-errors`' `failureFromResponse`, and a body without a
 * code is `Client.UnrecognizedResponse`, not `UNKNOWN`.
 */
const DELETED_SERVER_SYMBOLS: readonly string[] = [
  "CSRF_INVALID_CODE",
  "isWallowError",
  "NETWORK_ERROR_CODE",
  "NETWORK_TIMEOUT_CODE",
  "parseProblemDetails",
  "SESSION_REFRESH_FAILED_CODE",
  "UNKNOWN_ERROR_CODE",
  "WallowError",
];

/**
 * Server-only symbols that must never be reachable from the browser entry —
 * they pull in node/h3/openid-client and have no business in a browser bundle.
 */
const SERVER_ONLY_SYMBOLS: readonly string[] = [
  "CookieSessionStore",
  "ValkeySessionStore",
  "createApiProxy",
  "createBffHandlers",
  "loadBffConfigFromEnv",
  "readSession",
  "writeSession",
];

/** Value exports the server entry must expose. */
const SERVER_VALUE_EXPORTS: readonly string[] = [
  // stores
  "CookieSessionStore",
  "ValkeySessionStore",
  // errors
  "redact",
  "REDACTED",
  "RefreshFailedError",
  // handlers
  "createBffHandlers",
  "readSession",
  "readSessionRef",
  "writeSession",
  "writeSessionRef",
  // proxy
  "createApiProxy",
  "ensureFreshSession",
  "forceRefreshSession",
  "forwardWithResilience",
  "FORWARD_TIMEOUT_MS",
  "MAX_RETRY_AFTER_MS",
  // problem
  "problemResponse",
  // csrf
  "CSRF_HEADER",
  // config
  "loadBffConfigFromEnv",
  "DEFAULT_SESSION_TTL_SECONDS",
];

describe("browser entry (package root export)", () => {
  it.each(BROWSER_VALUE_EXPORTS)("exports %s", (name: string) => {
    expect(Object.keys(browserEntry)).toContain(name);
    expect((browserEntry as unknown as Record<string, unknown>)[name]).toBeDefined();
  });

  it.each(DELETED_LEGACY_SYMBOLS)("no longer exports the retired %s", (name: string) => {
    expect(Object.keys(browserEntry)).not.toContain(name);
  });

  it.each(SERVER_ONLY_SYMBOLS)("does not leak server-only %s", (name: string) => {
    expect(Object.keys(browserEntry)).not.toContain(name);
  });
});

describe("server entry (./server subpath export)", () => {
  it.each(SERVER_VALUE_EXPORTS)("exports %s", (name: string) => {
    expect(Object.keys(serverEntry)).toContain(name);
    expect((serverEntry as unknown as Record<string, unknown>)[name]).toBeDefined();
  });

  it.each(DELETED_SERVER_SYMBOLS)("no longer exports the retired %s", (name: string) => {
    expect(Object.keys(serverEntry)).not.toContain(name);
  });

  it("exports session stores as constructible SessionStore implementations", () => {
    const password: string = "a".repeat(32);
    const redis: RedisLike = {
      get: () => Promise.resolve(null),
      set: () => Promise.resolve("OK" as const),
      del: () => Promise.resolve(0),
      sadd: () => Promise.resolve(1),
      srem: () => Promise.resolve(0),
      smembers: () => Promise.resolve([]),
      expire: () => Promise.resolve(),
    };

    const cookieStore: SessionStore = new serverEntry.CookieSessionStore({
      password,
    } satisfies CookieSessionStoreOptions);
    const valkeyStore: SessionStore = new serverEntry.ValkeySessionStore({
      client: redis,
      password,
    } satisfies ValkeySessionStoreOptions);

    for (const store of [cookieStore, valkeyStore]) {
      expect(typeof store.read).toBe("function");
      expect(typeof store.write).toBe("function");
      expect(typeof store.destroy).toBe("function");
      expect(typeof store.withRefreshLock).toBe("function");
    }
  });

  it("exports RefreshFailedError as a real Error subclass", () => {
    const error: Error = new serverEntry.RefreshFailedError();
    expect(error).toBeInstanceOf(Error);
  });
});

describe("public type surface", () => {
  // Type-level pins. These compile-time references are what `tsc --noEmit`
  // below validates; they have no runtime effect.
  it("references every publicly required type", () => {
    type _Types = [
      CreateWallowSdkOptions,
      CsrfInterceptorClient,
      WallowSdk,
      WallowUser,
      BffConfig,
      BffHandlers,
      BffSession,
      BffUserResponse,
      CookieSessionStoreOptions,
      ForwardRequest,
      ForwardResult,
      RedisLike,
      SessionStore,
      ValkeySessionStoreOptions,
    ];
    expect(true).toBe(true);
  });

  it("typechecks clean — every type above is exported from its entry point", () => {
    let stdout: string = "";
    let failed: boolean = false;
    try {
      execFileSync("npx", ["tsc", "--noEmit"], {
        cwd: packageRoot,
        encoding: "utf8",
        stdio: ["ignore", "pipe", "pipe"],
      });
    } catch (error: unknown) {
      failed = true;
      const execError = error as { stdout?: string; stderr?: string };
      stdout = `${execError.stdout ?? ""}${execError.stderr ?? ""}`;
    }
    expect(failed ? stdout : "").toBe("");
  }, 120_000);
});

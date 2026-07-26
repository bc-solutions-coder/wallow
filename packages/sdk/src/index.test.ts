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
import type {
  BffClientOptions,
  CsrfInterceptorClient,
  SdkEnvelope,
  SsrRequestContext,
  WallowClientOptions,
  WallowUser,
} from "./index";
import type {
  BffConfig,
  BffHandlers,
  BffSession,
  BffUserResponse,
  CookieSessionStoreOptions,
  ForwardRequest,
  ForwardResult,
  ProblemDetails,
  RedisLike,
  SessionStore,
  ValkeySessionStoreOptions,
} from "./server/index";

const packageRoot: string = resolve(dirname(fileURLToPath(import.meta.url)), "..");

/** Value exports the browser entry must expose. */
const BROWSER_VALUE_EXPORTS: readonly string[] = [
  "buildConnectAuthorizeUrl",
  "buildConnectLogoutUrl",
  "buildConsentSubmitUrl",
  "buildExchangeTicketUrl",
  "client",
  "configureBffClient",
  "configureWallowClient",
  "createAuthClient",
  "createConfiguredOnce",
  "getUser",
  "configureSsrClient",
  "getSsrRequestContext",
  "isSafeMethod",
  "isSafeReturnUrl",
  "login",
  "logout",
  "setCsrfToken",
  "setSsrRequestContextResolver",
  "unwrap",
  "wireCsrfInterceptor",
  "wireSsrCookieInterceptor",
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
  "WallowError",
  "parseProblemDetails",
  "redact",
  "REDACTED",
  "UNKNOWN_ERROR_CODE",
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
  "NETWORK_ERROR_CODE",
  "NETWORK_TIMEOUT_CODE",
  // csrf
  "CSRF_HEADER",
  "CSRF_INVALID_CODE",
  // config
  "loadBffConfigFromEnv",
  "DEFAULT_SESSION_TTL_SECONDS",
];

describe("browser entry (package root export)", () => {
  it.each(BROWSER_VALUE_EXPORTS)("exports %s", (name: string) => {
    expect(Object.keys(browserEntry)).toContain(name);
    expect((browserEntry as unknown as Record<string, unknown>)[name]).toBeDefined();
  });

  it("keeps configureWallowClient as a back-compat alias of configureBffClient", () => {
    expect(browserEntry.configureWallowClient).toBe(browserEntry.configureBffClient);
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

  it("exports session stores as constructible SessionStore implementations", () => {
    const password: string = "a".repeat(32);
    const redis: RedisLike = {
      get: () => Promise.resolve(null),
      set: () => Promise.resolve("OK" as const),
      del: () => Promise.resolve(0),
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

  it("exports WallowError as a real Error subclass", () => {
    const error: Error = new serverEntry.WallowError({
      status: 404,
      code: "NOT_FOUND",
      title: "Not Found",
    });
    expect(error).toBeInstanceOf(Error);
  });
});

describe("public type surface", () => {
  // Type-level pins. These compile-time references are what `tsc --noEmit`
  // below validates; they have no runtime effect.
  it("references every publicly required type", () => {
    type _Types = [
      BffClientOptions,
      CsrfInterceptorClient,
      SdkEnvelope<unknown>,
      SsrRequestContext,
      WallowClientOptions,
      WallowUser,
      BffConfig,
      BffHandlers,
      BffSession,
      BffUserResponse,
      CookieSessionStoreOptions,
      ForwardRequest,
      ForwardResult,
      ProblemDetails,
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

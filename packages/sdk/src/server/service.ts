/**
 * `@bc-solutions-coder/sdk/server/service` — the machine-to-machine leg.
 *
 * A service account is a confidential client bound to one organization that
 * authenticates with the client-credentials grant; there is no user, no
 * session, and no browser. This entry is its own subpath so a worker that only
 * calls the API on its own behalf never pulls the BFF handler graph (cookies,
 * CSRF, the `/api` proxy) into its bundle — nothing here may import it.
 */

import {
  allowInsecureRequests,
  clientCredentialsGrant,
  discovery,
  type Configuration,
} from "openid-client";

import { createWallowSdk, type WallowSdk } from "../create-sdk";
import { shouldAllowInsecureRequests } from "./oidc";
import { createMemoryRedis } from "./store/memory";
import { createRedisFromUrl } from "./store/redis-url";
import { type RedisLike } from "./store/types";

/** The environment contract of a service client, loaded by {@link loadServiceConfigFromEnv}. */
export interface ServiceClientConfig {
  /** Public OIDC issuer URL, shared with the user BFF (`OIDC_ISSUER`). */
  issuer: string;
  /** Service-account client id (`OIDC_SERVICE_CLIENT_ID`). */
  clientId: string;
  /** Service-account client secret (`OIDC_SERVICE_CLIENT_SECRET`). */
  clientSecret: string;
  /** Scopes to request, from the space-separated `OIDC_SERVICE_SCOPES`. Required: a token with no scope reaches nothing. */
  scopes: string[];
  /** Base URL of the platform API (`BFF_API_BASE_URL`). */
  apiBaseUrl: string;
  /** Optional server-reachable discovery URL (`OIDC_METADATA_URL`), as for the user BFF. */
  metadataUrl?: string;
}

/** A clean environment contract: no collected problems to report. */
const NO_PROBLEMS = 0;

/**
 * Build a {@link ServiceClientConfig} from environment variables.
 *
 * Required: `OIDC_ISSUER`, `OIDC_SERVICE_CLIENT_ID`, `OIDC_SERVICE_CLIENT_SECRET`,
 * `OIDC_SERVICE_SCOPES`, `BFF_API_BASE_URL`. Optional: `OIDC_METADATA_URL`.
 * Only this subset is read — the user BFF's `OIDC_CLIENT_ID`, redirect URIs and
 * cookie settings are neither required nor consulted, so a service-only worker
 * carries five variables. Every problem is reported in ONE error, as the user
 * BFF's loader does.
 */
export function loadServiceConfigFromEnv(
  env: NodeJS.ProcessEnv = process.env,
): ServiceClientConfig {
  const problems: string[] = [];
  const requireEnv = (key: string): string => {
    const value: string = (env[key] ?? "").trim();
    if (value === "") {
      problems.push(`Missing required environment variable: ${key}`);
    }
    return value;
  };

  const issuer: string = requireEnv("OIDC_ISSUER");
  const clientId: string = requireEnv("OIDC_SERVICE_CLIENT_ID");
  const clientSecret: string = requireEnv("OIDC_SERVICE_CLIENT_SECRET");
  const scopesRaw: string = requireEnv("OIDC_SERVICE_SCOPES");
  const apiBaseUrl: string = requireEnv("BFF_API_BASE_URL");

  if (problems.length !== NO_PROBLEMS) {
    throw new Error(
      `Invalid service client environment configuration:\n  - ${problems.join("\n  - ")}`,
    );
  }

  const metadataUrl: string = (env.OIDC_METADATA_URL ?? "").trim();
  return {
    issuer,
    clientId,
    clientSecret,
    scopes: scopesRaw.split(/\s+/u),
    apiBaseUrl,
    metadataUrl: metadataUrl === "" ? undefined : metadataUrl,
  };
}

/** Options for {@link createServiceClient}. */
export interface ServiceClientOptions {
  /** Environment source for the config load. Defaults to `process.env`. */
  env?: NodeJS.ProcessEnv;
  /** A prebuilt config. When omitted it is loaded from {@link ServiceClientOptions.env}. */
  config?: ServiceClientConfig;
  /**
   * Shared token cache. Hand in the same Redis/Valkey-backed {@link RedisLike}
   * the BFF sessions use so every replica of a service shares ONE token and one
   * refresh lock. Omitted, a set `REDIS_URL` makes the client connect itself,
   * lazily, through the optional `redis` peer; with neither, the token is
   * cached in this process only.
   */
  store?: RedisLike;
  /**
   * Receives the `error` events of a cache connection the client opened ITSELF
   * from `REDIS_URL` (ignored when {@link ServiceClientOptions.store} is
   * supplied). Defaults to `console.error`.
   */
  onRedisError?: (error: unknown) => void;
  /** Transport to send API calls and the grant through. Defaults to `globalThis.fetch`. */
  fetch?: typeof globalThis.fetch;
}

/**
 * A service-account SDK instance: the SAME typed client shape as a user session's
 * {@link WallowSdk}, so a generated operation is called identically —
 * `inquiriesCreate({ client: service.client, body })` — plus the bearer itself
 * for the rare call outside the generated surface.
 */
export interface WallowServiceClient extends Pick<WallowSdk, "client"> {
  /** The current access token, fetched or refreshed as needed. */
  accessToken: () => Promise<string>;
}

/** Namespace prefix of the token-cache and lock keys. */
const KEY_PREFIX: string = "wallow";

/** Refresh-lock lifetime: an upper bound on one grant round trip. */
const LOCK_TTL_SECONDS = 10;

/** How early before expiry a cached token is treated as stale (milliseconds). */
const EXPIRY_SKEW_MS = 30_000;

/** Milliseconds per second. */
const MS_PER_SECOND = 1000;

/** Lifetime assumed when the token response omits `expires_in` (seconds). */
const NO_EXPIRY_SECONDS = 0;

/** HTTP status that means the bearer was rejected. */
const UNAUTHORIZED_STATUS = 401;

/** A cached token record, as serialised into the store. */
interface CachedToken {
  accessToken: string;
  /** Epoch milliseconds. */
  expiresAt: number;
}

/**
 * Obtains, caches and renews the service account's access token.
 *
 * Two layers of coordination: in-process callers share one in-flight grant
 * (single flight), and across processes the store's `SET NX EX` lock lets one
 * replica fetch while the others wait for its write.
 */
class ServiceTokenSource {
  private readonly cacheKey: string;
  private readonly lockKey: string;
  private configuration: Promise<Configuration> | undefined;
  private inflight: Promise<string> | undefined;

  private readonly config: ServiceClientConfig;
  private readonly store: RedisLike;

  constructor(config: ServiceClientConfig, store: RedisLike) {
    this.config = config;
    this.store = store;
    const scopeKey: string = encodeURIComponent(config.scopes.toSorted().join(" "));
    this.cacheKey = `${KEY_PREFIX}:service-token:${config.clientId}:${scopeKey}`;
    this.lockKey = `${KEY_PREFIX}:service-token-lock:${config.clientId}:${scopeKey}`;
  }

  async get(): Promise<string> {
    const cached: CachedToken | null = await this.readCache();
    if (cached !== null) {
      return cached.accessToken;
    }
    this.inflight ??= this.acquire().finally((): void => {
      this.inflight = undefined;
    });
    return this.inflight;
  }

  /** Drop `token` from the cache if it is still the cached one, so the next call fetches. */
  async invalidate(token: string): Promise<void> {
    const raw: string | null = await this.store.get(this.cacheKey);
    if (raw !== null && (JSON.parse(raw) as CachedToken).accessToken === token) {
      await this.store.del(this.cacheKey);
    }
  }

  private async readCache(): Promise<CachedToken | null> {
    const raw: string | null = await this.store.get(this.cacheKey);
    if (raw === null) {
      return null;
    }
    const cached: CachedToken = JSON.parse(raw) as CachedToken;
    return cached.expiresAt - EXPIRY_SKEW_MS > Date.now() ? cached : null;
  }

  private async acquire(): Promise<string> {
    const acquired: "OK" | null = await this.store.set(this.lockKey, "1", {
      nx: true,
      ex: LOCK_TTL_SECONDS,
    });
    if (acquired === null) {
      const winner: CachedToken | null = await this.awaitLockHolder();
      if (winner !== null) {
        return winner.accessToken;
      }
      // The holder never wrote (crashed, or its grant failed): fetch unlocked
      // rather than fail every caller for the length of a stale lock.
      return this.fetchAndCache();
    }
    try {
      // Double-check under the lock: the previous holder may have written
      // between this caller's cache miss and its lock acquisition.
      const cached: CachedToken | null = await this.readCache();
      return cached === null ? await this.fetchAndCache() : cached.accessToken;
    } finally {
      await this.store.del(this.lockKey);
    }
  }

  /** Poll the cache while another replica holds the lock; `null` once the lock has lapsed. */
  private awaitLockHolder(): Promise<CachedToken | null> {
    const deadline: number = Date.now() + LOCK_TTL_SECONDS * MS_PER_SECOND;
    const poll = async (): Promise<CachedToken | null> => {
      if (Date.now() >= deadline) {
        return null;
      }
      await new Promise<void>((resolve): void => {
        setTimeout(resolve, LOCK_POLL_MS);
      });
      const cached: CachedToken | null = await this.readCache();
      if (cached !== null) {
        return cached;
      }
      const lock: string | null = await this.store.get(this.lockKey);
      return lock === null ? this.readCache() : poll();
    };
    return poll();
  }

  private async fetchAndCache(): Promise<string> {
    const configuration: Configuration = await this.discover();
    const response = await clientCredentialsGrant(configuration, {
      scope: this.config.scopes.join(" "),
    });
    const expiresIn: number = response.expiresIn() ?? NO_EXPIRY_SECONDS;
    const record: CachedToken = {
      accessToken: response.access_token,
      expiresAt: Date.now() + expiresIn * MS_PER_SECOND,
    };
    if (expiresIn > NO_EXPIRY_SECONDS) {
      await this.store.set(this.cacheKey, JSON.stringify(record), { ex: expiresIn });
    }
    return record.accessToken;
  }

  private discover(): Promise<Configuration> {
    const metadataUrl: string =
      this.config.metadataUrl ?? `${this.config.issuer}/.well-known/openid-configuration`;
    this.configuration ??= discovery(
      new URL(metadataUrl),
      this.config.clientId,
      this.config.clientSecret,
      undefined,
      shouldAllowInsecureRequests(metadataUrl) ? { execute: [allowInsecureRequests] } : undefined,
    ).catch((error: unknown) => {
      // A failed discovery is not cached: the OP may simply not be up yet.
      this.configuration = undefined;
      throw error;
    });
    return this.configuration;
  }
}

/** Interval between cache polls while another replica holds the refresh lock. */
const LOCK_POLL_MS = 50;

/**
 * Pick the token cache: an explicit store, else a lazily connected `REDIS_URL`
 * (shared across replicas, like the BFF's sessions), else this process only.
 */
function selectCache(env: NodeJS.ProcessEnv, options: ServiceClientOptions): RedisLike {
  if (options.store !== undefined) {
    return options.store;
  }
  const redisUrl: string = (env.REDIS_URL ?? "").trim();
  if (redisUrl !== "") {
    return createRedisFromUrl(redisUrl, { onError: options.onRedisError });
  }
  return createMemoryRedis();
}

/**
 * Build a service-account client.
 *
 * Every API call carries the service account's bearer; a rejected bearer
 * (`401`) invalidates the cached token, fetches a fresh one, and replays the
 * request exactly once. Tokens are renewed {@link EXPIRY_SKEW_MS} before they
 * expire, so a caller never sends one about to lapse.
 */
export function createServiceClient(options: ServiceClientOptions = {}): WallowServiceClient {
  const env: NodeJS.ProcessEnv = options.env ?? process.env;
  const config: ServiceClientConfig = options.config ?? loadServiceConfigFromEnv(env);
  const transport: typeof globalThis.fetch = options.fetch ?? globalThis.fetch;
  const tokens: ServiceTokenSource = new ServiceTokenSource(config, selectCache(env, options));

  const send: typeof globalThis.fetch = async (
    input: RequestInfo | URL,
    init?: RequestInit,
  ): Promise<Response> => {
    const request: Request = input instanceof Request ? input : new Request(input, init);
    // BUFFERED so the request can be replayed after a rejected bearer; a stream
    // body is consumed by the first attempt.
    const body: ArrayBuffer | undefined =
      request.body === null ? undefined : await request.arrayBuffer();

    const attempt = (token: string): Promise<Response> => {
      const headers: Headers = new Headers(request.headers);
      headers.set("authorization", `Bearer ${token}`);
      return transport(
        new Request(request.url, {
          body,
          headers,
          method: request.method,
          redirect: request.redirect,
          signal: request.signal,
        }),
      );
    };

    const token: string = await tokens.get();
    const response: Response = await attempt(token);
    if (response.status !== UNAUTHORIZED_STATUS) {
      return response;
    }
    await tokens.invalidate(token);
    return attempt(await tokens.get());
  };

  const sdk: WallowSdk = createWallowSdk({ baseUrl: config.apiBaseUrl, csrf: false, fetch: send });
  return { client: sdk.client, accessToken: (): Promise<string> => tokens.get() };
}

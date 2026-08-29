/**
 * BFF server preset (Wallow-pu6a.3.7).
 *
 * Absorbs the wiring every fork currently hand-assembles in its own host file
 * (`apps/wallow-web/src/lib/bff-server.ts`): load {@link BffConfig} from the
 * environment, pick a {@link SessionStore}, build the OIDC tunnel handlers and
 * the `/api` proxy over that ONE shared store, and dispatch by path. What comes
 * back is three web-standard entry points a host can mount anywhere:
 * {@link WallowBffServer.handleBff}, {@link WallowBffServer.handleApi}, and
 * {@link WallowBffServer.handleHealth}.
 *
 * The mount points are exported as {@link WALLOW_BFF_MOUNT} and
 * {@link WALLOW_API_MOUNT} so a host and the SDK agree on the prefixes by
 * import rather than by repeating string literals that can drift apart.
 */

import { loadBffConfigFromEnv, type BffConfig } from "./config";
import { createBffHandlers, type BffHandler, type BffHandlers } from "./handlers";
import { resolveTrustedProxies, type PeerRequest, type TrustedProxies } from "./forwarded";
import { createApiProxy, type ApiProxyHandler } from "./proxy";
import { CookieSessionStore } from "./store/cookie";
import { createRedisAdapter, type NodeRedisClient } from "./store/redis-adapter";
import { createRedisFromUrl } from "./store/redis-url";
import { type SessionStore } from "./store/types";
import { ValkeySessionStore } from "./store/valkey";

/** Mount point of the reverse `/api` proxy: everything below it is forwarded. */
export const WALLOW_API_MOUNT: string = "/api";

/** Mount point of the OIDC tunnel handlers (`login`, `callback`, `user`, `logout`). */
export const WALLOW_BFF_MOUNT: string = "/bff";

/** Options for {@link createWallowBffServer}. */
export interface WallowBffServerOptions {
  /** Environment source for the config load and store selection. Defaults to `process.env`. */
  env?: NodeJS.ProcessEnv;
  /** A prebuilt config. When omitted it is loaded from {@link WallowBffServerOptions.env}. */
  config?: BffConfig;
  /**
   * An explicit session store. Wins over every other selection rule; supply it
   * to bring a store the preset does not know about.
   */
  store?: SessionStore;
  /**
   * A connected Redis/Valkey client — a node-redis client is assignable as-is.
   * Supply one to own the connection yourself (its lifecycle, its error
   * logging); the preset adapts it into a `ValkeySessionStore`. Without it,
   * a set `REDIS_URL` makes the preset connect itself, lazily, through the
   * optional `redis` peer.
   */
  redisClient?: NodeRedisClient;
  /**
   * Receives the `error` events of a client the preset connected ITSELF from
   * `REDIS_URL` (ignored when {@link WallowBffServerOptions.redisClient} or
   * {@link WallowBffServerOptions.store} is supplied). Defaults to `console.error`.
   */
  onRedisError?: (error: unknown) => void;
  /**
   * The proxies whose `X-Forwarded-For` may be believed when the `/api` proxy
   * resolves the caller's address, in the same notation as
   * `WALLOW_TRUSTED_PROXIES` (CIDRs, bare addresses, or the `loopback`,
   * `linklocal`, `uniquelocal`, `private` presets; comma- or space-separated).
   * When omitted it resolves from `WALLOW_TRUSTED_PROXIES` in
   * {@link WallowBffServerOptions.env}, then to trusting nothing — the peer
   * address IS the client. An empty string trusts nothing even when the
   * variable is set.
   */
  trustedProxies?: string;
}

/** The BFF surface a host mounts. */
export interface WallowBffServer {
  /** Handle a request under {@link WALLOW_BFF_MOUNT}; unknown sub-paths answer 404. */
  handleBff: (request: Request) => Promise<Response>;
  /**
   * Handle a request under {@link WALLOW_API_MOUNT}; anything else answers 404.
   * Pass the request as srvx hands it over — its `ip` is the peer address the
   * proxy forwards to the API, resolved through the trusted-proxy list and never
   * read from a header the caller could have written.
   */
  handleApi: (request: PeerRequest) => Promise<Response>;
  /** Liveness probe: 200 with a small JSON body. */
  handleHealth: () => Response;
  /** The resolved configuration. */
  readonly config: BffConfig;
  /** The selected session store — the SAME instance behind both handlers. */
  readonly store: SessionStore;
  /** The OIDC tunnel handlers behind {@link WallowBffServer.handleBff}. */
  readonly handlers: BffHandlers;
}

/** Status answered for a path this server does not route. */
const NOT_FOUND_STATUS = 404;

/** Path separator, and the sub-path of a request that hits the mount exactly. */
const ROOT_PATH: string = "/";

/**
 * Pick the session store.
 *
 * An explicit store wins outright. Otherwise a supplied Redis/Valkey client is
 * adapted into a {@link ValkeySessionStore}; failing that, a set `REDIS_URL`
 * still means Valkey — the preset connects itself on first use through the
 * optional `redis` peer. `REDIS_URL` never degrades to the cookie store:
 * silently serving stateless sessions to a deployment that asked for
 * server-side ones is exactly the failure this ordering prevents.
 */
function selectStore(
  config: BffConfig,
  env: NodeJS.ProcessEnv,
  options: WallowBffServerOptions,
): SessionStore {
  if (options.store !== undefined) {
    return options.store;
  }

  if (options.redisClient !== undefined) {
    return new ValkeySessionStore({
      client: createRedisAdapter(options.redisClient),
      password: config.cookiePasswords ?? config.cookiePassword,
      ttlSeconds: config.sessionTtlSeconds,
    });
  }

  const redisUrl: string = (env.REDIS_URL ?? "").trim();
  if (redisUrl !== "") {
    return new ValkeySessionStore({
      client: createRedisFromUrl(redisUrl, { onError: options.onRedisError }),
      password: config.cookiePasswords ?? config.cookiePassword,
      ttlSeconds: config.sessionTtlSeconds,
    });
  }

  return new CookieSessionStore({
    password: config.cookiePasswords ?? config.cookiePassword,
    ttlSeconds: config.sessionTtlSeconds,
  });
}

/**
 * The path below {@link WALLOW_BFF_MOUNT} a request addresses, or `null` when it
 * lies outside the mount. The test is a segment-boundary one, so a lookalike
 * prefix such as `/bffoo/user` is not routed here.
 */
function bffSubPath(pathname: string): string | null {
  if (pathname === WALLOW_BFF_MOUNT) {
    return ROOT_PATH;
  }
  if (!pathname.startsWith(`${WALLOW_BFF_MOUNT}${ROOT_PATH}`)) {
    return null;
  }
  return pathname.slice(WALLOW_BFF_MOUNT.length);
}

/**
 * Build the BFF server preset.
 *
 * @param options Config, store, and Redis-client overrides. Everything omitted
 *   is resolved from the environment.
 */
export function createWallowBffServer(options: WallowBffServerOptions = {}): WallowBffServer {
  const env: NodeJS.ProcessEnv = options.env ?? process.env;
  const config: BffConfig = options.config ?? loadBffConfigFromEnv(env);
  const store: SessionStore = selectStore(config, env, options);

  // Handlers and proxy share ONE store instance: the proxy has to resolve the
  // very sessions the callback handler wrote.
  const handlers: BffHandlers = createBffHandlers(config, store);
  const trusted: TrustedProxies = resolveTrustedProxies(options.trustedProxies, env);
  const proxy: ApiProxyHandler = createApiProxy(config, store, trusted);

  // Dispatch is by PATH only. Method policy belongs to the handlers: a bare
  // `GET /bff/logout` must reach the logout handler so it can answer 405 with
  // `Allow: POST`, which a method-filtered route would swallow as a 404.
  const routes: Record<string, BffHandler | undefined> = {
    "/login": handlers.login,
    "/callback": handlers.callback,
    "/user": handlers.user,
    "/logout": handlers.logout,
    "/frontchannel-logout": handlers.frontchannelLogout,
  };

  return {
    config,
    store,
    handlers,
    handleBff: (request: Request): Promise<Response> => {
      const subPath: string | null = bffSubPath(new URL(request.url).pathname);
      const handler: BffHandler | undefined = subPath === null ? undefined : routes[subPath];
      if (handler === undefined) {
        return Promise.resolve(new Response(null, { status: NOT_FOUND_STATUS }));
      }
      return handler(request);
    },
    handleApi: (request: PeerRequest): Promise<Response> => proxy(request),
    handleHealth: (): Response => Response.json({ status: "ok" }),
  };
}

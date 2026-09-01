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
import { discover, type DiscoveryDoc } from "./oidc";
import { createApiProxy, type ApiProxyHandler } from "./proxy";
import { CookieSessionStore } from "./store/cookie";
import { createRedisAdapter, type NodeRedisClient } from "./store/redis-adapter";
import { createRedisFromUrl } from "./store/redis-url";
import { type SessionStore } from "./store/types";
import { DEFAULT_KEY_PREFIX, ValkeySessionStore } from "./store/valkey";

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
  /**
   * Receives boot-time misconfiguration warnings — today exactly two: the
   * issuer advertises OIDC back-channel logout but the selected store defines
   * neither `revokeBySid` nor `revokeBySubject`, so logout tokens would be
   * accepted and revoke nothing; and the store's key namespace is already
   * claimed by a DIFFERENT BFF identity, so two BFFs would read each other's
   * sessions (set `BFF_APP_ID` per BFF to separate them). Defaults to
   * `console.warn`.
   */
  onWarning?: (message: string) => void;
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

  // `appId` namespaces the shared Valkey: session records AND the sid/sub
  // indexes move under `wallow:<appId>`, so co-tenant BFFs stop resolving each
  // other's sessions and a back-channel logout for one RP cannot tear down
  // another's. Unset means the store's own default prefix.
  const keyPrefix: string | undefined =
    config.appId === undefined ? undefined : `${DEFAULT_KEY_PREFIX}:${config.appId}`;

  if (options.redisClient !== undefined) {
    return new ValkeySessionStore({
      client: createRedisAdapter(options.redisClient),
      password: config.cookiePasswords ?? config.cookiePassword,
      ttlSeconds: config.sessionTtlSeconds,
      keyPrefix,
    });
  }

  const redisUrl: string = (env.REDIS_URL ?? "").trim();
  if (redisUrl !== "") {
    return new ValkeySessionStore({
      client: createRedisFromUrl(redisUrl, { onError: options.onRedisError }),
      password: config.cookiePasswords ?? config.cookiePassword,
      ttlSeconds: config.sessionTtlSeconds,
      keyPrefix,
    });
  }

  return new CookieSessionStore({
    password: config.cookiePasswords ?? config.cookiePassword,
    ttlSeconds: config.sessionTtlSeconds,
  });
}

/**
 * Fire-and-forget boot probe: warn once when the issuer advertises
 * back-channel logout the selected store cannot honour. A failed probe is
 * swallowed — the OP may simply not be up yet, and the first real request
 * retries through the same discovery cache.
 */
function warnWhenBackchannelUnsupported(
  config: BffConfig,
  store: SessionStore,
  onWarning: (message: string) => void,
): void {
  if (store.revokeBySid !== undefined || store.revokeBySubject !== undefined) {
    return;
  }
  void discover(config)
    .then((doc: DiscoveryDoc): void => {
      if (doc.backchannel_logout_supported === true) {
        onWarning(
          "The OIDC issuer advertises back-channel logout, but the selected session store " +
            "defines neither revokeBySid nor revokeBySubject — logout tokens will be accepted " +
            "and revoke nothing. Cookie sessions cannot be revoked server-side; use a " +
            "server-side store (set REDIS_URL or supply a ValkeySessionStore) to honour " +
            "back-channel logout.",
        );
      }
    })
    .catch((): void => {
      // Not reachable at boot is not a misconfiguration.
    });
}

/**
 * Fire-and-forget boot probe: stamp the store's key namespace with this BFF's
 * identity and warn when a DIFFERENT identity already holds it — two BFFs
 * writing one namespace read each other's sessions and tear down each other's
 * logins on back-channel logout. A failed claim is swallowed: the store may
 * simply not be up yet, and an unreachable store surfaces on the first real
 * request anyway.
 */
function warnWhenNamespaceShared(
  config: BffConfig,
  store: SessionStore,
  onWarning: (message: string) => void,
): void {
  if (store.claimNamespace === undefined) {
    return;
  }
  void store
    .claimNamespace(`${config.issuer} ${config.clientId}`)
    .then((holder: string | null): void => {
      if (holder !== null) {
        onWarning(
          `The session-store namespace is already claimed by a different BFF (${holder}). ` +
            "Two BFFs sharing one namespace read each other's sessions, and a back-channel " +
            "logout for one tears down the other's. Give each BFF its own BFF_APP_ID (or a " +
            "distinct ValkeySessionStore keyPrefix) so they write disjoint keys.",
        );
      }
    })
    .catch((): void => {
      // Not reachable at boot is not a misconfiguration.
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
  const onWarning: (message: string) => void = options.onWarning ?? console.warn;
  warnWhenBackchannelUnsupported(config, store, onWarning);
  warnWhenNamespaceShared(config, store, onWarning);
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
    "/backchannel-logout": handlers.backchannelLogout,
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

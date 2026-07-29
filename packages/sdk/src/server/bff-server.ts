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
import { createApiProxy, type ApiProxyHandler } from "./proxy";
import { CookieSessionStore } from "./store/cookie";
import { createRedisAdapter, type NodeRedisClient } from "./store/redis-adapter";
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
   * A connected Redis/Valkey client. The SDK never imports `redis` itself, so a
   * host that wants server-side sessions constructs and connects the client and
   * hands it here; the preset adapts it into a `ValkeySessionStore`. When
   * `REDIS_URL` is set and no client is supplied, construction fails fast.
   */
  redisClient?: NodeRedisClient;
}

/** The BFF surface a host mounts. */
export interface WallowBffServer {
  /** Handle a request under {@link WALLOW_BFF_MOUNT}; unknown sub-paths answer 404. */
  handleBff: (request: Request) => Promise<Response>;
  /** Handle a request under {@link WALLOW_API_MOUNT}; anything else answers 404. */
  handleApi: (request: Request) => Promise<Response>;
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
 * adapted into a {@link ValkeySessionStore}; the SDK never imports `redis`
 * itself, so the host constructs and connects the client and hands it in. That
 * makes `REDIS_URL` set with no client a misconfiguration rather than a
 * fallback: silently serving stateless cookie sessions to a deployment that
 * asked for server-side ones is exactly the failure this throw prevents.
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
    throw new Error(
      "REDIS_URL is set but no redisClient was supplied to createWallowBffServer. " +
        "The SDK does not import `redis`: construct and connect the client in the host " +
        "and pass it as `redisClient`, or unset REDIS_URL to use cookie sessions.",
    );
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
  const proxy: ApiProxyHandler = createApiProxy(config, store);

  // Dispatch is by PATH only. Method policy belongs to the handlers: a bare
  // `GET /bff/logout` must reach the logout handler so it can answer 405 with
  // `Allow: POST`, which a method-filtered route would swallow as a 404.
  const routes: Record<string, BffHandler | undefined> = {
    "/login": handlers.login,
    "/callback": handlers.callback,
    "/user": handlers.user,
    "/logout": handlers.logout,
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
    handleApi: (request: Request): Promise<Response> => proxy(request),
    handleHealth: (): Response => Response.json({ status: "ok" }),
  };
}

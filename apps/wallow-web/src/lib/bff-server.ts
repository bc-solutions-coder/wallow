/**
 * BFF server core for wallow-web (Wallow-8w1h.2.3, GREEN).
 *
 * Ports the h3 BFF handler construction out of the standalone `server.ts` so the
 * SDK's `createBffHandlers`/`createApiProxy` h3 handlers can be driven from the
 * TanStack Start server layer instead of a separate host process. It exposes:
 *
 *   - {@link bff}          — the four OIDC tunnel handlers (login/callback/user/
 *                            logout), from `createBffHandlers(config, store)`.
 *   - {@link apiProxy}     — the reverse `/api/**` proxy, from
 *                            `createApiProxy(config, store)` (SAME store as bff).
 *   - {@link handleBffRequest} — a framework-agnostic web `Request` -> `Response`
 *                            bridge (h3 `toWebHandler` shape) that mounts
 *                            `/health`, `/bff/*`, and `/api/**`, exactly as the
 *                            old `server.ts` router did.
 *
 * MOUNTING DECISION (spike outcome, Wallow-8w1h.2.3)
 * --------------------------------------------------
 * The bead design's first choice was file-based Start *server routes*
 * (`src/routes/bff/$.ts`, `src/routes/api/$.ts`) built with a
 * `createServerFileRoute`/`createServerRoute` API. That API does NOT exist in
 * the installed stack (@tanstack/react-start@1.168.28 /
 * @tanstack/react-router@1.170.18 expose no such creator, and
 * `@tanstack/react-start/server` has no server-route factory). Rather than ship
 * non-functional route stubs, the spike takes the design's documented fallback:
 * mount this h3 bridge at the Start SSR server layer (`dev-server.ts`) via a
 * path-prefix dispatch — `/health`, `/bff/*`, `/api/**` are answered by
 * {@link handleBffRequest}; everything else falls through to the router SSR.
 * This keeps the whole BFF surface reachable at the exact URLs the C#
 * `BffFlowTests`/`DockerComposeFixture` assert, in a single Node process, with
 * no createServerFileRoute dependency. If a future TanStack release adds a
 * real server-route creator, the `$.ts` route files can delegate to
 * {@link handleBffRequest} unchanged.
 */
import {
  CookieSessionStore,
  createApiProxy,
  createBffHandlers,
  createRedisAdapter,
  loadBffConfigFromEnv,
  ValkeySessionStore,
  type BffConfig,
  type BffHandlers,
  type NodeRedisClient,
  type SessionStore,
} from "@bc-solutions-coder/sdk/server";
import { createClient, type RedisClientType } from "redis";
import {
  createApp,
  createRouter,
  defineEventHandler,
  toWebHandler,
  type App,
  type EventHandler,
  type Router,
  type WebHandler,
} from "h3";

const config: BffConfig = loadBffConfigFromEnv();

// Where sessions live — the one knob swapped in production. When `REDIS_URL` is
// set the session cookie becomes an opaque reference and the full session
// (including tokens) is persisted server-side in Valkey/Redis, buying real
// server-side revocation and a cross-instance refresh lock. Without it we fall
// back to `CookieSessionStore`, which seals the whole session into the cookie
// and needs no external store. Both handler factories MUST share the SAME store
// — the proxy has to resolve the sessions the login callback wrote.
const redisUrl: string | undefined = process.env.REDIS_URL;
let store: SessionStore;
if (redisUrl !== undefined && redisUrl !== "") {
  const redisClient: RedisClientType = createClient({ url: redisUrl });
  redisClient.on("error", (error: unknown): void => {
    // eslint-disable-next-line no-console
    console.error("redis client error", error);
  });
  await redisClient.connect();
  // node-redis satisfies NodeRedisClient structurally, but its overloaded types
  // are broader than the port, so bridge through the three methods we use.
  const client: NodeRedisClient = {
    get: (key: string): Promise<string | null> => redisClient.get(key),
    set: (
      key: string,
      value: string,
      options?: { EX?: number; NX?: boolean },
    ): Promise<string | null> => {
      if (options === undefined) {
        return redisClient.set(key, value);
      }
      return redisClient.set(key, value, {
        ...(options.EX !== undefined ? { EX: options.EX } : {}),
        ...(options.NX === true ? { NX: true } : {}),
      });
    },
    del: (key: string): Promise<number> => redisClient.del(key),
  };
  store = new ValkeySessionStore({
    client: createRedisAdapter(client),
    password: config.cookiePassword,
    ttlSeconds: config.sessionTtlSeconds,
  });
  // eslint-disable-next-line no-console
  console.log("BFF sessions: ValkeySessionStore (server-side, REDIS_URL set)");
} else {
  store = new CookieSessionStore({ password: config.cookiePassword });
  // eslint-disable-next-line no-console
  console.log("BFF sessions: CookieSessionStore (stateless, no REDIS_URL)");
}

/**
 * The four BFF OIDC tunnel handlers (login/callback/user/logout). `/bff/login`
 * and `/bff/logout` issue redirects; `/bff/user` reflects the sealed session;
 * `/bff/callback` completes the code exchange.
 */
export const bff: BffHandlers = createBffHandlers(config, store);

/**
 * The reverse `/api/**` proxy handler. Everything under `/api` is forwarded to
 * the downstream API with a Bearer token and silent refresh. The proxy strips
 * the `/api` prefix itself, so the full request path must reach it intact.
 * Shares the SAME {@link store} as {@link bff} so it can resolve login sessions.
 */
export const apiProxy: EventHandler = createApiProxy(config, store);

const app: App = createApp();
const router: Router = createRouter();

// Liveness for the E2E DockerComposeFixture health wait.
router.get(
  "/health",
  defineEventHandler((): { status: string } => ({ status: "ok" })),
);

router.use("/bff/login", bff.login);
router.use("/bff/callback", bff.callback);
router.get("/bff/user", bff.user);
router.use("/bff/logout", bff.logout);

// The proxy strips the `/api` prefix itself, so route the whole subtree to it.
router.use("/api/**", apiProxy);

app.use(router);

const webHandler: WebHandler = toWebHandler(app);

/**
 * Bridge a WHATWG `Request` to a `Response` by driving the h3 app that mounts
 * `/health`, `/bff/*`, and `/api/**`. The Start SSR server layer forwards
 * requests to these prefixes here; a future file-based Start server route could
 * delegate to it unchanged (see MOUNTING DECISION above).
 */
export function handleBffRequest(request: Request): Promise<Response> {
  return webHandler(request);
}

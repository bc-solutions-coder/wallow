/**
 * wallow-web's BFF host wiring — a thin wrapper over the SDK's
 * `createWallowBffServer` preset, replacing the 170-line hand-rolled h3 app
 * (`bff-server.ts`) it retires along with the standalone `server.ts`/
 * `dev-server.ts` hosts that mounted it.
 *
 * Everything the old module assembled by hand — load the config from the
 * environment, pick a session store, build the OIDC tunnel handlers and the
 * `/api` proxy over that ONE shared store, dispatch by path — now lives in the
 * SDK. What is left here is the two things a HOST still owns:
 *
 *  - **The Redis client.** The SDK deliberately does not import `redis`, so when
 *    `REDIS_URL` is set this module constructs and connects the client and hands
 *    it in (`createWallowBffServer` throws rather than silently falling back to
 *    stateless cookie sessions for a deployment that asked for server-side ones).
 *    Both compose stacks set `REDIS_URL`, so this is the production path, not a
 *    corner case.
 *  - **Laziness.** The server is built on FIRST USE and memoised, never at module
 *    load: a Start server-route module is evaluated as part of the server bundle,
 *    where a config throw would take down SSR and every other route with it. A
 *    failed build is not cached, so a transient Redis outage at boot does not
 *    permanently disable the BFF.
 */
import {
  CLIENT_IP_HEADER,
  createWallowBffServer,
  type NodeRedisClient,
  type WallowBffServer,
} from "@bc-solutions-coder/sdk/server";
import { createClient, type RedisClientType } from "redis";

/**
 * The inbound request as srvx hands it to a Start server route. A WHATWG
 * `Request` has no socket, so the peer address arrives on this extra `ip`
 * property (populated in `vite dev` and in the built Nitro server alike).
 */
interface PeerRequest extends Request {
  readonly ip?: string | undefined;
}

let pending: Promise<WallowBffServer> | undefined;

/**
 * Adapt node-redis to the SDK's narrow client port. node-redis satisfies
 * {@link NodeRedisClient} structurally, but its overloaded signatures are
 * broader than the port, so the three methods the store uses are bridged
 * explicitly.
 */
function adaptRedisClient(redisClient: RedisClientType): NodeRedisClient {
  return {
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
        ...(options.EX === undefined ? {} : { EX: options.EX }),
        ...(options.NX === true ? { NX: true } : {}),
      });
    },
    del: (key: string): Promise<number> => redisClient.del(key),
  };
}

/**
 * Connect to Valkey/Redis when `REDIS_URL` is set, so the session cookie becomes
 * an opaque reference and the tokens live server-side — which is what makes
 * logout a real revocation and gives a multi-instance deployment one refresh
 * lock. Unset, the preset falls back to the stateless cookie store.
 */
async function connectRedisClient(): Promise<NodeRedisClient | undefined> {
  const redisUrl: string = (process.env.REDIS_URL ?? "").trim();
  if (redisUrl === "") {
    return undefined;
  }

  const redisClient: RedisClientType = createClient({ url: redisUrl });
  redisClient.on("error", (error: unknown): void => {
    console.error("redis client error", error);
  });
  await redisClient.connect();
  return adaptRedisClient(redisClient);
}

/**
 * Build the BFF server, connecting the session store first when one is configured.
 *
 * The choice is logged because the fallback is otherwise silent: an unset
 * `REDIS_URL` degrades to stateless cookie sessions with no error, so a
 * deployment that meant to have server-side sessions (and real logout
 * revocation) looks identical at boot to one that did not.
 */
async function buildBffServer(): Promise<WallowBffServer> {
  const redisClient: NodeRedisClient | undefined = await connectRedisClient();
  console.info(
    redisClient === undefined
      ? "BFF session store: cookie (stateless) — REDIS_URL is unset"
      : "BFF session store: redis/valkey (server-side)",
  );
  return createWallowBffServer(redisClient === undefined ? {} : { redisClient });
}

/** The app's one BFF server, built on first use and shared by every route below it. */
function getBffServer(): Promise<WallowBffServer> {
  pending ??= buildBffServer().catch((error: unknown) => {
    // Do not cache the failure: the next request rebuilds, so a Redis that was
    // not up yet is recoverable without restarting the host.
    pending = undefined;
    throw error;
  });
  return pending;
}

/** Handle a request under `/bff` — the OIDC tunnel (login, callback, user, logout). */
export async function handleBffRequest(request: Request): Promise<Response> {
  const server: WallowBffServer = await getBffServer();
  return server.handleBff(request);
}

/**
 * Handle a request under `/api` — the reverse proxy that attaches the session's bearer.
 *
 * The client IP is stamped onto the SDK's `CLIENT_IP_HEADER` before handing over:
 * the proxy appends that header to the outgoing `X-Forwarded-For` chain and
 * strips the seam header itself, but it can only do so if the host supplies the
 * address — without this the API sees every user of this app as one client and
 * rate-limits them together (Wallow-vufu.4.2, the BFF twin of Wallow-tt5j).
 *
 * The header is set ON THE INBOUND REQUEST rather than on a clone. The obvious
 * `new Request(request, { headers })` throws `Cannot read private member #state`
 * at runtime: srvx's request is its own class that only claims to be a `Request`
 * through `Symbol.hasInstance`, so undici's copy constructor passes the instance
 * check and then reads a private field that does not exist. Mutating is also
 * safe — the proxy builds its own outgoing `Headers`, and the request object is
 * per-request and dead once this returns.
 */
export async function handleApiRequest(request: PeerRequest): Promise<Response> {
  const server: WallowBffServer = await getBffServer();
  const clientIp: string | undefined = request.ip;
  if (clientIp !== undefined && clientIp !== "") {
    request.headers.set(CLIENT_IP_HEADER, clientIp);
  }
  return server.handleApi(request);
}

/** Liveness probe: 200 with a small JSON body, which both compose stacks wait on. */
export async function handleHealthRequest(): Promise<Response> {
  const server: WallowBffServer = await getBffServer();
  return server.handleHealth();
}

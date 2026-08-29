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
 *  - **The Redis client.** The preset can connect itself from `REDIS_URL`, but
 *    this host owns the connection instead so the client's `error` events reach
 *    the app's structured logger and a connection failure surfaces at BUILD
 *    time (below) rather than on the first session write. Both compose stacks
 *    set `REDIS_URL`, so this is the production path, not a corner case.
 *  - **Laziness.** The server is built on FIRST USE and memoised, never at module
 *    load: a Start server-route module is evaluated as part of the server bundle,
 *    where a config throw would take down SSR and every other route with it. A
 *    failed build is not cached, so a transient Redis outage at boot does not
 *    permanently disable the BFF.
 */
import { type PeerRequest } from "@bc-solutions-coder/env/client-address";
import {
  CLIENT_IP_HEADER,
  createWallowBffServer,
  type WallowBffServer,
} from "@bc-solutions-coder/sdk/server";
import { createClient, type RedisClientType } from "redis";

import { clientAddressFor } from "./client-address.server";
import { serverLog } from "./log.server";

let pending: Promise<WallowBffServer> | undefined;

/**
 * Connect to Valkey/Redis when `REDIS_URL` is set, so the session cookie becomes
 * an opaque reference and the tokens live server-side — which is what makes
 * logout a real revocation and gives a multi-instance deployment one refresh
 * lock. Unset, the preset falls back to the stateless cookie store.
 */
async function connectRedisClient(): Promise<RedisClientType | undefined> {
  const redisUrl: string = (process.env.REDIS_URL ?? "").trim();
  if (redisUrl === "") {
    return undefined;
  }

  const redisClient: RedisClientType = createClient({ url: redisUrl });
  redisClient.on("error", (error: unknown): void => {
    serverLog.error("bff.redis.error", {}, error);
  });
  await redisClient.connect();
  return redisClient;
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
  const redisClient: RedisClientType | undefined = await connectRedisClient();
  serverLog.info("bff.session_store.selected", {
    store: redisClient === undefined ? "cookie" : "redis",
    stateless: redisClient === undefined,
  });
  return createWallowBffServer(redisClient === undefined ? {} : { redisClient });
}

/**
 * The app's one BFF server, built on first use and shared by every route below it.
 *
 * Exported because the log ingest route needs the SAME config and store to read
 * the session behind a batch: a second `createWallowBffServer` would be a second
 * Redis connection answering from the same data, and a drifted cookie name would
 * make it answer `null` for a signed-in user.
 */
export function getBffServer(): Promise<WallowBffServer> {
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
 * Behind an ingress the peer is the INGRESS, so the address is resolved through
 * `clientAddressFor` rather than read off the connection: it consults the inbound
 * `X-Forwarded-For` only when the peer is a proxy `WALLOW_TRUSTED_PROXIES` names, so an
 * untrusted caller cannot stamp a chosen address into the API's rate-limit key
 * (Wallow-tvn3). The API pops the RIGHTMOST chain entry into its
 * `RemoteIpAddress`, which is this value.
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
  const clientIp: string | undefined = clientAddressFor(request);
  if (clientIp !== undefined && clientIp !== "") {
    request.headers.set(CLIENT_IP_HEADER, clientIp);
  } else {
    // Removed, not left alone. The seam header is a plain request header, so a
    // caller can send one; the proxy appends whatever it finds there to the
    // outbound chain and the API believes it. Stamping over it covers that on
    // every request that HAS a peer address, and this covers the rest.
    request.headers.delete(CLIENT_IP_HEADER);
  }
  return server.handleApi(request);
}

/** Liveness probe: 200 with a small JSON body, which both compose stacks wait on. */
export async function handleHealthRequest(): Promise<Response> {
  const server: WallowBffServer = await getBffServer();
  return server.handleHealth();
}

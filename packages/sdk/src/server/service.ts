/**
 * PROTOTYPE STUB — `@bc-solutions-coder/sdk/server/service`, the M2M subpath
 * decided in #121. Signature only; the body throws. Exists on this branch so
 * the external-RP example's consumer code typechecks against the decided shape.
 *
 * Decided contract (#121): its own subpath so a service-only worker never pulls
 * the BFF handler graph; `openid-client`'s `clientCredentialsGrant`; token
 * cached in a `RedisLike` store with the SET NX EX refresh lock (in-memory when
 * none is given); env `OIDC_SERVICE_CLIENT_ID` / `OIDC_SERVICE_CLIENT_SECRET` /
 * `OIDC_SERVICE_SCOPES` (required) sharing `OIDC_ISSUER` / `OIDC_METADATA_URL` /
 * `BFF_API_BASE_URL` / `REDIS_URL`; returns the same typed client shape as
 * `createWallowSdk()`.
 */
import type { WallowSdk } from "../create-sdk";
import type { RedisLike } from "./store/types";

export interface ServiceClientOptions {
  env?: NodeJS.ProcessEnv;
  /** Token cache; in-memory when omitted. */
  store?: RedisLike;
}

/** Same shape as a user-session SDK: generated operations take `{ client }`. */
export type WallowServiceClient = Pick<WallowSdk, "client">;

export function createServiceClient(_options: ServiceClientOptions = {}): WallowServiceClient {
  throw new Error("PROTOTYPE: createServiceClient is a stub on this branch (see #121, #127)");
}

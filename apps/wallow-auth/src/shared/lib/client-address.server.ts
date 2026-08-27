/**
 * This deployment's client-address resolution, bound once.
 *
 * The resolution itself is `@bc-solutions-coder/env/client-address`, shared with
 * wallow-web and minimal-app because getting it wrong twice in different ways is
 * the failure that produced it (Wallow-tvn3). What lives HERE is the single
 * `process.env` read: the env package must not touch the environment at module
 * scope, because every app's `start.ts` is aliased into the client module graph
 * as well as the server one.
 *
 * Bound at module scope, not per request — parsing `WALLOW_TRUSTED_PROXIES` is start-up
 * work, and a resolver rebuilt per call would redo it on every log record.
 *
 * With `WALLOW_TRUSTED_PROXIES` unset — the default, and the case for every deployment
 * with no proxy in front — this answers with the peer address srvx read off the
 * connection and consults no header at all.
 */
import {
  createClientAddressResolver,
  type PeerRequest,
} from "@bc-solutions-coder/env/client-address";

/** The caller's address, or `undefined` when the host reported no peer. */
export const clientAddressFor: (request: PeerRequest) => string | undefined =
  createClientAddressResolver(process.env);

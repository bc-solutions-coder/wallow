/**
 * `auth-extras` — the identity behaviors the generated client cannot
 * express (Wallow-pu6a.5.4).
 *
 * This module is what is LEFT of `auth-client.ts` after the generated operation
 * layer (`{op}()`, `{op}Options()`, `{op}Mutation()`) absorbed the rest. Every
 * method that only renamed a generated op, unwrapped an envelope, or mapped an
 * error is gone: `responseStyle: 'data'` + `throwOnError: true` plus the
 * `WallowError` interceptor cover all of them. What survives here is the
 * residue that no codegen flag can produce:
 *
 *   1. {@link getCurrentUser} — 401 is the ANSWER "anonymous", not a failure;
 *   2. {@link validateRedirectUriArgs} — an absent `clientId` omits the KEY.
 *
 * The second shapes ARGUMENTS rather than wrapping the call, so the same
 * helper composes with the bare operation and with its generated query
 * options, which a call wrapper could not do without re-hiding the query key.
 *
 * Nothing else belongs in this file. A new endpoint is reached by calling its
 * generated operation (or its generated query/mutation options) directly with
 * `{ client: sdk.client }` — never by adding a passthrough here.
 */

import { isWallowError } from "./errors";
import {
  type AccountValidateRedirectUriData,
  type CurrentUserResponse,
  usersGetCurrentUser,
} from "./generated";
import type { Client } from "./generated/client";

/** Call options shared by the operation-invoking helpers here. */
export interface AuthExtrasOptions {
  /**
   * The request-scoped client from `createWallowSdk()`. Passed straight to the
   * generated operation, so the call rides the caller's instance (its baseUrl,
   * its forwarded cookie) rather than any module-global one.
   */
  readonly client?: Client;
}

/** Arguments for the redirect-uri check: what {@link validateRedirectUriArgs} shapes. */
export type ValidateRedirectUriArgs = Pick<AccountValidateRedirectUriData, "query">;

/** The status the API returns for an anonymous caller: the answer, not a failure. */
const ANONYMOUS_STATUS: number = 401;

/**
 * Resolve the signed-in user, or `null` when the browser is anonymous.
 *
 * A 401 resolves `null`; every other failure throws the SAME object it arrived
 * as, so an outage can never masquerade as a signed-out user. An UNBRANDED
 * failure rethrows too, even one claiming `status: 401`: under the unified
 * error contract a non-`WallowError` means something bypassed the interceptor,
 * and that must surface rather than sign the user out.
 */
export async function getCurrentUser(
  options?: AuthExtrasOptions,
): Promise<CurrentUserResponse | null> {
  try {
    // A 200 with no body is degenerate (the endpoint always bodies a
    // CurrentUserResponse). Fall to the LESS-privileged branch rather than
    // inventing a signed-in user out of nothing.
    return (await usersGetCurrentUser(options ?? {})) ?? null;
  } catch (error: unknown) {
    if (isWallowError(error) && error.status === ANONYMOUS_STATUS) {
      return null;
    }

    throw error;
  }
}

/**
 * Shape the redirect-uri validation arguments, omitting the `clientId` KEY (not
 * sending it as `undefined`) when no client scopes the question.
 *
 * The generated client would put a bare `clientId=` on the wire, and an unknown
 * client fails CLOSED to the AuthUrl-only origin set — a different question
 * from asking unscoped.
 */
export function validateRedirectUriArgs(uri: string, clientId?: string): ValidateRedirectUriArgs {
  return clientId === undefined || clientId === ""
    ? { query: { uri } }
    : { query: { uri, clientId } };
}

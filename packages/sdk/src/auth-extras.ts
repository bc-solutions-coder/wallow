/** Convenience helpers for resolving the current user and building redirect-validation arguments. */

import { isApiFailure } from "@bc-solutions-coder/api-errors";

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
 * Fetch the current API user, returning `null` for an unauthenticated response.
 *
 * Pass `{ client: sdk.client }` to use your configured SDK instance. A 401
 * `ApiFailure` or an empty successful response returns `null`; other failures
 * are rethrown so callers can distinguish a service error from a signed-out user.
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
    if (isApiFailure(error) && error.status === ANONYMOUS_STATUS) {
      return null;
    }

    throw error;
  }
}

/**
 * Build query arguments for `accountValidateRedirectUri`.
 *
 * Omits `clientId` when it is undefined or an empty string. This helper does
 * not validate the URI or make a request; pass its result to the generated
 * operation together with `{ client: sdk.client }`.
 */
export function validateRedirectUriArgs(uri: string, clientId?: string): ValidateRedirectUriArgs {
  return clientId === undefined || clientId === ""
    ? { query: { uri } }
    : { query: { uri, clientId } };
}

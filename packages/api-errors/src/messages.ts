/**
 * From a failure to the sentence a person reads.
 *
 * {@link resolveFailureMessage} walks a fixed precedence: the call site's
 * messages, the app's registry, the copy shipped here per code, the problem's
 * own `detail` (a 4xx with an API code only), the copy shipped here per
 * status, the call site's fallback, and finally one generic sentence. It
 * always returns a string.
 */

import { ClientErrorCode, type FailureCode, isClientErrorCode } from "./codes";
import type { ApiFailure } from "./failure";
import { toApiFailure } from "./parse";

/** The sentence for one code, given the failure it is rendering. */
export type FailureMessage = (failure: ApiFailure) => string;

/**
 * Sentences keyed by code. The key type admits any string so a code the
 * catalogue does not know yet (an `OAuth.*` token, a fork's own) still
 * registers, while the known ones autocomplete.
 */
export type FailureMessageRegistry = Readonly<
  // oxlint-disable-next-line typescript/ban-types -- the known codes autocomplete, any other string still registers
  Partial<Record<FailureCode | (string & {}), FailureMessage>>
>;

export interface ResolveFailureMessageOptions {
  /** The app's registry, from {@link defineFailureMessages}. */
  readonly registry?: FailureMessageRegistry | undefined;
  /** Sentences for this call site alone; they win over the registry. */
  readonly messages?: FailureMessageRegistry | undefined;
  /** The call site's own last resort, ahead of the generic sentence. */
  readonly fallback?: string | undefined;
}

const UNAUTHORIZED: number = 401;
const FORBIDDEN: number = 403;
const NOT_FOUND: number = 404;
const CONFLICT: number = 409;
const TOO_MANY_REQUESTS: number = 429;

/** A `Retry-After` of zero (or a past date) is no wait at all. */
const NO_WAIT: number = 0;
const ONE_SECOND: number = 1;
const FIRST_CLIENT_ERROR: number = 400;
const FIRST_SERVER_ERROR: number = 500;

const GENERIC_MESSAGE: string = "Something went wrong. Please try again.";
const SERVER_SIDE_MESSAGE: string = "Something went wrong on our side. Please try again later.";
const SESSION_EXPIRED_MESSAGE: string = "Your session has expired. Please sign in again.";

/** Copy shipped per code. An app registry entry for the same code wins. */
const CODE_MESSAGES: FailureMessageRegistry = {
  [ClientErrorCode.TRANSPORT_NETWORK_ERROR]: () =>
    "Unable to reach the server. Check your connection and try again.",
  [ClientErrorCode.TRANSPORT_TIMEOUT]: () =>
    "The server took too long to respond. Please try again.",
  [ClientErrorCode.CLIENT_UNRECOGNIZED_RESPONSE]: () => SERVER_SIDE_MESSAGE,
  [ClientErrorCode.BFF_SESSION_REFRESH_FAILED]: () => SESSION_EXPIRED_MESSAGE,
};

/** Copy shipped per status, for a code nobody wrote a sentence for. */
const STATUS_MESSAGES: Readonly<Record<number, FailureMessage>> = {
  [UNAUTHORIZED]: () => SESSION_EXPIRED_MESSAGE,
  [FORBIDDEN]: () => "You don't have permission to do that.",
  [NOT_FOUND]: () => "That could not be found.",
  [CONFLICT]: () => "That change conflicts with a newer one. Refresh and try again.",
  [TOO_MANY_REQUESTS]: (failure: ApiFailure) => {
    if (failure.retryAfter === undefined || failure.retryAfter <= NO_WAIT) {
      return "Too many requests. Please wait a moment and try again.";
    }

    const unit: string = failure.retryAfter === ONE_SECOND ? "second" : "seconds";
    return `Too many requests. Please wait ${failure.retryAfter} ${unit} and try again.`;
  },
};

/**
 * A code is wire data, so it can spell an `Object.prototype` member
 * (`constructor`, `valueOf`): only an own, callable entry counts as a message.
 */
function lookup(
  table: Readonly<Record<string, FailureMessage | undefined>> | undefined,
  code: string,
): FailureMessage | undefined {
  if (table === undefined || !Object.hasOwn(table, code)) {
    return undefined;
  }

  const entry: FailureMessage | undefined = table[code];
  return typeof entry === "function" ? entry : undefined;
}

/**
 * Declares an app's registry: overrides for the codes whose shipped copy (or
 * lack of one) the app wants to replace. Identity at runtime; the value is the
 * typing.
 */
export function defineFailureMessages(entries: FailureMessageRegistry): FailureMessageRegistry {
  return entries;
}

/**
 * The sentence to show for `error`, which need not be a failure yet: anything
 * else is classified through `toApiFailure` first, so a thrown `Error` is
 * never echoed as its own message.
 */
export function resolveFailureMessage(
  error: unknown,
  options: ResolveFailureMessageOptions = {},
): string {
  const failure: ApiFailure = toApiFailure(error);

  const callSite: FailureMessage | undefined = lookup(options.messages, failure.code);
  if (callSite) {
    return callSite(failure);
  }

  const registered: FailureMessage | undefined = lookup(options.registry, failure.code);
  if (registered) {
    return registered(failure);
  }

  const shipped: FailureMessage | undefined = lookup(CODE_MESSAGES, failure.code);
  if (shipped) {
    return shipped(failure);
  }

  if (failure.detail !== undefined && isClientProblem(failure)) {
    return failure.detail;
  }

  const byStatus: FailureMessage | undefined =
    failure.status >= FIRST_SERVER_ERROR
      ? () => SERVER_SIDE_MESSAGE
      : STATUS_MESSAGES[failure.status];
  if (byStatus) {
    return byStatus(failure);
  }

  return options.fallback ?? GENERIC_MESSAGE;
}

/**
 * Whether the failure is one nothing should be shown for: the caller
 * abandoned the request, so there is no one waiting on a sentence.
 */
export function isSilentFailure(error: unknown): boolean {
  return toApiFailure(error).code === ClientErrorCode.TRANSPORT_ABORTED;
}

/**
 * A 4xx the API answered with, whose `detail` the API wrote for a person. A
 * client-minted code has no such author, and a 5xx's detail may be a stack
 * trace, so neither reaches the screen.
 */
function isClientProblem(failure: ApiFailure): boolean {
  return (
    failure.status >= FIRST_CLIENT_ERROR &&
    failure.status < FIRST_SERVER_ERROR &&
    !isClientErrorCode(failure.code)
  );
}

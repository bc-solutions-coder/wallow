/**
 * `@bc-solutions-coder/api-errors` — the Wallow API failure model, with no
 * runtime dependency and no React: the {@link ApiFailure} type, the parsers
 * that build one from a response or a thrown error, the message resolver, and
 * the field-error split for forms.
 */

export { ClientErrorCode, type FailureCode } from "./codes";
export { ApiFailure, type ApiFailureInit, isApiFailure } from "./failure";
export { type SplitFieldErrors, splitFieldErrors } from "./field-errors";
export { ErrorCode } from "./generated";
export {
  defineFailureMessages,
  type FailureMessage,
  type FailureMessageRegistry,
  failureReference,
  type FailureReference,
  isSilentFailure,
  resolveFailureMessage,
  type ResolveFailureMessageOptions,
} from "./messages";
export {
  type FailureContext,
  type FailureResponse,
  failureFromResponse,
  parseRetryAfter,
  toApiFailure,
} from "./parse";

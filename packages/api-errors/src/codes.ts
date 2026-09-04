import type { ErrorCode } from "./generated";

/**
 * The codes the client mints itself, for failures the API never answered.
 * Shaped like the generated {@link ErrorCode} object so the two read alike.
 */
export const ClientErrorCode = {
  /** The request never reached the server, or the connection dropped. */
  TRANSPORT_NETWORK_ERROR: "Transport.NetworkError",
  /** The request or its response ran out of time. */
  TRANSPORT_TIMEOUT: "Transport.Timeout",
  /** The caller abandoned the request. */
  TRANSPORT_ABORTED: "Transport.Aborted",
  /** A response arrived that carried no problem the client recognises. */
  CLIENT_UNRECOGNIZED_RESPONSE: "Client.UnrecognizedResponse",
  /** The BFF rejected the request's CSRF token. */
  BFF_CSRF_INVALID: "Bff.CsrfInvalid",
  /** The BFF could not refresh the session and tore it down. */
  BFF_SESSION_REFRESH_FAILED: "Bff.SessionRefreshFailed",
  /** The BFF found no session to attach to the request. */
  BFF_SESSION_MISSING: "Bff.SessionMissing",
} as const;

export type ClientErrorCode = (typeof ClientErrorCode)[keyof typeof ClientErrorCode];

/** Every code a failure can carry by name: the API catalogue plus the client's own. */
export type FailureCode = ErrorCode | ClientErrorCode;

const CLIENT_ERROR_CODES: ReadonlySet<string> = new Set(Object.values(ClientErrorCode));

/** Whether `code` is one the client minted rather than one the API answered with. */
export function isClientErrorCode(code: string): code is ClientErrorCode {
  return CLIENT_ERROR_CODES.has(code);
}

/**
 * The one writer behind every failure the server hop originates itself.
 *
 * Both presets — the session-owning `/api` proxy and the session-less
 * passthrough — answer their own rejections through {@link problemResponse}, so
 * a stale session, a bad CSRF token, an unknown path, or an unreachable API
 * reaches the browser wearing the API's own envelope: RFC 7807 members plus a
 * top-level `code` and the `requestId`, and never a `traceId` (there was no
 * backend trace to have gotten one from). The browser's `api-errors` parser
 * then reads an originated problem exactly as it reads a relayed one.
 *
 * The per-code copy below is the server counterpart of the failure-message
 * defaults `@bc-solutions-coder/api-errors` ships: fixed wording per case. The
 * message of the underlying fault — an undici error, an abort, a rejected grant
 * — never enters a body; it belongs in the redacted log record.
 *
 * This module is imported by the passthrough subpath, so it must stay clear of
 * the handler/proxy graph: `api-errors` and the request-id helpers only.
 */

import { ClientErrorCode, ErrorCode } from "@bc-solutions-coder/api-errors";

import { REQUEST_ID_HEADER } from "../request-id";

/** The media type of an RFC 7807 body. */
const PROBLEM_MEDIA_TYPE: string = "application/problem+json";

/** RFC 7807's "no further semantics" problem type. */
const BLANK_TYPE: string = "about:blank";

/** The copy shipped for one originated code. */
interface ProblemCopy {
  /** Short, human-readable summary — stable per code. */
  readonly title: string;
  /** One sentence for the user, fixed per case. */
  readonly detail: string;
}

const SESSION_EXPIRED_DETAIL: string = "Your session has expired. Please sign in again.";

/**
 * Title and detail per code the server hop originates: the server counterpart
 * of the failure messages `api-errors` ships, phrased for the hop that writes
 * them (a BFF that cannot reach the API is not the user's connection).
 */
const PROBLEM_COPY: Readonly<Record<string, ProblemCopy>> = {
  [ErrorCode.HTTP_NOT_FOUND]: {
    title: "Not found",
    detail: "That could not be found.",
  },
  [ClientErrorCode.BFF_SESSION_MISSING]: {
    title: "Not signed in",
    detail: "You are not signed in. Please sign in to continue.",
  },
  [ClientErrorCode.BFF_SESSION_REFRESH_FAILED]: {
    title: "The session could not be refreshed",
    detail: SESSION_EXPIRED_DETAIL,
  },
  [ClientErrorCode.BFF_CSRF_INVALID]: {
    title: "CSRF token mismatch or missing",
    detail: "The request did not carry a valid CSRF token. Reload the page and try again.",
  },
  [ErrorCode.AUTH_UNAUTHENTICATED]: {
    title: "Unauthenticated",
    detail: SESSION_EXPIRED_DETAIL,
  },
  [ClientErrorCode.TRANSPORT_NETWORK_ERROR]: {
    title: "The upstream request failed",
    detail: "The server could not be reached. Please try again later.",
  },
  [ClientErrorCode.TRANSPORT_TIMEOUT]: {
    title: "The upstream request timed out",
    detail: "The server took too long to respond. Please try again.",
  },
};

/**
 * The title {@link problemResponse} writes for `code`, for a failure that is
 * constructed before it is rendered — so the log record and the wire body
 * name the failure the same way.
 */
export function problemTitle(code: string): string {
  return Object.hasOwn(PROBLEM_COPY, code) ? PROBLEM_COPY[code].title : code;
}

/** What {@link problemResponse} needs beyond the status and code. */
export interface ProblemResponseOptions {
  /** The request id, named in the body and echoed on `x-request-id`. */
  requestId: string;
  /** Replaces the shipped detail for this code; still fixed wording, never a fault's message. */
  detail?: string;
  /**
   * Headers accumulated before the failure — typically the session cookies the
   * hop wrote for itself — copied onto the response so they are not lost on the
   * error path. Never mutated.
   */
  headers?: Headers;
}

/**
 * Render an originated problem: an RFC 7807 body in the API's envelope with a
 * top-level `code`, `application/problem+json`, and the request id on both the
 * body and the header.
 *
 * @param status The HTTP status the hop answers with.
 * @param code The machine-readable code, from `ErrorCode` or `ClientErrorCode`.
 * @param options The request id, an optional detail override, and any headers
 *   already accumulated for the response.
 */
export function problemResponse(
  status: number,
  code: string,
  options: ProblemResponseOptions,
): Response {
  const copy: ProblemCopy | undefined = Object.hasOwn(PROBLEM_COPY, code)
    ? PROBLEM_COPY[code]
    : undefined;
  const detail: string | undefined = options.detail ?? copy?.detail;
  const title: string = copy?.title ?? code;

  const headers: Headers = new Headers(options.headers);
  headers.set("content-type", PROBLEM_MEDIA_TYPE);
  headers.set(REQUEST_ID_HEADER, options.requestId);

  return Response.json(
    {
      type: BLANK_TYPE,
      title,
      status,
      code,
      ...(detail === undefined ? {} : { detail }),
      requestId: options.requestId,
    },
    { status, headers },
  );
}

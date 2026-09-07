/**
 * Create problem responses for locally originated BFF and passthrough failures. Public bodies use
 * fixed messages and requestId; transport details stay in logs. Keep this module independent of
 * the BFF handler graph for the passthrough entrypoint.
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
  [ErrorCode.VALIDATION_FAILED]: {
    title: "Validation failed",
    detail: "The request is invalid. Check the request and try again.",
  },
  [ErrorCode.HTTP_METHOD_NOT_ALLOWED]: {
    title: "Method not allowed",
    detail: "This HTTP method is not allowed for this endpoint.",
  },
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
 * Create an application/problem+json response for a server-originated failure.
 *
 * Includes about:blank, status, code, a fixed title, and requestId in both body and header.
 * Copies optional response headers without mutating them. Known codes receive default detail
 * text; unknown codes use the code as title.
 *
 * @param status HTTP failure status.
 *
 * @param code Machine-readable ErrorCode or ClientErrorCode value.
 *
 * @param options Request correlation, optional fixed detail text, and headers such as
 * Set-Cookie.
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

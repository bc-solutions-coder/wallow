import {
  type ApiFailure,
  failureFromResponse,
  isApiFailure,
  parseRetryAfter,
  toApiFailure,
} from "@bc-solutions-coder/api-errors";

import type { CreateClientConfig } from "./generated/client.gen";
import { REQUEST_ID_HEADER } from "./request-id";

/** The header a 429 or 503 names its back-off in; the client drops it with the body. */
const RETRY_AFTER_HEADER: string = "retry-after";

/**
 * Runtime configuration hook called by the generated client at construction.
 *
 * Wired in via `runtimeConfigPath` in `openapi-ts.config.ts` so the client the
 * generated SDK operations use is the same one we configure for the BFF.
 */
export const createClientConfig: CreateClientConfig = (config) => ({
  ...config,
  baseUrl: "/api",
  credentials: "include",
});

/**
 * The subset of the generated `@hey-api` client this module wires the error
 * interceptor onto. Kept structural — mirroring {@link CsrfInterceptorClient} —
 * so the real client is assignable without importing its concrete type here.
 */
export interface ApiFailureInterceptorClient {
  interceptors: {
    error: {
      use: (interceptor: (error: unknown, response: Response | undefined) => unknown) => unknown;
    };
  };
}

/**
 * Normalize whatever a failed generated operation threw into an
 * {@link ApiFailure}, using `@bc-solutions-coder/api-errors`' parser.
 *
 * With `throwOnError: true` the generated client throws the PARSED response
 * body — an object when the body was JSON, the raw text otherwise, and, for a
 * transport fault, the `Error` `fetch` rejected with. The `Response` (when one
 * arrived) is the only place the status, the `x-request-id` the BFF echoed, and
 * a `Retry-After` are still reachable: the client has already consumed the
 * body, so those three are read here and handed to the package beside it.
 */
export function toFailure(error: unknown, response: Response | undefined): ApiFailure {
  // Interceptors chain, and the BFF tunnel already normalizes server-side;
  // re-wrapping would bury a real code under an unrecognised-response one.
  if (isApiFailure(error)) {
    return error;
  }

  // No response at all: the request never landed, and the package classifies
  // the rejection (timeout, abort, or a dropped connection) from the error.
  if (response === undefined) {
    return toApiFailure(error);
  }

  // The raw text the client threw for a non-JSON body still has to be parsed
  // by the package (it may be problem+json under the wrong content type).
  if (typeof error === "string") {
    return failureFromResponse(response, error);
  }

  // Everything else arrived WITH a response: the body the client already
  // parsed, nothing for an empty body, or the error thrown reading it. The
  // status is real either way, so a server that answered is never reported as
  // unreachable, and the headers the client dropped ride along.
  return toApiFailure(error, {
    status: response.status,
    requestId: response.headers.get(REQUEST_ID_HEADER) ?? undefined,
    retryAfter: parseRetryAfter(response.headers.get(RETRY_AFTER_HEADER)),
  });
}

/**
 * Register the error interceptor on the given client, so every operation's
 * failure path rejects with an {@link ApiFailure} instead of a raw body.
 *
 * The transport status is only reachable here: the generated client hands the
 * error interceptor the `Response` before it throws, and a bare 401 with an
 * empty body carries its status nowhere else. The same is true of the
 * `x-request-id` the BFF echoed and of `Retry-After` — they are headers, so
 * the parsed body the client throws has already lost them.
 */
export function wireApiFailureInterceptor(client: ApiFailureInterceptorClient): void {
  client.interceptors.error.use((error: unknown, response: Response | undefined): unknown =>
    toFailure(error, response),
  );
}

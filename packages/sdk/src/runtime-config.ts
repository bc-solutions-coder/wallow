import { WallowError, isWallowError } from "./errors";
import type { CreateClientConfig } from "./generated/client.gen";
import { REQUEST_ID_HEADER } from "./request-id";

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
export interface WallowErrorInterceptorClient {
  interceptors: {
    error: {
      use: (interceptor: (error: unknown, response: Response | undefined) => unknown) => unknown;
    };
  };
}

/** Code attributed to a failure that carries no machine-readable code. */
const UNKNOWN_ERROR_CODE: string = "UNKNOWN";

/** Title attributed to a failure whose body carries no problem details. */
const UNKNOWN_ERROR_TITLE: string = "Unknown error";

/** Status attributed to a failure that names no status anywhere. */
const FALLBACK_ERROR_STATUS: number = 500;

/**
 * The status and code attributed to a request that never produced a response at
 * all.
 *
 * Deliberately the SAME pair `server/proxy.ts` raises when the BFF's own forward
 * to the API fails (`NETWORK_FAILURE_STATUS` / `NETWORK_ERROR_CODE`, exported
 * from the `./server` entry), so "the request never landed" is ONE contract
 * whichever leg of the tunnel dropped it — a spec pins the two codes in
 * agreement, as one already does for {@link UNKNOWN_ERROR_CODE}.
 *
 * A synthesized 500 would be indistinguishable from a server that answered 500,
 * and that distinction is load-bearing: a screen owes a user with no network
 * different copy than one whose server said no, and telling the first to re-read
 * their password is useless advice.
 */
const NETWORK_FAILURE_STATUS: number = 503;
const NETWORK_ERROR_CODE: string = "NETWORK_ERROR";

/**
 * Normalize whatever a failed generated operation threw into a
 * {@link WallowError}.
 *
 * With `throwOnError: true` the generated client throws the PARSED response
 * body, which is an RFC 7807 problem details object for most endpoints and a
 * bare `{ succeeded: false, error }` object for the Identity auth and MFA
 * endpoints — and, for a network fault or an empty error body, not an object at
 * all. This is the one place that difference is erased.
 *
 * `responseStatus` is the transport status, used when the body names none.
 * `requestId` is the `x-request-id` the BFF echoed on the response — the browser
 * cannot recover it from the parsed body, so the interceptor reads it off the
 * `Response` and hands it in here (Wallow-pu6a.6.7). The backend `traceId` needs
 * no parameter: the API already writes it into the problem details body.
 */
export function toWallowError(
  error: unknown,
  responseStatus?: number,
  requestId?: string,
): WallowError {
  // Interceptors chain, and the BFF tunnel already normalizes server-side;
  // re-wrapping would bury a real code under UNKNOWN.
  if (isWallowError(error)) {
    return error;
  }

  // A transport fault (no response, no body) still has to arrive as a
  // WallowError. Probed BEFORE the plain-object branch, which an Error would
  // otherwise satisfy while carrying none of the members it looks for.
  //
  // `responseStatus` separates the two ways an Error reaches here. ABSENT means
  // the request never landed — the NETWORK_ERROR case. PRESENT means a response
  // DID arrive and only its body could not be read (a parse failure); that is
  // not a network fault, so it keeps the real transport status and the ordinary
  // unknown code rather than claiming the server was unreachable.
  if (error instanceof Error) {
    const neverLanded: boolean = responseStatus === undefined;

    return new WallowError({
      status: responseStatus ?? NETWORK_FAILURE_STATUS,
      code: neverLanded ? NETWORK_ERROR_CODE : UNKNOWN_ERROR_CODE,
      title: UNKNOWN_ERROR_TITLE,
      detail: error.message,
      requestId,
    });
  }

  const problem: Record<string, unknown> = isPlainObject(error) ? error : {};
  const status: unknown = problem["status"];
  const title: unknown = problem["title"];
  const detail: unknown = problem["detail"];

  return new WallowError({
    status: typeof status === "number" ? status : (responseStatus ?? FALLBACK_ERROR_STATUS),
    code: readCode(problem) ?? UNKNOWN_ERROR_CODE,
    title: typeof title === "string" ? title : UNKNOWN_ERROR_TITLE,
    detail: typeof detail === "string" ? detail : undefined,
    requestId,
    traceId: readTraceId(problem),
  });
}

/**
 * Recover the backend's W3C trace id from a parsed error body, preferring
 * `extensions.traceId` over a flattened top-level one for the same reason
 * {@link readCode} prefers `extensions.code`: the extensions placement is the
 * RFC 7807 one, and which of the two the API emits depends on its problem
 * details serializer. Non-string members are ignored rather than coerced.
 */
function readTraceId(problem: Record<string, unknown>): string | undefined {
  const extensions: unknown = problem["extensions"];
  if (isPlainObject(extensions) && typeof extensions["traceId"] === "string") {
    return extensions["traceId"];
  }

  const traceId: unknown = problem["traceId"];
  return typeof traceId === "string" ? traceId : undefined;
}

/**
 * Recover the machine-readable error code from a parsed error body.
 *
 * Three placements are probed, most authoritative first:
 * 1. `extensions.code` — RFC 7807 as ASP.NET Core emits it;
 * 2. `code` — the same, for serializer setups that flatten extension members;
 * 3. `error` — the Identity auth and MFA controllers do NOT emit problem details
 *    at all; they answer with a bare `{ succeeded: false, error }` anonymous
 *    object, so the code arrives under `error`. Probed last so real problem
 *    details always win.
 *
 * Non-string members are ignored rather than coerced: OAuth-style bodies can
 * carry an object under `error`, and a stringified object is not a code.
 */
function readCode(problem: Record<string, unknown>): string | undefined {
  const extensions: unknown = problem["extensions"];
  if (isPlainObject(extensions) && typeof extensions["code"] === "string") {
    return extensions["code"];
  }

  const code: unknown = problem["code"];
  if (typeof code === "string") {
    return code;
  }

  const error: unknown = problem["error"];
  return typeof error === "string" ? error : undefined;
}

function isPlainObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Register the error interceptor on the given client, so every operation's
 * failure path rejects with a {@link WallowError} instead of a raw body.
 *
 * The transport status is only reachable here: the generated client hands the
 * error interceptor the `Response` before it throws, and a bare 401 with an
 * empty body carries its status nowhere else. The same is true of the
 * `x-request-id` the BFF echoed — it is a header, so the parsed body the client
 * throws has already lost it (Wallow-pu6a.6.7).
 */
export function wireWallowErrorInterceptor(client: WallowErrorInterceptorClient): void {
  client.interceptors.error.use((error: unknown, response: Response | undefined): unknown =>
    toWallowError(error, response?.status, response?.headers.get(REQUEST_ID_HEADER) ?? undefined),
  );
}

/** Create isolated browser or server-request clients for generated API operations. */
import { wireCsrfInterceptor } from "./csrf";
import { type Client, createClient, createConfig } from "./generated/client";
import type { ClientOptions } from "./generated/types.gen";
import { wireApiFailureInterceptor } from "./runtime-config";

/** Options for {@link createWallowSdk}. */
export interface CreateWallowSdkOptions {
  /** API base URL: `/api` for a browser BFF, or an absolute URL for server calls. */
  baseUrl: string;
  /**
   * Reachable server origin used for SSR transport while retaining `baseUrl`
   * in query-cache identities. Replaces only the outgoing request's origin.
   */
  internalOrigin?: string | undefined;
  /** Transport to send through. Defaults to `globalThis.fetch`. */
  fetch?: typeof globalThis.fetch | undefined;
  /** Incoming SSR `Cookie` header, forwarded on every request from this instance. */
  cookieHeader?: string | undefined;
  /**
   * Echo the browser BFF CSRF cookie on state-changing requests. Defaults to
   * `true`; use `false` for an API passthrough without a BFF session. During SSR,
   * forward any required CSRF header explicitly: the interceptor reads `document`.
   */
  csrf?: boolean | undefined;
}

/** A request-scoped Wallow SDK instance. */
export interface WallowSdk {
  /**
   * The generated client this instance owns. Pass it to any generated operation
   * as the `{ client }` call option to route that call through this instance.
   */
  readonly client: Client;
}

/**
 * Create an isolated client for calling Wallow API operations.
 *
 * Pass the returned `client` to generated functions as `{ client: sdk.client }`.
 * Use `/api` for a browser BFF, or an absolute BFF URL and the incoming
 * `cookieHeader` for SSR. Create a new instance for each server request.
 * Requests include credentials and use the SDK's `ApiFailure` error handling.
 *
 * @example
 * const sdk = createWallowSdk({ baseUrl: "/api" });
 * const user = await usersGetCurrentUser({ client: sdk.client });
 *
 * @throws Error when `baseUrl` is empty or whitespace.
 */
export function createWallowSdk(options: CreateWallowSdkOptions): WallowSdk {
  if (options.baseUrl.trim() === "") {
    throw new Error(
      'createWallowSdk requires a non-empty baseUrl: the browser passes the same-origin BFF path (e.g. "/api") and an SSR render passes an absolute origin.',
    );
  }

  const transport: typeof globalThis.fetch = options.fetch ?? globalThis.fetch;
  const cookieHeader: string | undefined = options.cookieHeader;
  const internalOrigin: string | undefined =
    options.internalOrigin === undefined || options.internalOrigin === ""
      ? undefined
      : options.internalOrigin;

  // Both per-request concerns ride on the `fetch` seam rather than on
  // interceptors: `internalOrigin` must stay invisible to the interceptor chain
  // (it runs before transport, and the request it sees is the request identity
  // an SSR-primed cache shares with the browser), and keeping `cookieHeader` on
  // the same seam leaves CSRF as the instance's only interceptor.
  const send: typeof globalThis.fetch = async (
    input: RequestInfo | URL,
    init?: RequestInit,
  ): Promise<Response> => {
    const request: Request = input instanceof Request ? input : new Request(input, init);
    if (cookieHeader !== undefined) {
      request.headers.set("cookie", cookieHeader);
    }
    return transport(
      internalOrigin === undefined ? request : await retarget(request, internalOrigin),
    );
  };

  const client: Client = createClient(
    createConfig<ClientOptions>({
      baseUrl: options.baseUrl,
      credentials: "include",
      fetch: send,
      // Parity with the generated default client, which the codegen config now
      // emits with `throwOnError: true`: every operation rejects on a non-2xx so
      // there is ONE error path. Without it a per-instance call would resolve
      // `undefined` on failure (the `responseStyle: "data"` no-throw branch).
      throwOnError: true,
    }),
  );
  if (options.csrf ?? true) {
    wireCsrfInterceptor(client);
  }
  // D14: the error interceptor is DEFINED in `runtime-config.ts` but registered
  // per instance here. `createClientConfig` cannot do it — it returns a config,
  // and no client exists yet to hang an interceptor on.
  wireApiFailureInterceptor(client);

  return { client };
}

/**
 * Rebuild `request` against `origin`, keeping method, path, query, headers and
 * body intact.
 *
 * The body must be BUFFERED rather than handed straight to the new `Request`: a
 * `Request` passed as a `RequestInit` contributes its `body` as a stream, which
 * a same-realm construction refuses without half-duplex support, so the payload
 * would silently vanish on POST/PUT/PATCH.
 */
async function retarget(request: Request, origin: string): Promise<Request> {
  const source: URL = new URL(request.url);
  const target: URL = new URL(`${source.pathname}${source.search}${source.hash}`, origin);
  const body: ArrayBuffer | undefined =
    request.body === null ? undefined : await request.arrayBuffer();

  return new Request(target, {
    body,
    credentials: request.credentials,
    headers: request.headers,
    method: request.method,
    redirect: request.redirect,
    signal: request.signal,
  });
}

/**
 * Per-request SDK factory (Wallow-pu6a.3.5).
 *
 * The SDK's original model was a module-global client singleton: `client.ts`
 * constructs ONE generated `@hey-api` client at import time and every generated
 * operation calls through it, so `configureBffClient()`/`configureSsrClient()`
 * mutate shared state. That is safe in a browser (one document, one session) and
 * WRONG on a server, where concurrent renders for different users share the
 * module graph — the last request to configure the singleton wins, its cookie
 * leaks into another render, and interceptors accumulate on every re-configure.
 *
 * `createWallowSdk()` replaces that with an instance built per request: its own
 * generated client, its own `baseUrl`, its own interceptor list, its own
 * forwarded cookie. Generated operations bind to it through the standard
 * `{ client }` call option, e.g.
 * `usersGetCurrentUser({ client: sdk.client })`.
 */
import { wireCsrfInterceptor } from "./csrf";
import { type Client, createClient, createConfig } from "./generated/client";
import type { ClientOptions } from "./generated/types.gen";
import { wireWallowErrorInterceptor } from "./runtime-config";

/** Options for {@link createWallowSdk}. */
export interface CreateWallowSdkOptions {
  /**
   * Base URL every generated operation resolves against. REQUIRED — the factory
   * bakes in no `/api` default, because the correct value differs per caller
   * (the browser wants the same-origin relative BFF path, an SSR render wants an
   * absolute origin Node's `fetch` can parse).
   */
  baseUrl: string;
  /**
   * Origin the SSR host can reach ITSELF on, when that differs from the
   * browser-facing origin embedded in {@link baseUrl} (Wallow-spb5). Applied
   * ONLY inside the instance's `fetch` — it rewrites the outgoing request's
   * origin and leaves the client's configured `baseUrl` (and therefore every
   * request identity derived from it) untouched, so a server instance and a
   * browser instance built with the same `baseUrl` stay hydration-compatible.
   */
  internalOrigin?: string | undefined;
  /** Transport to send through. Defaults to `globalThis.fetch`. */
  fetch?: typeof globalThis.fetch | undefined;
  /**
   * The incoming request's `Cookie` header, forwarded on every outgoing request.
   * Node's `fetch` has no cookie jar, so an SSR render must carry the session
   * cookie explicitly. Captured per instance, never read from module scope.
   */
  cookieHeader?: string | undefined;
  /**
   * Whether to wire the CSRF request interceptor onto the instance. Defaults to
   * `true` — the BFF topology, where the `/api` proxy rejects a state-changing
   * request that does not echo the double-submit cookie. Pass `false` in a
   * passthrough topology (wallow-auth): there is no BFF session and no CSRF
   * cookie of its own, and behind a shared-hostname ingress the jar can hold
   * ANOTHER app's `-csrf` cookie, which the interceptor would happily stamp onto
   * requests whose upstream never asked for it.
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
 * Build a request-scoped SDK instance over the generated client factory.
 *
 * Every call constructs a FRESH generated client — no module-global state is
 * read or written — wires the CSRF interceptor onto it exactly once (unless
 * `csrf: false` opts the topology out), and applies `internalOrigin` inside the
 * instance's `fetch` only.
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
  wireWallowErrorInterceptor(client);

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

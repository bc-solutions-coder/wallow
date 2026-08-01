/**
 * Shared SDK test seam (Wallow-pu6a.5.1) — a fake transport handed to the REAL
 * `createWallowSdk()` factory.
 *
 * The rule this encodes is the same one `.claude/rules/TESTING.md` already
 * states for `@bc-solutions-coder/ui`: specs drive the real implementation, not
 * a hand-rolled stand-in. So nothing here mocks the SDK module — no
 * `vi.mock("@bc-solutions-coder/sdk")`, no fake facade object with the four
 * methods a screen happens to call. The harness builds a genuine `WallowSdk`
 * whose only substitution is `fetch`, which means the whole pipeline the app
 * ships (generated operation -> CSRF interceptor -> request serialization ->
 * response parsing -> TanStack Query cache) executes in the spec, while the
 * test still owns the wire result and can read the outgoing request back.
 *
 * It supersedes `apps/wallow-web/src/test/sdk-client-mock.ts`, which injected
 * `fetch` into the SDK's MODULE-GLOBAL client via `client.setConfig()`. That
 * singleton is deleted in Wallow-pu6a.5.5, so the injection point moves to the
 * per-request factory. Until that deletion lands, screens that still resolve
 * their SDK through the old singleton can be bridged with `legacyClients` (see
 * below) — deliberately typed structurally so this package never imports the
 * deprecated symbol and the bridge disappears from call sites, not from here.
 *
 * No `vitest` import: the recorder is a plain closure, so this module stays
 * usable from the node project, the browser project and the storybook project
 * alike, and importing it costs nothing at Vitest config-load time.
 */
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";

/**
 * Base URL every harness-built SDK resolves against unless overridden.
 *
 * ABSOLUTE on purpose. The apps pass a relative `/api` in the browser, but this
 * harness also runs on the node project, where `new Request("/api/...")` throws
 * on a relative URL. A fake absolute origin works identically in both and keeps
 * recorded `call.url` values stable to assert on.
 */
export const DEFAULT_HARNESS_BASE_URL = "http://wallow.test/api";

/** One recorded outgoing request, decoded for assertions. */
export interface SdkCall {
  /** Uppercase HTTP method, e.g. `POST`. */
  method: string;
  /** Request pathname only, e.g. `/api/v1/identity/organizations`. */
  path: string;
  /** Full request URL including query string. */
  url: string;
  /** Parsed JSON body, multipart fields as an object, the raw string, or `undefined`. */
  body: unknown;
  /** Outgoing request headers, lowercased keys. */
  headers: Readonly<Record<string, string>>;
}

/** Per-request responder installed by {@link SdkHarness.respond}. */
export type SdkResponder = (call: SdkCall) => Response | Promise<Response>;

/**
 * A client whose transport can be reprogrammed after construction.
 *
 * TRANSITIONAL (removed with Wallow-pu6a.5.5). Structural rather than an import
 * of the SDK's deprecated module-global `client`, so deleting that singleton
 * touches the specs that opt in, never this package.
 */
export interface LegacyConfigurableClient {
  setConfig: (config: { fetch?: typeof globalThis.fetch }) => unknown;
}

/** Options for {@link createSdkHarness}. */
export interface SdkHarnessOptions {
  /** Base URL for the SDK instance. Defaults to {@link DEFAULT_HARNESS_BASE_URL}. */
  baseUrl?: string | undefined;
  /**
   * Extra already-constructed clients to point at this harness's transport.
   *
   * TRANSITIONAL: the escape hatch for screens that still reach the SDK through
   * the deprecated module singleton instead of the router context. Pass
   * `[client]` (imported from `@bc-solutions-coder/sdk` by the spec) until
   * Wallow-pu6a.5.5 deletes it, then drop the option.
   */
  legacyClients?: readonly LegacyConfigurableClient[] | undefined;
}

/** Handle returned by {@link createSdkHarness}. */
export interface SdkHarness {
  /** The real SDK instance, built by `createWallowSdk` over the fake transport. */
  readonly sdk: WallowSdk;
  /** `sdk.client` — pass as the `{ client }` call option to any generated operation. */
  readonly client: WallowSdk["client"];
  /** Every request recorded so far, in order. */
  readonly calls: readonly SdkCall[];
  /** The most recent recorded request, or `undefined`. */
  readonly last: SdkCall | undefined;
  /** The recording transport itself, for wiring a second SDK instance by hand. */
  readonly fetch: typeof globalThis.fetch;
  /** All subsequent requests resolve with `data` as a JSON body at `status` (default 200). */
  resolveJson: (data?: unknown, status?: number) => void;
  /** All subsequent requests resolve non-2xx with `errorBody` as JSON at `status` (default 400). */
  rejectJson: (errorBody: unknown, status?: number) => void;
  /** All subsequent requests never settle — drives loading/pending assertions. */
  pending: () => void;
  /** Install a custom per-request responder. */
  respond: (responder: SdkResponder) => void;
  /** Clear recorded calls and restore the default `{}`-at-200 responder. */
  reset: () => void;
}

const OK_STATUS = 200;
const ERROR_STATUS = 400;
const LAST_INDEX = -1;

function jsonResponse(data: unknown, status: number): Response {
  return Response.json(data ?? null, { status });
}

/**
 * Decode an outgoing body for assertions: multipart as a field object, JSON as a
 * value, anything else as its raw text, and a body-less request as `undefined`
 * (NOT `""`, which reads as "sent an empty body" in a spec).
 *
 * Always off a `clone()` — the real transport still has to read the original.
 */
async function decodeBody(request: Request): Promise<unknown> {
  const contentType: string = request.headers.get("content-type") ?? "";
  if (contentType.includes("multipart/form-data")) {
    try {
      const form: FormData = await request.clone().formData();
      return Object.fromEntries(form.entries());
    } catch {
      return undefined;
    }
  }

  const text: string = await request.clone().text();
  if (text === "") {
    return undefined;
  }
  try {
    return JSON.parse(text) as unknown;
  } catch {
    return text;
  }
}

/**
 * Build a {@link SdkHarness}: a real `WallowSdk` whose transport is a recording
 * fake the spec programs.
 */
export function createSdkHarness(options: SdkHarnessOptions = {}): SdkHarness {
  const calls: SdkCall[] = [];
  const defaultResponder: SdkResponder = () => jsonResponse({}, OK_STATUS);
  let responder: SdkResponder = defaultResponder;

  // Recording happens BEFORE the responder runs, which is what makes `pending()`
  // useful: the request is on `calls` for the spec to assert on even though its
  // promise never settles.
  const transport: typeof globalThis.fetch = async (
    input: RequestInfo | URL,
    init?: RequestInit,
  ): Promise<Response> => {
    const request: Request = input instanceof Request ? input : new Request(input, init);
    const call: SdkCall = {
      method: request.method,
      path: new URL(request.url).pathname,
      url: request.url,
      body: await decodeBody(request),
      headers: Object.freeze(Object.fromEntries(request.headers.entries())),
    };
    calls.push(call);
    return responder(call);
  };

  const sdk: WallowSdk = createWallowSdk({
    baseUrl: options.baseUrl ?? DEFAULT_HARNESS_BASE_URL,
    fetch: transport,
  });

  for (const legacyClient of options.legacyClients ?? []) {
    legacyClient.setConfig({ fetch: transport });
  }

  return {
    sdk,
    client: sdk.client,
    get calls(): readonly SdkCall[] {
      return calls;
    },
    get last(): SdkCall | undefined {
      return calls.at(LAST_INDEX);
    },
    fetch: transport,
    resolveJson(data?: unknown, status: number = OK_STATUS): void {
      responder = () => jsonResponse(data, status);
    },
    rejectJson(errorBody: unknown, status: number = ERROR_STATUS): void {
      responder = () => jsonResponse(errorBody, status);
    },
    pending(): void {
      responder = () => new Promise<Response>(() => {});
    },
    respond(next: SdkResponder): void {
      responder = next;
    },
    reset(): void {
      calls.length = 0;
      responder = defaultResponder;
    },
  };
}

/**
 * Base URL for an app served by the PASSTHROUGH proxy rather than the BFF.
 *
 * `createApiPassthrough` mounts `/v1/**` and `/connect/**` at the site root, and
 * such an app builds its SDK with `baseUrl: globalThis.location.origin` to match
 * — so a harness on {@link DEFAULT_HARNESS_BASE_URL} would record `/api/v1/...`
 * paths it never issues. Same fake origin, no path prefix.
 */
export const PASSTHROUGH_HARNESS_BASE_URL = "http://wallow.test";

/** A harness whose recorded `call.path` matches a passthrough-hosted app's requests. */
export function createPassthroughHarness(): SdkHarness {
  return createSdkHarness({ baseUrl: PASSTHROUGH_HARNESS_BASE_URL });
}

/**
 * Multi-route programming, re-exported so a spec reaches the whole harness
 * through one specifier.
 */
export {
  failsWith,
  neverSettles,
  routeHarness,
  type HarnessRouteResponse,
  type HarnessRoutes,
  type RouteHarnessOptions,
} from "./harness-routes";

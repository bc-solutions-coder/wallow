/**
 * Shared browser-test seam for the SDK query layer (Wallow-evd5.2.6).
 *
 * Since the feature `api.ts` files became thin re-exports of
 * `@bc-solutions-coder/sdk/query` (Wallow-evd5.2.2), component specs can no
 * longer mock the retired `getWallowSdk()` facade — the SDK query factories call
 * the generated ops on the shared `@hey-api` `client` directly. This helper
 * overrides that client's `fetch`, so the REAL query/mutation pipeline (bootstrap
 * -> generated op -> `unwrap` -> React Query invalidation) runs end to end while
 * the test controls the network result and inspects the outgoing request.
 *
 * The browser vitest project does not inline the SDK, so mocking the generated
 * ops (an internal `../generated` import) is not possible; the client's `fetch`
 * config is the stable public seam. Component specs never import
 * `src/lib/wallow-sdk`, so the query bootstrap has no configurator registered and
 * `ensureQueryBootstrapped()` is inert — this injected fetch fully governs I/O.
 */
import { client } from "@bc-solutions-coder/sdk";
import { type Mock, vi } from "vitest";

/** One recorded outgoing request, decoded for assertions. */
export interface SdkCall {
  method: string;
  /** Request pathname only (e.g. `/v1/identity/organizations/o1/members`). */
  path: string;
  /** Full request URL. */
  url: string;
  /** Parsed JSON body, multipart fields as an object, or the raw string. */
  body: unknown;
}

/** Handle returned by {@link installSdkClientMock}. */
export interface SdkClientMock {
  /** Every request recorded so far, in order. */
  readonly calls: SdkCall[];
  /** The most recent recorded request, or `undefined`. */
  readonly last: SdkCall | undefined;
  /** The underlying `fetch` spy. */
  readonly fetchMock: Mock;
  /** All subsequent requests resolve with `data` as a JSON body at `status`. */
  resolveJson: (data?: unknown, status?: number) => void;
  /** All subsequent requests reject (non-2xx) with `errorBody` as JSON at `status`. */
  rejectJson: (errorBody: unknown, status?: number) => void;
  /** All subsequent requests never settle — drives loading/pending states. */
  pending: () => void;
  /** Install a custom per-request responder. */
  respond: (fn: (call: SdkCall) => Response | Promise<Response>) => void;
}

const DEFAULT_STATUS = 200;
const ERROR_STATUS = 400;
const LAST_INDEX = -1;

function jsonResponse(data: unknown, status: number): Response {
  return Response.json(data ?? null, { status });
}

async function decodeBody(request: Request): Promise<unknown> {
  const contentType = request.headers.get("content-type") ?? "";
  if (contentType.includes("multipart/form-data")) {
    try {
      const form = await request.clone().formData();
      return Object.fromEntries(form.entries());
    } catch {
      return undefined;
    }
  }
  const text = await request.clone().text();
  if (text === "") {
    return undefined;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

/**
 * Override the shared SDK client's `fetch` with a recording spy. Call in a
 * `beforeEach`; requests default to resolving `{}` at 200 until a `resolveJson`/
 * `rejectJson`/`pending`/`respond` call reprograms the responder.
 */
export function installSdkClientMock(): SdkClientMock {
  const calls: SdkCall[] = [];
  let responder: (call: SdkCall) => Response | Promise<Response> = () =>
    jsonResponse({}, DEFAULT_STATUS);

  const fetchMock = vi.fn(async (input: Request): Promise<Response> => {
    const request = input;
    const call: SdkCall = {
      method: request.method,
      path: new URL(request.url, "http://localhost").pathname,
      url: request.url,
      body: await decodeBody(request),
    };
    calls.push(call);
    return responder(call);
  });

  client.setConfig({ fetch: fetchMock as unknown as typeof fetch });

  return {
    calls,
    get last() {
      return calls.at(LAST_INDEX);
    },
    fetchMock,
    resolveJson(data?: unknown, status = DEFAULT_STATUS) {
      responder = () => jsonResponse(data, status);
    },
    rejectJson(errorBody: unknown, status = ERROR_STATUS) {
      responder = () => jsonResponse(errorBody, status);
    },
    pending() {
      responder = () => new Promise<Response>(() => {});
    },
    respond(fn: (call: SdkCall) => Response | Promise<Response>) {
      responder = fn;
    },
  };
}

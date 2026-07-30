/**
 * Path routing for the shared SDK harness (Wallow-pu6a.5.5).
 *
 * `createSdkHarness()` programs ONE responder at a time, which is all a spec
 * driving a single query needs. Several dashboard screens read two or three
 * operations at once (an organization plus its members plus its clients), and
 * before the hand-written query layer was deleted those specs pre-seeded each
 * one through `queryClient.setQueryData(['orgs', 'o1', 'members'], ...)` — a
 * hierarchical key that no longer exists, and a shortcut that skipped the
 * request pipeline entirely.
 *
 * This replaces that seeding with the thing it was standing in for: the wire.
 * Each entry is keyed `"<METHOD> <path>"` and matched against the request the
 * SDK actually issued, so the generated operation, the interceptors, the
 * response parsing and the React Query cache all run exactly as they do in the
 * app — and an operation the spec forgot to program shows up as an unmatched
 * request instead of silently reading seeded data.
 */
import type { SdkCall, SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";

/** Status returned for a matched route, and for the fallback. */
const OK_STATUS = 200;

/** Status returned when no entry matches — a programming error in the spec. */
const NOT_FOUND_STATUS = 404;

/** Marks a route value as a descriptor rather than a plain body. */
const ROUTE_RESPONSE = Symbol("harness-route-response");

/** A non-200 answer, or one that never arrives. See {@link failsWith}/{@link neverSettles}. */
export interface HarnessRouteResponse {
  readonly [ROUTE_RESPONSE]: true;
  readonly body: unknown;
  readonly status: number;
  readonly settles: boolean;
}

/**
 * Answer this route non-2xx — the branch a spec needs when one operation on a
 * loaded screen must fail while the reads behind it keep succeeding.
 */
export function failsWith(body: unknown, status: number): HarnessRouteResponse {
  return { [ROUTE_RESPONSE]: true, body, status, settles: true };
}

/**
 * Never answer this route, leaving its query pending. Scoped to one operation,
 * unlike `harness.pending()`, which suspends every request on the screen.
 */
export function neverSettles(): HarnessRouteResponse {
  return { [ROUTE_RESPONSE]: true, body: null, status: OK_STATUS, settles: false };
}

function isRouteResponse(value: unknown): value is HarnessRouteResponse {
  return typeof value === "object" && value !== null && ROUTE_RESPONSE in value;
}

/**
 * Bodies to answer with, keyed `"<METHOD> <path>"` — e.g.
 * `"GET /v1/identity/organizations/o1/members"`. Paths are matched as a suffix
 * of the request pathname, so the harness's `/api` base URL prefix is implicit.
 * A value may also be a {@link failsWith} / {@link neverSettles} descriptor.
 */
export type HarnessRoutes = Readonly<Record<string, unknown>>;

/** Options for {@link routeHarness}. */
export interface RouteHarnessOptions {
  /**
   * Body for any request no entry matches. Omit to have unmatched requests fail
   * with a 404, which is what a spec normally wants: it names the operation it
   * did not expect instead of letting a screen render off `{}`.
   */
  fallback?: unknown;
}

interface HarnessRoute {
  method: string;
  path: string;
  body: unknown;
}

function parse(routes: HarnessRoutes): HarnessRoute[] {
  return Object.entries(routes).map(([spec, body]): HarnessRoute => {
    const [method = "", path = ""] = spec.split(" ");
    return { method: method.toUpperCase(), path, body };
  });
}

/**
 * Program `harness` to answer each request from `routes`.
 *
 * @param harness The harness backing the render under test.
 * @param routes Bodies keyed `"<METHOD> <path>"`.
 * @param options See {@link RouteHarnessOptions}.
 */
export function routeHarness(
  harness: SdkHarness,
  routes: HarnessRoutes,
  options: RouteHarnessOptions = {},
): void {
  const parsed: HarnessRoute[] = parse(routes);
  const hasFallback: boolean = "fallback" in options;

  harness.respond((call: SdkCall): Response | Promise<Response> => {
    const match: HarnessRoute | undefined = parsed.find(
      (route) => route.method === call.method && call.path.endsWith(route.path),
    );

    if (match !== undefined) {
      if (isRouteResponse(match.body)) {
        return match.body.settles
          ? Response.json(match.body.body ?? null, { status: match.body.status })
          : new Promise<Response>(() => {});
      }
      return Response.json(match.body ?? null, { status: OK_STATUS });
    }

    if (hasFallback) {
      return Response.json(options.fallback ?? null, { status: OK_STATUS });
    }

    return Response.json(
      { title: `No harness route for ${call.method} ${call.path}`, status: NOT_FOUND_STATUS },
      { status: NOT_FOUND_STATUS },
    );
  });
}

/**
 * Program an SDK harness with responses selected by HTTP method and pathname suffix.
 * Unmatched requests return 404 unless a fallback response is supplied.
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
 * Build a route response descriptor with the supplied JSON body and HTTP status.
 *
 * Use a non-success status to exercise an operation's error handling. The status
 * is passed to Response.json without validation by this helper.
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
 * Replace a harness's responder with method-and-path routing.
 *
 * The first entry with a matching method and pathname suffix wins; query strings
 * are ignored. Plain values become JSON at status 200. Use failsWith or neverSettles
 * for other outcomes. Unmatched calls return 404 unless options contains fallback.
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

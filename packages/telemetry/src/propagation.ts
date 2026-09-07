const SEE_OTHER = 303;
const MOVED = 301;
const FOUND = 302;
const MAX_REDIRECTS = 20;
const TEMPORARY = 307;
const PERMANENT = 308;
const NO_REDIRECTS = 0;
const ONE_HOP = 1;
const REDIRECT_STATUSES = new Set([MOVED, FOUND, SEE_OTHER, TEMPORARY, PERMANENT]);

/** Apply propagation on every hop, including redirects followed by the host fetch client. */
export async function fetchWithContext(
  request: Request,
  ownedOrigins: ReadonlySet<string>,
  traceparent: string | undefined,
  remaining = MAX_REDIRECTS,
  replayBody?: string | URLSearchParams | Blob,
): Promise<Response> {
  const originalRedirect = request.redirect;
  request.headers.delete("baggage");
  request.headers.delete("traceparent");
  request.headers.delete("tracestate");
  if (traceparent !== undefined && ownedOrigins.has(new URL(request.url).origin)) {
    request.headers.set("traceparent", traceparent);
  }
  const response = await fetch(request, { redirect: "manual" });
  const location = response.headers.get("location");
  if (
    !REDIRECT_STATUSES.has(response.status) ||
    location === null ||
    originalRedirect === "manual"
  ) {
    return response;
  }
  await response.body?.cancel();
  if (originalRedirect === "error" || remaining === NO_REDIRECTS) {
    throw new TypeError("Fetch redirect refused");
  }
  const target = new URL(location, request.url);
  const headers = new Headers(request.headers);
  if (target.origin !== new URL(request.url).origin) {
    headers.delete("authorization");
    headers.delete("cookie");
    headers.delete("proxy-authorization");
  }
  const becomesGet =
    (response.status === SEE_OTHER && request.method !== "HEAD") ||
    ((response.status === MOVED || response.status === FOUND) && request.method === "POST");
  if (becomesGet) {
    headers.delete("content-type");
    headers.delete("content-length");
  }
  if (!becomesGet && request.body !== null && replayBody === undefined) {
    throw new TypeError("Streaming request bodies cannot be replayed after a redirect");
  }
  const init = {
    method: becomesGet ? "GET" : request.method,
    headers,
    body: becomesGet ? null : replayBody,
    signal: request.signal,
    redirect: originalRedirect,
    credentials: request.credentials,
    duplex: "half",
  };
  return fetchWithContext(
    new Request(target, init),
    ownedOrigins,
    traceparent,
    remaining - ONE_HOP,
    becomesGet ? undefined : replayBody,
  );
}

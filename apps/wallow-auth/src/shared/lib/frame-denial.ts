/**
 * Refuse to be framed.
 *
 * The auth host renders the screens a user types a password and answers a
 * consent prompt on. Framed by another origin, those screens are clickjacking
 * targets: an invisible consent form under a decoy button approves a client the
 * user never saw. Both headers say no — `frame-ancestors` is the standard the
 * browsers honour, `X-Frame-Options` the legacy one older engines still read.
 */

/** The `Content-Security-Policy` directive that refuses every framing origin. */
const FRAME_ANCESTORS_NONE = "frame-ancestors 'none'";

/** The legacy header value carrying the same refusal. */
const X_FRAME_OPTIONS_DENY = "DENY";

/** A policy string with `frame-ancestors` already stated, whatever its value. */
const FRAME_ANCESTORS_DIRECTIVE = /(?:^|;)\s*frame-ancestors\b/iu;

/**
 * The response with both frame refusals set. A `Content-Security-Policy` the
 * response already carries (the API's own, on a proxied `/connect/**` page) is
 * kept and appended to rather than replaced — unless it already states a
 * `frame-ancestors`, which is then left as the upstream's decision.
 *
 * Returns a new `Response` when the headers cannot be written (a fetched
 * upstream response is immutable); the body stream is handed over, not copied.
 */
export function withFrameDenial(response: Response): Response {
  let target: Response = response;
  try {
    target.headers.set("X-Frame-Options", X_FRAME_OPTIONS_DENY);
  } catch {
    target = new Response(response.body, {
      status: response.status,
      statusText: response.statusText,
      headers: new Headers(response.headers),
    });
    target.headers.set("X-Frame-Options", X_FRAME_OPTIONS_DENY);
  }

  const existing: string | null = target.headers.get("Content-Security-Policy");
  if (existing === null || existing.trim() === "") {
    target.headers.set("Content-Security-Policy", FRAME_ANCESTORS_NONE);
  } else if (!FRAME_ANCESTORS_DIRECTIVE.test(existing)) {
    target.headers.set(
      "Content-Security-Policy",
      `${existing.replace(/;\s*$/u, "")}; ${FRAME_ANCESTORS_NONE}`,
    );
  }

  return target;
}

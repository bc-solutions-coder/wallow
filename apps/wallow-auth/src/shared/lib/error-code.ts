/**
 * Read a member off an unknown value without asserting its shape.
 *
 * Narrowing is STRUCTURAL rather than `instanceof ApiFailure`, so a screen
 * matches on the wire shape alone and stays indifferent to which bundle built
 * the failure it was handed.
 *
 * The membership test is `in` rather than a truthiness check: these endpoints
 * answer `{ succeeded: false }`, and a real `false` has to stay distinguishable
 * from an absent member.
 *
 * It lives in `shared/` rather than beside its first caller because
 * `wallow/zone-dag` forbids feature-to-feature imports, so four features each
 * ended up carrying a byte-identical copy.
 */
export function readMember(value: unknown, name: string): unknown {
  if (typeof value !== "object" || value === null || !(name in value)) {
    return undefined;
  }

  return (value as Record<string, unknown>)[name];
}

/** The code prefix `@bc-solutions-coder/api-errors` gives a bare `{ error: "<token>" }` body. */
const OAUTH_GRAMMAR_PREFIX = "OAuth.";

/**
 * The API's machine token for a failure, when there is one.
 *
 * These auth endpoints still answer with a bare `{ succeeded, error }` rather
 * than RFC 7807, so `splitServerError` cannot read them and each screen maps the
 * token to its own copy. The SDK parses that body under the OAuth grammar —
 * `code` becomes `OAuth.<Token>` and `title` keeps the raw token — so for those
 * failures the token is read off `title`; a real problem's `code` is returned
 * as-is. The token is matched against, NEVER rendered — an unrecognised or
 * non-string one reads as absent and falls to that screen's generic message
 * rather than leaking Identity's own prose into the UI.
 *
 * The `title` detour goes when the auth endpoints answer problems and these
 * screens move onto a `defineFailureMessages` registry.
 */
export function readErrorCode(cause: unknown): string | undefined {
  const code: unknown = readMember(cause, "code");
  if (typeof code !== "string") {
    return undefined;
  }
  if (!code.startsWith(OAUTH_GRAMMAR_PREFIX)) {
    return code;
  }

  const title: unknown = readMember(cause, "title");
  return typeof title === "string" ? title : undefined;
}

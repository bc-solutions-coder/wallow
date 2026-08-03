/**
 * Read a member off an unknown value without asserting its shape.
 *
 * Narrowing is STRUCTURAL rather than `instanceof WallowError`, because a
 * network-level rejection never reached the server and carries neither `code`
 * nor `status` — and because that class ships from the SDK's `./server` entry,
 * which a screen may not import at all.
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

/**
 * The API's machine token for a failure, when there is one.
 *
 * These auth endpoints answer with a bare `{ succeeded, error }` rather than RFC
 * 7807, so `splitServerError` cannot read them and each screen maps the token to
 * its own copy. The token is matched against, NEVER rendered — an unrecognised
 * or non-string one reads as absent and falls to that screen's generic message
 * rather than leaking Identity's own prose into the UI.
 */
export function readErrorCode(cause: unknown): string | undefined {
  const code: unknown = readMember(cause, "code");

  return typeof code === "string" ? code : undefined;
}

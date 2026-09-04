/**
 * The double-submit CSRF token check shared by the BFF handlers and the proxy.
 *
 * This lives apart from both because each needs it: `handlers.ts` validates the
 * token on logout, `proxy.ts` validates it on every state-changing forward, and
 * `proxy.ts` already reads the session helpers out of `handlers.ts`. Folding
 * these back into either file reinstates an import cycle.
 */
import { timingSafeEqual } from "node:crypto";

/** Header carrying the double-submit CSRF token on state-changing requests. */
export const CSRF_HEADER: string = "x-csrf-token";

/** The HTTP methods that mutate state and therefore carry a CSRF token. */
const STATE_CHANGING_METHODS: ReadonlySet<string> = new Set<string>([
  "POST",
  "PUT",
  "PATCH",
  "DELETE",
]);

/** Whether `method` mutates state and therefore requires a CSRF token. */
export function isStateChangingMethod(method: string): boolean {
  return STATE_CHANGING_METHODS.has(method.toUpperCase());
}

/**
 * Whether the token presented by the browser matches the session-bound token.
 *
 * The comparison must not leak the position of the first differing byte.
 */
export function csrfTokenMatches(
  expected: string | undefined,
  presented: string | undefined,
): boolean {
  // An absent or empty session token is never satisfiable: without this an
  // unauthenticated session would accept an empty `x-csrf-token` header.
  if (expected === undefined || expected === "" || presented === undefined || presented === "") {
    return false;
  }

  const a: Buffer = Buffer.from(expected, "utf8");
  const b: Buffer = Buffer.from(presented, "utf8");

  // timingSafeEqual throws on unequal lengths, so the length check comes first.
  // Token length is not a secret; its bytes are.
  if (a.length !== b.length) {
    return false;
  }

  return timingSafeEqual(a, b);
}

import { isSafeReturnUrl } from "@bc-solutions-coder/sdk";

/**
 * The app's returnUrl guarding, in ONE place (Wallow-j7qk item 6).
 *
 * Every screen that consults the open-redirect guard used to restate two
 * questions inline — "is an empty value an attack?" and "what proves the
 * destination safe?" — and the restatements drifted. `decideReturnUrl` owns
 * both: a call site picks a MODE and acts on the verdict. What a site DOES
 * about "refuse" stays its own: the navigating screens route to `ERROR_HREF`,
 * the link-building screens drop the parameter (see the note in
 * `features/verify-email/sign-in-href.ts` for why routing would be wrong
 * there).
 *
 * Two returnUrl consumers stay OUTSIDE the modes by adjudication, not
 * omission, and both are absolute-URI flows the relative-only rule cannot
 * judge:
 *
 * - `features/accept-terms/accept-terms-handoff.ts` — cargo to a server
 *   endpoint that re-validates against the OIDC allow-list; every value
 *   arriving there is absolute and already allow-listed, so any local mode
 *   would refuse 100% of legitimate traffic.
 * - `features/logout/LogoutScreen` — `post_logout_redirect_uri` is absolute BY
 *   DEFINITION (the relying party's own origin), and the only competent judge
 *   is the server allow-list. A mode would locally accept a RELATIVE value and
 *   skip the probe — widening trust, not consolidating it. Its share of this
 *   module is `isRedirectUriAllowed` below, not a mode.
 */

/**
 * Where a refused `returnUrl` goes.
 *
 * REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`): an
 * unsafe value routes here rather than silently falling back to "/", which would
 * swallow the open-redirect attempt and leave the user on a screen that looks as
 * though nothing was wrong.
 *
 * A raw `href` rather than `to` + `search` (bd memory
 * `tanstack-router-redirect-to-an-unregistered-route-use-href-not-to`), which
 * also keeps every caller off `/error`'s `validateSearch` shape.
 */
export const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/**
 * How a call site reads an EMPTY-BUT-PRESENT `returnUrl` (a bare `?returnUrl=`),
 * and whether an absolute value may be referred to the server. The three modes
 * are the three documented rationales the inline variants carried:
 *
 * - `"refuse-empty"` — the MOUNT-GUARD reading (`useReturnUrlGuard`): the guard
 *   runs on a PRESENT value only, and `""` IS present and unsafe, so it lands on
 *   refuse. An absent value stays "absent" — a direct visit is not an attack.
 * - `"empty-ok"` — the oracle's `string.IsNullOrEmpty` parity: `""` means "no
 *   destination", never an error. For sites whose answer to "no returnUrl" is a
 *   sensible default (a signed-in banner, a plain link) and which must keep a
 *   malformed `?returnUrl=` off the error page.
 * - `"server-allowlist"` — the MfaChallenge reading: like `"refuse-empty"`,
 *   except an absolute value is not condemned locally. The external-login
 *   hand-off arrives with an ABSOLUTE returnUrl no string inspection can tell
 *   from an attack, so it becomes "ask", to be settled by
 *   `auth.validateRedirectUri` — and by nothing weaker.
 */
export type ReturnUrlMode = "refuse-empty" | "empty-ok" | "server-allowlist";

/**
 * The verdicts. "accept" and "ask" carry the (necessarily non-empty) value so a
 * caller acts on a string the guard actually saw, with no re-narrowing:
 *
 * - `"absent"` — no destination; proceed with the screen's default.
 * - `"refuse"` — decided against; never act on the value.
 * - `"accept"` — proven relative-safe (`isSafeReturnUrl`).
 * - `"ask"` — absolute, so only the server allow-list can decide
 *   (`"server-allowlist"` mode only). Fail CLOSED while the answer is pending
 *   or missing.
 */
export type ReturnUrlDecision =
  | { readonly verdict: "absent" }
  | { readonly verdict: "refuse" }
  | { readonly verdict: "accept"; readonly returnUrl: string }
  | { readonly verdict: "ask"; readonly returnUrl: string };

export function decideReturnUrl(
  returnUrl: string | undefined,
  mode: "refuse-empty" | "empty-ok",
): Exclude<ReturnUrlDecision, { verdict: "ask" }>;
export function decideReturnUrl(
  returnUrl: string | undefined,
  mode: ReturnUrlMode,
): ReturnUrlDecision;

/**
 * Decide what a screen may do with its `returnUrl`.
 *
 * EMPTINESS IS DECIDED BEFORE SAFETY, mirroring the oracle's
 * `if (!string.IsNullOrEmpty(ReturnUrl))` wrapping its `IsSafe` call: `""` is
 * not nullish and IS unsafe by `isSafeReturnUrl`, so a rule that consulted the
 * guard first could never distinguish "no destination" from an attack.
 *
 * The safety rule itself is the SDK's `isSafeReturnUrl` — the mirror of the
 * server's `ReturnUrlValidator.IsSafe` — called HERE and nowhere else in the
 * app, so a second copy of the security rule cannot drift.
 */
export function decideReturnUrl(
  returnUrl: string | undefined,
  mode: ReturnUrlMode,
): ReturnUrlDecision {
  if (returnUrl === undefined) {
    return { verdict: "absent" };
  }

  if (returnUrl === "") {
    return mode === "empty-ok" ? { verdict: "absent" } : { verdict: "refuse" };
  }

  if (isSafeReturnUrl(returnUrl)) {
    return { verdict: "accept", returnUrl };
  }

  return mode === "server-allowlist" ? { verdict: "ask", returnUrl } : { verdict: "refuse" };
}

/**
 * The `{ allowed }` narrowing for `auth.validateRedirectUri`, shared by the two
 * screens that consult the server allow-list (MfaChallenge's redirect verdict
 * and LogoutScreen's return link).
 *
 * The endpoint returns an anonymous `Ok(new { allowed = … })` the OpenAPI spec
 * declares with no schema, so the facade types the call `Promise<unknown>` and
 * the C# client's `body?.Allowed == true` collapse does not come for free. The
 * comparison is STRICT, reproducing it: anything that is not literally
 * `allowed: true` — a missing key, the STRING "true", a non-object body — is
 * NOT allowed. JS truthiness would admit `allowed: "false"`, a non-empty string
 * and therefore truthy.
 */
export function isRedirectUriAllowed(body: unknown): boolean {
  if (typeof body !== "object" || body === null || !("allowed" in body)) {
    return false;
  }

  return body.allowed === true;
}

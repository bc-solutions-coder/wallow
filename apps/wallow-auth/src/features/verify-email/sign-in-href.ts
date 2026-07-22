import { getWallowAuthSdk } from "../../lib/wallow-auth-sdk";

/**
 * Build the "go to sign in" link both verify-email screens end on, forwarding
 * `returnUrl` into the login page only when the open-redirect guard accepts it.
 *
 * Shared by `VerifyEmailConfirm` and `VerifyEmailNotice` deliberately: both
 * sibling screens must compute this same sign-in link with the same guard, and a
 * single helper is the only way to keep them honest about it.
 *
 * ── A DELIBERATE DEVIATION FROM THE ORACLE (hardening) ───────────────────────
 *
 * `VerifyEmailConfirm.razor` guards this link with `ReturnUrlValidator.IsSafe`:
 *
 *     private string LoginUrl => ReturnUrlValidator.IsSafe(ReturnUrl)
 *         ? $"/login?returnUrl={Uri.EscapeDataString(ReturnUrl!)}"
 *         : "/login";
 *
 * `VerifyEmail.razor` guards the very same link with `string.IsNullOrEmpty`
 * instead, so it forwards `?returnUrl=https://evil.example` into the login page's
 * query string untouched. Two sibling screens computing one link two ways reads
 * as an oversight in the original rather than a decision, so the port applies
 * `IsSafe` to BOTH. It strictly narrows behaviour and forwards nothing hostile.
 *
 * This is NOT the refuse-vs-sanitize case (bd memory
 * `returnurl-guard-refuse-dont-sanitize`): nothing navigates here. These screens
 * only decline to hand a hostile value to the next screen, so routing to
 * `/error?reason=invalid_redirect_uri` — the right answer when a screen is about
 * to NAVIGATE somewhere unsafe — would be wrong. The user's email really was
 * verified; refusing them the sign-in link over a query parameter they may not
 * have chosen would punish them for the attacker's input.
 *
 * The guard is reached through `getWallowAuthSdk()`, never by re-implementing
 * the rule here: it mirrors the server's `ReturnUrlValidator.IsSafe`, and a
 * second copy of a security rule is a second copy to get wrong.
 *
 * @param returnUrl The `returnUrl` query parameter, if the link carried one.
 * @returns `/login`, or `/login?returnUrl=...` when `returnUrl` is safe.
 */
export function signInHref(returnUrl: string | undefined): string {
  // The `undefined` arm is redundant against the guard (which rejects nullish
  // itself) and is kept only to narrow `returnUrl` to `string` for the template
  // below without a cast — the guard returns a boolean, not a type predicate.
  if (returnUrl === undefined || !getWallowAuthSdk().oidc.isSafeReturnUrl(returnUrl)) {
    return "/login";
  }

  // `Uri.EscapeDataString` parity: the value becomes ONE query parameter. An
  // unencoded `&` would forge extra parameters onto the login page.
  return `/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

import {
  type AllowListedReturnUrl,
  allowListedReturnUrl,
  isSafeReturnUrl,
  validateRedirectUriArgs,
} from "@bc-solutions-coder/sdk";
import { useQuery } from "@bc-solutions-coder/query";
import { useNavigate, useRouteContext } from "@tanstack/react-router";
import { useEffect } from "react";

import { accountValidateRedirectUriOptions } from "../api";
import { ERROR_HREF } from "@shared/lib/return-url";

/**
 * The MfaChallenge screen's open-redirect guard (Wallow-vec7.3.17).
 *
 * Feature-local rather than shared, because it is the only guard in the app that
 * consults the server's allow-list: the external-login hand-off arrives with an
 * ABSOLUTE returnUrl, and no string inspection can tell that apart from an
 * attack. Every other screen's returnUrl is relative and settled by
 * `useReturnUrlGuard` for free.
 */

/**
 * The `{ allowed }` narrowing for `auth.validateRedirectUri`, owned at this
 * boundary exactly as the LogoutScreen port owns its own.
 *
 * The endpoint returns an anonymous `Ok(new { allowed = … })` the OpenAPI spec
 * declares with no schema, so the facade types the call `Promise<unknown>`. The
 * comparison is STRICT, mirroring the C# `body?.Allowed == true`: anything that
 * is not literally `allowed: true` — a missing key, the STRING "true", a
 * non-object body — is NOT allowed. JS truthiness would admit `allowed: "false"`.
 */
function isRedirectUriAllowed(body: unknown): boolean {
  if (typeof body !== "object" || body === null || !("allowed" in body)) {
    return false;
  }

  return body.allowed === true;
}

/**
 * What can be settled about `returnUrl` WITHOUT a network call, and the one case
 * that cannot be ("ask" — an absolute URL, where only the server's allow-list
 * knows).
 */
type LocalDecision = "accept" | "refuse" | "ask";

/** The mount guard's answer. "pending" is its own state: see `verdictOf`. */
type ReturnUrlVerdict = "accept" | "refuse" | "pending";

/**
 * The half of the guard that needs no network.
 *
 * `isRelativeSafe` is `isSafeReturnUrl`'s answer, which proves a value can only
 * resolve against THIS origin. It is passed in already computed rather than as a
 * callback, so the SDK facade's method is never called unbound.
 */
function localDecisionOf(returnUrl: string | undefined, isRelativeSafe: boolean): LocalDecision {
  if (returnUrl === undefined) {
    // The oracle's ordinary direct (non-OIDC) sign-in. No destination to decide;
    // routing it to /error would break every direct login.
    return "accept";
  }

  if (isRelativeSafe) {
    // The password path (`Login.razor`:509 -> `BuildMfaRedirectUrl` threads the
    // relative OIDC returnUrl). The common case, decided for free.
    return "accept";
  }

  if (returnUrl === "") {
    // `IsNullOrEmpty` parity: `?returnUrl=` is a PRESENT value that fails
    // `IsNullOrWhiteSpace`, so it is the unsafe case, not the nullish one. A
    // malformed link is not a destination worth asking the server about.
    return "refuse";
  }

  // Absolute: either the external-login hand-off's allow-listed returnUrl or an
  // attack, and `isSafeReturnUrl` is false for BOTH. Only the allow-list can tell.
  return "ask";
}

/**
 * FAIL CLOSED, in every direction.
 *
 * A rejection (the facade's `unwrap()` throws on non-2xx — the C#
 * `!IsSuccessStatusCode -> false` arm) leaves `allowed` undefined, and an
 * unreachable validator must never become a reason to TRUST a URI. In flight it is
 * undefined too, which is why "pending" is a verdict of its own rather than
 * collapsing into "accept": the caller renders nothing until the answer lands.
 */
function verdictOf(
  local: LocalDecision,
  allowListPending: boolean,
  allowed: boolean | undefined,
): ReturnUrlVerdict {
  if (local !== "ask") {
    return local;
  }

  if (allowListPending) {
    return "pending";
  }

  return allowed === true ? "accept" : "refuse";
}

export interface RedirectGuard {
  readonly verdict: ReturnUrlVerdict;
  /**
   * The value the exchange-ticket mint takes for a returnUrl this guard accepted.
   *
   * A method rather than a flag, so the allow-list verdict travels WITH the
   * returnUrl instead of being re-derived at the hand-off. The builder applies
   * the guard that matches what it is handed, and only this screen knows which
   * one is owed (Wallow-a6jr): a locally-decided returnUrl is relative and
   * `isSafeReturnUrl` is the whole proof, while the "ask" one is absolute and
   * carries the allow-list's answer as an `AllowListedReturnUrl`. The mint
   * refuses anything that is not strictly allowed, so this cannot widen the
   * accept-set the mount guard already settled.
   */
  readonly handOff: (returnUrl: string) => string | AllowListedReturnUrl;
}

/**
 * Decide whether the challenge may act on its `returnUrl`, navigating away if
 * not, and hand back the value the exchange-ticket mint takes.
 *
 * REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`); the
 * oracle instead nulls an unsafe returnUrl and shows a bare success, silently
 * swallowing the attempt. Refused as soon as the verdict lands, following the
 * ConsentScreen port and `Login.razor` L533-540: do not make a user burn a
 * one-time second factor on a destination already decided against.
 *
 * `scopedClientId` scopes the probe to the flow's own client. Unscoped, the
 * endpoint answers against the UNION of every registered client's origins, so a
 * URI any client at all registered would pass for this one.
 */
export function useRedirectVerdict(
  returnUrl: string | undefined,
  scopedClientId: string | undefined,
): RedirectGuard {
  const { sdk } = useRouteContext({ from: "__root__" });
  const navigate = useNavigate();

  const local: LocalDecision = localDecisionOf(
    returnUrl,
    returnUrl !== undefined && isSafeReturnUrl(returnUrl),
  );

  // The `?? ""` is unreachable — `enabled` gates the read on `local === "ask"`,
  // and a nullish returnUrl is decided "accept" — and is present only to narrow
  // the argument to the `string` the factory takes, without a cast.
  const validation = useQuery({
    ...accountValidateRedirectUriOptions({
      client: sdk.client,
      ...validateRedirectUriArgs(returnUrl ?? "", scopedClientId),
    }),
    // The factory hands back the raw body; the verdict is this screen's reading
    // of it.
    select: isRedirectUriAllowed,
    // The ONLY case that costs a request: an absolute returnUrl. The password path
    // and the direct sign-in are already decided, and must not pay for a probe
    // that would sit between the user and their code field.
    enabled: local === "ask",
  });

  const verdict: ReturnUrlVerdict = verdictOf(local, validation.isPending, validation.data);

  useEffect(() => {
    if (verdict === "refuse") {
      void navigate({ href: ERROR_HREF });
    }
  }, [verdict, navigate]);

  return {
    verdict,
    handOff: (accepted: string): string | AllowListedReturnUrl =>
      local === "ask" ? allowListedReturnUrl(accepted, validation.data === true) : accepted,
  };
}

import {
  type AllowListedReturnUrl,
  allowListedReturnUrl,
  validateRedirectUriArgs,
} from "@bc-solutions-coder/sdk";
import { useQuery } from "@bc-solutions-coder/query";
import { useNavigate, useRouteContext } from "@tanstack/react-router";
import { useEffect } from "react";

import { accountValidateRedirectUriOptions } from "../api";
import {
  ERROR_HREF,
  type ReturnUrlDecision,
  decideReturnUrl,
  isRedirectUriAllowed,
} from "@shared/lib/return-url";

/**
 * The MfaChallenge screen's open-redirect guard (Wallow-vec7.3.17).
 *
 * The app's ONE `"server-allowlist"`-mode caller of `decideReturnUrl`, and
 * feature-local because of it: the external-login hand-off arrives with an
 * ABSOLUTE returnUrl, and no string inspection can tell that apart from an
 * attack — only the server's allow-list can, which is what the mode's "ask"
 * verdict defers to. Every other screen's returnUrl is relative and settled
 * locally by its mode.
 */

/** The mount guard's answer. "pending" is its own state: see `verdictOf`. */
type ReturnUrlVerdict = "accept" | "refuse" | "pending";

/**
 * FAIL CLOSED, in every direction.
 *
 * A rejection (the facade's `unwrap()` throws on non-2xx — the C#
 * `!IsSuccessStatusCode -> false` arm) leaves `allowed` undefined, and an
 * unreachable validator must never become a reason to TRUST a URI. In flight it is
 * undefined too, which is why "pending" is a verdict of its own rather than
 * collapsing into "accept": the caller renders nothing until the answer lands.
 *
 * "absent" (the ordinary direct, non-OIDC sign-in — routing it to /error would
 * break every direct login) and "accept" (the password path's relative OIDC
 * returnUrl, decided for free) are both settled locally.
 */
function verdictOf(
  decision: ReturnUrlDecision["verdict"],
  allowListPending: boolean,
  allowed: boolean | undefined,
): ReturnUrlVerdict {
  if (decision === "refuse") {
    return "refuse";
  }

  if (decision !== "ask") {
    return "accept";
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

  const decision = decideReturnUrl(returnUrl, "server-allowlist");

  // The `?? ""` is unreachable — `enabled` gates the read on the "ask" verdict,
  // and a nullish returnUrl is decided "absent" — and is present only to narrow
  // the argument to the `string` the factory takes, without a cast.
  const validation = useQuery({
    ...accountValidateRedirectUriOptions({
      client: sdk.client,
      ...validateRedirectUriArgs(returnUrl ?? "", scopedClientId),
    }),
    // The factory hands back the raw body; the verdict is the shared narrowing's
    // reading of it.
    select: isRedirectUriAllowed,
    // The ONLY case that costs a request: an absolute returnUrl. The password path
    // and the direct sign-in are already decided, and must not pay for a probe
    // that would sit between the user and their code field.
    enabled: decision.verdict === "ask",
  });

  const verdict: ReturnUrlVerdict = verdictOf(
    decision.verdict,
    validation.isPending,
    validation.data,
  );

  useEffect(() => {
    if (verdict === "refuse") {
      void navigate({ href: ERROR_HREF });
    }
  }, [verdict, navigate]);

  return {
    verdict,
    handOff: (accepted: string): string | AllowListedReturnUrl =>
      decision.verdict === "ask"
        ? allowListedReturnUrl(accepted, validation.data === true)
        : accepted,
  };
}

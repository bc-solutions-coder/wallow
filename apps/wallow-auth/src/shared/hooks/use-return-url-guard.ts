import { isSafeReturnUrl } from "@bc-solutions-coder/sdk";
import { useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";

import { ERROR_HREF } from "@shared/lib/return-url";

export type ReturnUrlVerdict = "accept" | "refuse";

/**
 * Decide whether a screen may act on its `returnUrl`, and navigate away if not.
 *
 * An ABSENT returnUrl is not an attack — it is the ordinary direct path — so the
 * guard runs on a PRESENT value only. `""` IS present, and `isSafeReturnUrl("")`
 * is false, so it lands on refuse rather than on the nullish case. That is the
 * mount-time reading, and it is deliberately NOT the one the two pure helpers
 * take: they mirror the oracle's `IsNullOrEmpty`, where `""` means "no
 * destination" and a bare `?returnUrl=` must not reach the error page.
 *
 * A caller renders nothing on "refuse": the navigation is in flight, and a form
 * shown meanwhile invites the user to spend a one-time factor on a destination
 * already decided against.
 */
export function useReturnUrlGuard(returnUrl: string | undefined): ReturnUrlVerdict {
  const navigate = useNavigate();
  const verdict: ReturnUrlVerdict =
    returnUrl !== undefined && !isSafeReturnUrl(returnUrl) ? "refuse" : "accept";

  useEffect(() => {
    if (verdict === "refuse") {
      void navigate({ href: ERROR_HREF });
    }
  }, [verdict, navigate]);

  return verdict;
}

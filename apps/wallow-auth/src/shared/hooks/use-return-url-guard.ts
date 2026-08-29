import { useNavigate } from "@tanstack/react-router";
import { useEffect } from "react";

import { ERROR_HREF, decideReturnUrl } from "@shared/lib/return-url";

export type ReturnUrlVerdict = "accept" | "refuse";

/**
 * Decide whether a screen may act on its `returnUrl`, and navigate away if not.
 *
 * The `"refuse-empty"` mode is the mount-time reading: an absent returnUrl is
 * the ordinary direct path, but a bare `?returnUrl=` is a PRESENT, unsafe value
 * and refuses. See `@shared/lib/return-url` for the mode table.
 *
 * A caller renders nothing on "refuse": the navigation is in flight, and a form
 * shown meanwhile invites the user to spend a one-time factor on a destination
 * already decided against.
 */
export function useReturnUrlGuard(returnUrl: string | undefined): ReturnUrlVerdict {
  const navigate = useNavigate();
  const verdict: ReturnUrlVerdict =
    decideReturnUrl(returnUrl, "refuse-empty").verdict === "refuse" ? "refuse" : "accept";

  useEffect(() => {
    if (verdict === "refuse") {
      void navigate({ href: ERROR_HREF });
    }
  }, [verdict, navigate]);

  return verdict;
}

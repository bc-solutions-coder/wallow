import { useLoaderData } from "@tanstack/react-router";

import {
  resolveTransactionBranding,
  type TransactionBranding,
} from "@shared/lib/authorize-context";

/**
 * The requesting client's branding for the current authorize transaction, or
 * `undefined` when the screen should wear the fork's own chrome.
 *
 * The context itself is resolved ONCE, by the root route's loader (see
 * `routes/__root.tsx`); this hook only reads that answer back and maps it for
 * `AuthLayout`. Every in-transaction screen calls it; the email-link screens
 * (error, reset-password, verify-email/confirm, invitation, setup) do not —
 * their fork branding is `AuthLayout`'s own default, not a decision this hook
 * needs to see.
 *
 * `undefined` rather than `null` on the fork arm so the result spreads straight
 * into `AuthLayout`'s optional `branding`/`organizationName` props.
 *
 * The loader data is read DEFENSIVELY: under a root that never ran this app's
 * loader — a spec's throwaway root, or a render inside the root error
 * boundary — `useLoaderData` answers `undefined`, and the right chrome for
 * both is the fork's own, not a crash.
 */
export function useTransactionBranding(): TransactionBranding | undefined {
  const loaderData = useLoaderData({ from: "__root__" }) as
    | { readonly authorizeContext?: Parameters<typeof resolveTransactionBranding>[0] }
    | undefined;

  return resolveTransactionBranding(loaderData?.authorizeContext) ?? undefined;
}

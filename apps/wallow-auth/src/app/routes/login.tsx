import { scalarToString } from "@bc-solutions-coder/utils/guards";
import { createFileRoute, redirect, useRouteContext } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import { useTransactionBranding } from "@shared/hooks/use-transaction-branding";
import { isPasswordResetMessage, LoginScreen, PASSWORD_RESET_MESSAGE } from "@features/login";
import { ensureSetupRequired } from "@features/setup";

/**
 * The `/login` route (Wallow-vec7.3.11 / 2.8a).
 *
 * The path was pre-registered against a placeholder by Wallow-vec7.3.16 and is
 * the contract: `src/router.tsx` already binds it, so this task replaced the
 * placeholder component here and left the router untouched.
 *
 * This route owns the query string — the oracle's `[SupplyParameterFromQuery]`
 * properties — and hands them down as props, keeping `LoginScreen` a pure
 * function of its inputs and testable without a router. This is the seam
 * `/reset-password` established and `/consent` followed.
 *
 * `.3.12` (magic-link) adds `magicLinkToken` HERE, alongside these three, and
 * passes it down the same way; it is not read yet.
 *
 * `AuthLayout` supplies the branded chrome every auth page renders inside, and
 * wears the requesting client's branding when the login sits inside an authorize
 * transaction — see {@link LoginRoute}. Every transaction screen wires the same
 * overlay (issue #142).
 */
interface LoginSearch {
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
  /**
   * The `client_id` query parameter. The wire name is snake_case, per the oracle's
   * `[SupplyParameterFromQuery(Name = "client_id")]` — it is OpenIddict's parameter
   * name and is not this screen's to rename, even though the prop it feeds is
   * `clientId`.
   */
  readonly client_id?: string;
  /** The oracle's `Error` — a failure handed back by a redirect, e.g. from external login. */
  readonly error?: string;
  /**
   * The oracle's `magicLinkToken` (Wallow-vec7.3.12) — present only on the link
   * `MagicLinkRequestedNotificationHandler.cs:21` emails: `{authUrl}/login?
   * magicLinkToken=…&returnUrl=…&client_id=…`. The magic-link panel redeems it on
   * load.
   */
  readonly magicLinkToken?: string;
  /**
   * The oracle's dead `message` param (Wallow-xzha.1.2). `ResetPasswordForm`
   * navigates to `/login?message=password_reset` after a completed reset. It is
   * compared against a literal token, not used as a URI, so `validateSearch`
   * threads ONLY the recognised `password_reset` value through and drops anything
   * else — `?message=` is attacker-constructable, so no arbitrary value becomes a
   * prop.
   */
  readonly message?: string;
}

/**
 * Every param is OPTIONAL and an unsafe `returnUrl` is NOT rejected here,
 * deliberately: `/` redirects to a bare `/login`, and refusing at the
 * search-validation layer would throw before the screen mounts, whereas the
 * open-redirect refusal is specified to land the user on
 * `/error?reason=invalid_redirect_uri` (bd memory
 * `returnurl-guard-refuse-dont-sanitize`). Handing the raw value to the component,
 * which guards it at the point of navigation, is what makes that possible.
 *
 * `returnUrl`, `client_id` and `magicLinkToken` are NOT re-stringified the way
 * `error` is: they are used as a URI, an identifier and a credential, not matched
 * against literals, so a value the parser turned into a number was never a usable
 * one. A real magic-link token cannot BE such a value in any case — it is
 * `base64(32 bytes) + "." + signature` (PasswordlessService.cs:70-72), so it always
 * carries base64 padding and never JSON-parses to a scalar.
 */
function validateSearch(search: Record<string, unknown>): LoginSearch {
  return {
    returnUrl: typeof search.returnUrl === "string" ? search.returnUrl : undefined,
    client_id: typeof search.client_id === "string" ? search.client_id : undefined,
    error: scalarToString(search.error),
    magicLinkToken: typeof search.magicLinkToken === "string" ? search.magicLinkToken : undefined,
    // Like `error`, `message` is matched against a literal token, so a scalar the
    // parser turned into a boolean/number is re-stringified before the known-token
    // check — and only the recognised value survives, never a raw attacker string.
    message: isPasswordResetMessage(scalarToString(search.message))
      ? PASSWORD_RESET_MESSAGE
      : undefined,
  };
}

/*
 * The per-client branding overlay: a login inside an authorize transaction
 * headlines the requesting client's name, tagline and logo instead of the
 * fork's, while the layout's footer keeps attributing the fork. The context is
 * resolved ONCE by the root route's loader (issue #142) — keyed by the
 * transaction's `returnUrl`, never by the bare `client_id`, whose anonymous
 * branding endpoint is gone — and `useTransactionBranding()` only reads that
 * answer back. Branding is CHROME, so every path that is not a resolved
 * third-party client (no transaction, first-party client, failed fetch)
 * collapses to `undefined` — `AuthLayout`'s fork default — and never gates the
 * sign-in form.
 */
function LoginRoute() {
  const { returnUrl, client_id: clientId, error, magicLinkToken, message } = Route.useSearch();
  const transaction = useTransactionBranding();
  const { webAppUrl } = useRouteContext({ from: "__root__" });

  return (
    <AuthLayout branding={transaction?.branding} organizationName={transaction?.organizationName}>
      <LoginScreen
        returnUrl={returnUrl}
        clientId={clientId}
        error={error}
        magicLinkToken={magicLinkToken}
        message={message}
        homeUrl={webAppUrl}
      />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/login")({
  validateSearch,
  /*
   * The first-run gate: while a fresh deployment still has no administrator,
   * every way in funnels through this page (`/` redirects here, and the OIDC
   * flow lands here), so this is the one place that needs to know setup is
   * still open and forward the visitor to `/setup`.
   *
   * Only a definite "setup is required" redirects. Complete, unknown, and
   * unreachable all render the login page — a status hiccup must never take
   * sign-in down. The search params are deliberately dropped on the redirect:
   * `/setup` is deployment bootstrap, not a step in the flow that carried them,
   * and post-setup the visitor re-enters sign-in from its own link.
   */
  beforeLoad: async ({ context }) => {
    const required: boolean | null = await ensureSetupRequired({
      queryClient: context.queryClient,
      client: context.sdk.client,
    });

    if (required === true) {
      throw redirect({ to: "/setup" });
    }
  },
  component: LoginRoute,
});

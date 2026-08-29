import {
  forkBranding,
  mergeClientBranding,
  type ResolvedBranding,
} from "@bc-solutions-coder/styles";
import { useQuery } from "@bc-solutions-coder/query";
import { scalarToString } from "@bc-solutions-coder/utils/guards";
import { createFileRoute, redirect, useRouteContext } from "@tanstack/react-router";

import { AuthLayout } from "@shared/components/auth-layout";
import {
  clientBrandingGetBrandingOptions,
  isPasswordResetMessage,
  LoginScreen,
  PASSWORD_RESET_MESSAGE,
} from "@features/login";
import { ensureSetupRequired } from "@features/setup";
import { BASE_PATH } from "@shared/lib/base-path";

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
 * `AuthLayout` supplies the branded chrome every auth page renders inside, and this
 * route is where the per-client (`client_id`) branding overlay is wired
 * (Wallow-ffpq.2.5) — see {@link LoginRoute}. The other screens still render the
 * fork's own branding; only `/login` has an acceptance criterion for the overlay.
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

/**
 * The per-client branding overlay: `/login?client_id=acme` headlines Acme's name,
 * tagline and logo instead of the fork's, while the layout's footer keeps
 * attributing the fork.
 *
 * Branding is CHROME, so every path that is not a resolved client falls back to
 * the fork by returning `undefined` — `AuthLayout`'s own default, which is
 * `mergeClientBranding(fork, null)`. That covers all three: no `client_id` (the
 * query never runs), the fetch still in flight, and a client with no branding row,
 * which the API answers with a bare 404 and the SDK surfaces as a rejection. None
 * of them may gate the sign-in form or show the person an error they cannot act on
 * — hence no read of `isError` here, and no retry (`createQueryClient()` sets
 * `retry: false` for every query in the app, so this needs no local override).
 *
 * Per-client THEME COLOURS are deliberately out of scope: the CSS variables are
 * emitted by `__root.tsx` from the module constant `forkResolvedBranding`, and the
 * root route has no loader to learn `client_id` in. Only the identity fields
 * (name/tagline/logo) are overlaid.
 */
function useClientBranding(
  // An absent `client_id` defaults to the empty string so the `enabled` gate and
  // the factory agree WITHOUT a cast — the seam `RegisterForm`'s `clientTenant`
  // read established, where "" is the one value the gate refuses.
  clientId: string = "",
): ResolvedBranding | undefined {
  const { sdk } = useRouteContext({ from: "__root__" });

  const { data } = useQuery({
    ...clientBrandingGetBrandingOptions({ client: sdk.client, path: { clientId } }),
    enabled: clientId !== "",
  });

  // The base path resolves the FORK's icon, which the client branch never
  // renders (a client shows its own hosted logo or none), so it changes nothing
  // here today. Passed anyway so there is one based call shape in the app and no
  // second, unprefixed one to drift back to.
  return data === undefined ? undefined : mergeClientBranding(forkBranding, data, BASE_PATH);
}

function LoginRoute() {
  const { returnUrl, client_id: clientId, error, magicLinkToken, message } = Route.useSearch();
  const branding: ResolvedBranding | undefined = useClientBranding(clientId);

  return (
    <AuthLayout branding={branding}>
      <LoginScreen
        returnUrl={returnUrl}
        clientId={clientId}
        error={error}
        magicLinkToken={magicLinkToken}
        message={message}
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

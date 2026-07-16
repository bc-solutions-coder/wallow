import { createFileRoute } from "@tanstack/react-router";

import { AuthLayout } from "../components/auth-layout";
import { LoginScreen } from "../features/login/components/LoginScreen";

/**
 * The `/login` route (Wallow-vec7.3.11 / 2.8a) — the React port of the Blazor
 * oracle `api/src/Wallow.Auth/Components/Pages/Login.razor`.
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
 * `AuthLayout` supplies the branded chrome every auth page renders inside. It is
 * given no `branding` prop, so it falls back to the fork's own — the per-client
 * (`client_id`) branding overlay is not wired on any screen in this app yet, and
 * no acceptance criterion asks for it, even though this route carries a `client_id`.
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
}

/**
 * TanStack's default search parser JSON-parses EVERY query value before
 * `validateSearch` sees it, so `?error=true` arrives as the BOOLEAN `true` and
 * `?error=1` as a NUMBER (bd memory
 * `tanstack-router-default-search-parser-json-parses-values`). `error` is compared
 * against literal tokens, so a scalar is re-stringified rather than dropped: the
 * common `typeof x === "string" ? x : undefined` idiom would silently swallow an
 * error hand-back and show the user a clean login form after a failure.
 *
 * Non-scalars (`?error=[1,2]`) have no literal they could match and read as absent
 * — a junk link must still render a usable form, not a validation error.
 */
function readScalar(value: unknown): string | undefined {
  if (typeof value === "string") {
    return value;
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  return undefined;
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
    error: readScalar(search.error),
    magicLinkToken: typeof search.magicLinkToken === "string" ? search.magicLinkToken : undefined,
  };
}

function LoginRoute() {
  const { returnUrl, client_id: clientId, error, magicLinkToken } = Route.useSearch();

  return (
    <AuthLayout>
      <LoginScreen
        returnUrl={returnUrl}
        clientId={clientId}
        error={error}
        magicLinkToken={magicLinkToken}
      />
    </AuthLayout>
  );
}

export const Route = createFileRoute("/login")({
  validateSearch,
  component: LoginRoute,
});

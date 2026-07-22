import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";

/**
 * The Login screen's external-provider list (Wallow-vec7.3.14 / 2.8d).
 *
 * NOT a login panel. It implements no `LoginPanelProps`, owns no mutation and
 * reports nothing up: there is no auth RESULT to report, because the browser
 * leaves this origin entirely when the user clicks. `.3.11` placed it as a
 * SECTION the shell renders next to `TabPanel`, gated on `signedIn`, which is
 * also where the oracle puts it — outside the tab `else if` chain (so it offers
 * itself alongside all three tabs) but inside the `else` of `if (_signedIn)`.
 *
 * Testids are INVENTED per the scout's mandate: the oracle tags nothing here.
 *
 * ── THE ORIGIN DIVERGENCE (inherited from Wallow-vec7.3.4/.3.6/.3.11) ─────────
 *
 * The oracle's `{ApiBaseUrl}` prepend is NOT ported, and this is the seam where
 * it matters most in the chain. This link starts the OIDC challenge; the
 * handshake it kicks off rides SameSite cookies, and a cross-origin top-level GET
 * drops them. This app's h3 server mounts `/v1/**` at the ROOT
 * (`src/lib/auth-server.ts`), so the same-origin path IS the endpoint. The
 * `ApiBaseUrl` knob this app deliberately lacks would also be unresolvable from a
 * browser: `WALLOW_API_INTERNAL_URL` is a SERVER-side address.
 *
 * ── NO `isSafeReturnUrl` GUARD (deferred; adjudicated on Wallow-vec7.3.10) ────
 *
 * `returnUrl` is inert query CARGO on a same-origin CONSTANT path, and the API
 * re-validates it fail-closed before it does anything else —
 * `AccountController.ExternalLogin` (:257) bails to
 * `{authUrl}/error?reason=invalid_redirect_uri` unless
 * `redirectUriValidator.IsAllowedAsync(returnUrl)` passes, and
 * `ExternalLoginCallback` (:274) checks it a second time. The open-redirect
 * decision is the API's, made against the OpenIddict allow-list.
 *
 * Guarding here would not harden anything — it would break everything.
 * `IsAllowedAsync` (OpenIddictRedirectUriValidator.cs:24) accepts ONLY ABSOLUTE
 * URLs; `isSafeReturnUrl` (packages/sdk/src/auth-oidc.ts:55) accepts ONLY
 * relative single-`/` paths. The accept-sets are provably disjoint, so the guard
 * would refuse every value the server can honour. `.3.6` shipped that exact
 * mistake on the MFA hand-off and dead-ended every external-login user at
 * `/error` until `.3.17` undid it. The rule (bd memory `guard-where-the-client-
 * picks-the-destination-defer`): guard where the CLIENT picks the destination,
 * defer where the SERVER does.
 *
 * What a deferred guard DOES owe is that the cargo cannot break OUT of the query
 * string — `encodeURIComponent` below. ASP.NET binds a duplicated `[FromQuery]`
 * value as `"a,b"`, so unencoded cargo carrying `&provider=` could change which
 * identity provider the user is challenged against.
 */

/** The oracle's `GetExternalLoginUrl` target, sans the `ApiBaseUrl` prepend. */
const EXTERNAL_LOGIN_PATH = "/v1/identity/auth/external-login";

/**
 * Narrow the facade's `Promise<unknown>` at this boundary — no cast, structural
 * check, fail closed (bd memory `untyped-sdk-response-fail-closed-pattern-wallow-
 * auth`). `GetExternalProviders` (AccountController.cs:56-63) returns
 * `Ok(List<string>)` of `s.DisplayName ?? s.Name`.
 *
 * A partly-valid list is rejected WHOLE rather than filtered down to its good
 * entries: a body this shape means the endpoint is not what we believe it to be,
 * and half-trusting it would put a challenge link built from a non-name on screen.
 */
function providerNamesOf(body: unknown): readonly string[] {
  if (!Array.isArray(body)) {
    return [];
  }

  const names: string[] = body.filter(
    (entry: unknown): entry is string => typeof entry === "string" && entry !== "",
  );

  return names.length === body.length ? names : [];
}

/**
 * The oracle escapes only the returnUrl (L315-316). The provider is escaped too:
 * it is API-supplied prose (`DisplayName`), not a URL token, and it lands in the
 * same query string as the cargo.
 */
function externalLoginHref(provider: string, returnUrl: string): string {
  return (
    `${EXTERNAL_LOGIN_PATH}?provider=${encodeURIComponent(provider)}` +
    `&returnUrl=${encodeURIComponent(returnUrl)}`
  );
}

/** Display names are prose ("Microsoft Entra ID"), so the testid cannot be the raw name. */
function providerTestId(provider: string): string {
  const slug: string = provider
    .toLowerCase()
    .replaceAll(/[^a-z0-9]+/gu, "-")
    .replaceAll(/^-+|-+$/gu, "");

  return `login-external-${slug}`;
}

/** The oracle's `BbSeparator` + "Or continue with" caption. */
function SeparatorCaption() {
  return (
    <div className="relative flex items-center justify-center">
      <span className="absolute inset-x-0 top-1/2 border-t border-border" />
      <span className="relative bg-card px-2 text-xs uppercase text-muted-foreground">
        Or continue with
      </span>
    </div>
  );
}

/** One provider's challenge link — the oracle's outline `BbButton Href=…`. */
function ProviderLink({ provider, href }: { readonly provider: string; readonly href: string }) {
  return (
    <a
      className="inline-flex items-center justify-center rounded-md border border-border px-4 py-2 text-sm font-medium text-foreground hover:bg-accent"
      href={href}
      data-testid={providerTestId(provider)}
    >
      {provider}
    </a>
  );
}

/** The oracle's `grid grid-cols-2` of provider buttons. */
function ProviderGrid({
  providers,
  returnUrl,
}: {
  readonly providers: readonly string[];
  readonly returnUrl: string;
}) {
  return (
    <div className="grid grid-cols-2 gap-2">
      {providers.map((provider: string) => (
        <ProviderLink
          key={provider}
          provider={provider}
          href={externalLoginHref(provider, returnUrl)}
        />
      ))}
    </div>
  );
}

export interface ExternalProvidersProps {
  /**
   * The OIDC `returnUrl` to resume after the provider hands the user back. Pure
   * CARGO — see the no-guard note above. May be RELATIVE (a direct sign-in, from
   * `/connect/authorize`) or ABSOLUTE (an allow-listed external-login value); both
   * are passed through untouched and adjudicated by the API.
   */
  readonly returnUrl?: string;
}

export function ExternalProviders({ returnUrl }: ExternalProvidersProps): ReactNode {
  const query = useQuery({
    queryKey: ["external-providers"],
    queryFn: async (): Promise<unknown> => await getWallowAuthSdk().auth.getExternalProviders(),
    // A provider list that failed to load will not load on a second try fast
    // enough to matter to someone staring at a sign-in form.
    retry: false,
  });

  const providers: readonly string[] = providerNamesOf(query.data);

  // The oracle's `@if (_externalProviders.Count > 0)`. This also absorbs the
  // pending and REJECTED states: the oracle awaits this in `OnInitializedAsync`
  // with no try/catch, so a failure takes the whole page down with it. That is not
  // ported — password sign-in works perfectly well without the social buttons, so
  // a failure degrades to the same rendering as "none configured" rather than
  // destroying the screen around it.
  if (providers.length === 0) {
    return null;
  }

  // The oracle's `ReturnUrl ?? currentUrl` (`Navigation.Uri`, L314): a user who
  // came to /login directly, outside any OIDC flow, must land back where they
  // started rather than at a dead end.
  //
  // DIVERGENCE: `""` takes the fallback too. The oracle's `??` passes `""` THROUGH
  // (it is not null) and the server then bounces the user to /error via its
  // `IsNullOrEmpty` arm — a dead link, rendered on purpose. An empty returnUrl and
  // an absent one are the same user with the same intent, so they get the same
  // link. Disclosed on the bead.
  const target: string =
    returnUrl === undefined || returnUrl === "" ? globalThis.location.href : returnUrl;

  return (
    <div className="space-y-3" data-testid="login-external-providers">
      <SeparatorCaption />
      <ProviderGrid providers={providers} returnUrl={target} />
    </div>
  );
}

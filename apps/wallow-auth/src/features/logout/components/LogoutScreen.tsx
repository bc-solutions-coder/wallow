import { useQuery } from "@tanstack/react-query";
import { Card } from "@bc-solutions-coder/ui";
import type { ReactNode } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";

/**
 * The Logout screen (Wallow-vec7.3.5), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Logout.razor`.
 *
 * ONE ROUTE, TWO PHASES. The oracle drives both off `signed_out` on a single
 * `@page "/logout"`: the CONFIRM step asks "are you sure" and hands off to
 * `/connect/logout`; the SIGNED-OUT LANDING is where OpenIddict sends the browser
 * back once the end-session request completes, and it offers a way back to the
 * relying party. Both phases share `logout-confirm-heading` — same testid,
 * different text — which is the oracle's choice and is preserved verbatim rather
 * than "fixed" into two testids.
 *
 * `postLogoutRedirectUri` and `signedOut` arrive as props rather than being read
 * from the router here: the route owns the query string (the oracle's two
 * `[SupplyParameterFromQuery]` properties) and hands them down, keeping this
 * component a pure function of its inputs. This is the seam `ResetPasswordForm`
 * established and `ConsentScreen` followed.
 *
 * The API is reached through `getWallowAuthSdk()`, never `@bc-solutions-coder/sdk`
 * directly — that facade is this app's only permitted importer of the SDK.
 *
 * ── THE ORIGIN TRAP (the load-bearing port decision on this screen) ───────────
 *
 * The oracle builds its logout URL against an absolute API origin:
 *
 *     private string ApiBaseUrl => Configuration["ApiBaseUrl"]
 *         ?? throw new InvalidOperationException("ApiBaseUrl must be configured");
 *     string url = $"{ApiBaseUrl}/connect/logout";
 *
 * That prepend is deliberately NOT ported, for the reasons established on
 * `/consent` (Wallow-vec7.3.4). apps/wallow-auth's h3 server
 * (`src/lib/auth-server.ts`) is a PASSTHROUGH REVERSE PROXY mounting `/connect/**`
 * and `/v1/**` at the ROOT — the same fact behind the facade's `baseUrl: '/'` (bd
 * memory `wallow-auth-same-origin-baseurl-apps-wallow-auth`). This origin DOES
 * host `/connect/logout`, so the origin argument is `""`.
 *
 * It is worse than cosmetic HERE specifically, because `/connect/logout` is a
 * COOKIE-READING endpoint: it must see the auth cookie to know whose session to
 * end. Sending the browser cross-origin drops that `SameSite` cookie, and the
 * end-session request then no-ops or bounces the user through a needless
 * re-prompt — a sign-out button that does not sign you out. It would also
 * reintroduce an `ApiBaseUrl` knob this app deliberately lacks: its only API URL,
 * `WALLOW_API_INTERNAL_URL`, is a SERVER-side internal address
 * (`http://wallow-api` under Aspire) the browser cannot resolve at all.
 *
 * `buildConnectLogoutUrl` (Wallow-vec7.2.2) owns the rest of the `LogoutUrl`
 * getter — the `IsNullOrEmpty` omission of the parameter and the
 * `Uri.EscapeDataString` encoding — under tests of its own.
 *
 * ── WHY NO isSafeReturnUrl GUARD ON THIS SCREEN ──────────────────────────────
 *
 * Every other screen in this phase guards its returnUrl with `isSafeReturnUrl`.
 * This one must NOT, and the difference is not an oversight to correct:
 * `post_logout_redirect_uri` is an ABSOLUTE URI by definition — the relying
 * party's own origin, which is not this one — so the relative-path guard (a
 * single leading `/`) would reject every legitimate caller. That is why
 * `buildConnectLogoutUrl` documents itself as deliberately unguarded.
 *
 * The open-redirect defence here is the SERVER's instead, and it is stronger:
 * `auth.validateRedirectUri`, an allow-list check against the client's REGISTERED
 * post-logout URIs. `signed_out` and `post_logout_redirect_uri` are both
 * attacker-suppliable and this landing renders for anyone who types the URL, with
 * no proof a sign-out ever happened — that call is the only thing standing
 * between a crafted link and a Wallow-branded button pointing at an arbitrary
 * origin.
 */

/**
 * The origin the logout URL is built against: this one. See the origin trap above
 * — named rather than inlined so the `""` reads as a decision rather than a
 * forgotten argument.
 */
const SAME_ORIGIN = "";

/**
 * The oracle's `SignedOut == "true"` — an ordinal string equality, not a boolean
 * parse. The exactness matters in the SAFE direction: anything else falls to the
 * confirm step, so a mangled link asks again rather than telling a still-signed-in
 * user they are signed out.
 */
const SIGNED_OUT = "true";

/**
 * The `{ allowed }` narrowing, owned at THIS boundary.
 *
 * The facade types the call `Promise<unknown>` (auth-client.ts:149), because the
 * OpenAPI spec declares the 200 with no schema — the endpoint returns an anonymous
 * `Ok(new { allowed = … })` (AccountController.cs:601-607), so there is nothing to
 * generate a type from. The C# client's `body?.Allowed == true` collapse
 * (AuthApiClient.cs:93-108) therefore does not come for free.
 *
 * That comparison is STRICT and this reproduces it: anything that is not literally
 * `allowed: true` — a missing key, the STRING `"true"`, a non-object body — is NOT
 * allowed. Leaning on JS truthiness instead would link on `allowed: "false"`,
 * which is a non-empty string and therefore truthy.
 */
function isRedirectUriAllowed(body: unknown): boolean {
  if (typeof body !== "object" || body === null || !("allowed" in body)) {
    return false;
  }

  return body.allowed === true;
}

/**
 * The oracle's `BbCardTitle`. One testid across both phases, per the oracle; the
 * TEXT is what tells the two phases apart.
 */
function CardHeading({ signedOut }: { readonly signedOut: boolean }) {
  return (
    <h1 className="text-lg font-semibold text-card-foreground">
      <span data-testid="logout-confirm-heading">{signedOut ? "Signed out" : "Sign out"}</span>
    </h1>
  );
}

/**
 * The oracle's `else` arm: the prompt and the handoff.
 *
 * The control is an ANCHOR, as in the oracle (`<a href="@LogoutUrl">`). It has to
 * be a real navigation — `/connect/logout` is served by the h3 proxy and is not in
 * the client-side route tree, so a router-driven control would 404 in-app. Keeping
 * the sign-out behind a CLICK rather than firing it on mount also keeps it off the
 * CSRF sink `<img src="/logout">` would otherwise be.
 */
function ConfirmStep({ postLogoutRedirectUri }: { readonly postLogoutRedirectUri?: string }) {
  const logoutUrl: string = getWallowAuthSdk().oidc.buildConnectLogoutUrl(
    SAME_ORIGIN,
    postLogoutRedirectUri,
  );

  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">Are you sure you want to sign out?</p>
      <a
        href={logoutUrl}
        data-testid="logout-confirm-button"
        className="inline-flex w-full items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
      >
        Sign out
      </a>
    </div>
  );
}

/**
 * The oracle's `@if (_isRedirectUriValid)` block. The href is the RAW post-logout
 * URI, not the `/connect/logout` URL — this is the return trip.
 *
 * It is only ever handed a URI the SERVER allowed: `_isRedirectUriValid` starts
 * FALSE and only the allow-list can flip it.
 */
function ReturnLink({ uri }: { readonly uri: string }) {
  return (
    <a
      href={uri}
      data-testid="logout-return-link"
      className="mt-4 inline-flex w-full items-center justify-center rounded-md border border-input bg-background px-4 py-2 text-sm font-medium hover:bg-accent hover:text-accent-foreground"
    >
      Return to application
    </a>
  );
}

/**
 * The oracle's `SignedOut == "true"` arm. The heading and copy render
 * unconditionally and only the LINK sits behind the validation: the user is told
 * the sign-out worked without waiting on a check that has nothing to do with it.
 */
function SignedOutLanding({ returnUri }: { readonly returnUri?: string }) {
  return (
    <div className="space-y-4">
      <p className="text-sm text-muted-foreground">You have been successfully signed out.</p>
      {returnUri === undefined ? null : <ReturnLink uri={returnUri} />}
    </div>
  );
}

/** The oracle's `BbCardFooter` — outside the `@if`, so it renders on both arms. */
function LogoutFooter() {
  return (
    <div className="w-full text-center">
      <a
        href="/login"
        data-testid="logout-back-link"
        className="text-sm text-muted-foreground hover:text-foreground"
      >
        Back to sign in
      </a>
    </div>
  );
}

export interface LogoutScreenProps {
  /**
   * The `post_logout_redirect_uri` query parameter — `undefined` when the link
   * omits it. An ABSOLUTE URI by definition (the relying party's own origin),
   * which is why the relative-path `isSafeReturnUrl` guard does NOT apply to it.
   */
  readonly postLogoutRedirectUri?: string;
  /**
   * The `signed_out` query parameter, kept as the raw STRING the oracle compares
   * (`SignedOut == "true"`) rather than a parsed boolean — the exact-match
   * semantics are the spec, and any other value means the confirm step.
   */
  readonly signedOut?: string;
}

export function LogoutScreen({ postLogoutRedirectUri, signedOut }: LogoutScreenProps): ReactNode {
  const isSignedOut: boolean = signedOut === SIGNED_OUT;

  // `IsNullOrEmpty` parity: an empty URI is a malformed link, not a destination to
  // ask the server about.
  const hasRedirectUri: boolean =
    postLogoutRedirectUri !== undefined && postLogoutRedirectUri !== "";

  const validation = useQuery({
    queryKey: ["post-logout-redirect-uri", postLogoutRedirectUri],
    queryFn: async (): Promise<boolean> => {
      if (postLogoutRedirectUri === undefined) {
        // Unreachable: `enabled` gates this on `hasRedirectUri`. Present only to
        // narrow the prop to the `string` the call takes, without a cast.
        return false;
      }

      return isRedirectUriAllowed(
        await getWallowAuthSdk().auth.validateRedirectUri(postLogoutRedirectUri),
      );
    },
    // The oracle's `if (SignedOut == "true" && !IsNullOrEmpty(PostLogoutRedirectUri))`.
    // Validating on the confirm step would be wasted — the API re-validates the
    // parameter on the end-session request itself — and would leak a probe on
    // every render of the prompt.
    enabled: isSignedOut && hasRedirectUri,
    // A URI the allow-list refuses will not be on it a second later, and the
    // failure arm below is already the safe one; retrying only delays the answer.
    retry: false,
  });

  // FAIL CLOSED, in every direction. The C# `!IsSuccessStatusCode → false` arm
  // arrives here as a REJECTION (the facade's `unwrap()` throws on non-2xx), which
  // leaves `data` undefined — an unreachable validator must not become a reason to
  // trust the URI. In flight it is undefined too, so the link gates FIRST rather
  // than being rendered optimistically and retracted on the answer: a link
  // retracted late is a link a fast user can click.
  //
  // A failed validation surfaces NO error state, matching the oracle, which has no
  // error element on this screen at all. It is not the user's problem: they ARE
  // signed out, which is what they came for. Only the convenience link is lost.
  const returnUri: string | undefined =
    validation.data === true ? postLogoutRedirectUri : undefined;

  return (
    <Card>
      <CardHeading signedOut={isSignedOut} />
      {isSignedOut ? (
        <SignedOutLanding returnUri={returnUri} />
      ) : (
        <ConfirmStep postLogoutRedirectUri={postLogoutRedirectUri} />
      )}
      <LogoutFooter />
    </Card>
  );
}

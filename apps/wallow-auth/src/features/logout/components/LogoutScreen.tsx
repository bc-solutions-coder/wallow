import { buildConnectLogoutUrl, validateRedirectUriArgs } from "@bc-solutions-coder/sdk";
import { Card, MutedText, Text } from "@bc-solutions-coder/ui";
import { useQuery } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { accountValidateRedirectUriOptions } from "../api";
import { BASE_PATH, toAppHref } from "@shared/lib/base-path";
import { isRedirectUriAllowed } from "@shared/lib/return-url";

/**
 * The Logout screen (Wallow-vec7.3.5).
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
 * Mutations call the GENERATED operations and reads use the generated
 * `{op}Options()` factories, both bound to the request-scoped SDK off the router
 * context (`useRouteContext({ from: "__root__" })`). The OIDC URL builders are
 * pure and imported directly. There is no app-level facade (Wallow-pu6a.5.5).
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
 * `/consent` (Wallow-vec7.3.4). apps/wallow-auth's API surface
 * (`src/shared/lib/api-passthrough.server.ts`) is a PASSTHROUGH REVERSE PROXY mounting `/connect/**`
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
 *
 * For the same reason this screen takes no `decideReturnUrl` mode: a mode would
 * locally accept a RELATIVE value and skip the probe, widening trust. Its share
 * of the shared guard module is the `isRedirectUriAllowed` narrowing alone —
 * see `@shared/lib/return-url`.
 */

/**
 * The same-origin base the logout URL is built against: this app. See the origin
 * trap above — named rather than inlined so the empty default reads as a decision
 * rather than a forgotten argument. It carries the base path because under a
 * based build the passthrough answers under that prefix, not at the site root.
 */
const SAME_ORIGIN_BASE: string = BASE_PATH;

/**
 * The oracle's `SignedOut == "true"` — an ordinal string equality, not a boolean
 * parse. The exactness matters in the SAFE direction: anything else falls to the
 * confirm step, so a mangled link asks again rather than telling a still-signed-in
 * user they are signed out.
 */
const SIGNED_OUT = "true";

/**
 * The oracle's `BbCardTitle`. One testid across both phases, per the oracle; the
 * TEXT is what tells the two phases apart.
 */
function CardHeading({ signedOut }: { readonly signedOut: boolean }) {
  return (
    /*
      `as="h2"`, not the `<h1>` this card used to open: `AuthLayout` owns the
      page's one level-1 heading. The testid moves off the inner `<span>` and
      onto the heading itself — the span existed only to carry it, and it names
      exactly the same text.
    */
    <Text as="h2" variant="subheading" color="onCard" data-testid="logout-confirm-heading">
      {signedOut ? "Signed out" : "Sign out"}
    </Text>
  );
}

/**
 * The oracle's `else` arm: the prompt and the handoff.
 *
 * The control is an ANCHOR, as in the oracle (`<a href="@LogoutUrl">`). It has to
 * be a real navigation — `/connect/logout` is served by the passthrough proxy and is not in
 * the client-side route tree, so a router-driven control would 404 in-app. Keeping
 * the sign-out behind a CLICK rather than firing it on mount also keeps it off the
 * CSRF sink `<img src="/logout">` would otherwise be.
 */
function ConfirmStep({ postLogoutRedirectUri }: { readonly postLogoutRedirectUri?: string }) {
  const logoutUrl: string = buildConnectLogoutUrl(SAME_ORIGIN_BASE, postLogoutRedirectUri);

  return (
    <div className="space-y-4">
      <MutedText>Are you sure you want to sign out?</MutedText>
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
      <MutedText>You have been successfully signed out.</MutedText>
      {returnUri === undefined ? null : <ReturnLink uri={returnUri} />}
    </div>
  );
}

/** The oracle's `BbCardFooter` — outside the `@if`, so it renders on both arms. */
function LogoutFooter() {
  return (
    <div className="w-full text-center">
      <a
        href={toAppHref("/login")}
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
  const { sdk } = useRouteContext({ from: "__root__" });
  const isSignedOut: boolean = signedOut === SIGNED_OUT;

  // `IsNullOrEmpty` parity: an empty URI is a malformed link, not a destination to
  // ask the server about.
  const hasRedirectUri: boolean =
    postLogoutRedirectUri !== undefined && postLogoutRedirectUri !== "";

  // The `?? ""` is unreachable — `enabled` gates the read on `hasRedirectUri` —
  // and is present only to narrow the prop to the `string` the factory takes,
  // without a cast.
  const validation = useQuery({
    ...accountValidateRedirectUriOptions({
      client: sdk.client,
      ...validateRedirectUriArgs(postLogoutRedirectUri ?? ""),
    }),
    // The factory hands back the raw body; the verdict is this screen's reading
    // of it.
    select: isRedirectUriAllowed,
    // The oracle's `if (SignedOut == "true" && !IsNullOrEmpty(PostLogoutRedirectUri))`.
    // Validating on the confirm step would be wasted — the API re-validates the
    // parameter on the end-session request itself — and would leak a probe on
    // every render of the prompt.
    enabled: isSignedOut && hasRedirectUri,
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

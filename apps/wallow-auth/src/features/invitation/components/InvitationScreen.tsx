import type { InvitationResponse } from "@bc-solutions-coder/sdk";
import { forkBranding } from "@bc-solutions-coder/styles";
import { Button, Card, ErrorBanner, MutedText, Text } from "@bc-solutions-coder/ui";
import { useMutation, useQuery } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { invitationsAcceptMutation, invitationsVerifyOptions } from "../api";
import { toAppHref } from "@shared/lib/base-path";

/**
 * The InvitationLanding screen (Wallow-vec7.3.9).
 *
 * `token` and `isAuthenticated` arrive as props rather than being read inside the
 * component: the route owns the query string (the oracle's single
 * `[SupplyParameterFromQuery] Token`) and owns the auth-state probe, which keeps
 * this component a pure function of its inputs and testable without a router.
 * This is the seam `ResetPasswordForm` established and `VerifyEmailConfirm` and
 * `ConsentScreen` followed.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `invitation-loading`, `invitation-error`, `invitation-info`,
 * `invitation-expired`, `invitation-accept-error`, `invitation-accept`,
 * `invitation-decline`, `invitation-create-account`, `invitation-sign-in`.
 *
 * Writes use the generated `{op}Mutation()` factories and reads the generated
 * `{op}Options()` factories, both bound to the request-scoped SDK off the router
 * context (`useRouteContext({ from: "__root__" })`). The OIDC URL builders are
 * pure and imported directly. There is no app-level facade (Wallow-pu6a.5.5).
 *
 * ── THE AUTHENTICATED BRANCH IS A BUG FIX, NOT A PORT ────────────────────────
 *
 * The oracle's `_isAuthenticated` is ALWAYS FALSE in production: `Wallow.Auth`
 * registers no `AddAuthentication`/`UseAuthentication`/
 * `AddCascadingAuthenticationState` at all, so the `AuthenticationStateProvider`
 * it injects (InvitationLanding.razor:7,130) is seeded from an `HttpContext.User`
 * that no auth middleware ever populates. Its accept/decline branch is dead code,
 * and the two E2E tests that would have caught it never run (the containerised
 * e2e job is `if: false` in ci.yml). Porting BOTH branches here therefore fixes a
 * bug rather than reproducing behaviour: the API has always supported it —
 * `POST /v1/identity/invitations/{token}/accept` is `[Authorize]`
 * (InvitationsController.cs:82-84), `GET verify/{token}` is `[AllowAnonymous]`
 * (:67-70). The route supplies the real answer via the SDK's `getCurrentUser`
 * seam (Wallow-vec7.2.4).
 *
 * ── FOUR ORACLE BRANCHES COLLAPSE INTO TWO REJECTIONS, KEYED ON STATUS ───────
 *
 * The oracle's `AuthApiClient` SWALLOWS non-2xx into sentinels —
 * `VerifyInvitationAsync` returns null on any failure (AuthApiClient.cs:297-312),
 * `AcceptInvitationAsync` returns `IsSuccessStatusCode` (:314-322) — so it forks
 * sentinel-vs-`catch` and gives each of the two calls two messages. The facade's
 * `unwrap()` THROWS on every non-2xx, so each pair arrives as ONE rejection and
 * that fork is gone. What survives is `.status` (`toWallowError` always populates
 * it), and it is enough to keep all four messages:
 *
 *   - `verify/{token}` has exactly ONE failure return, a bare `NotFound()`
 *     (InvitationsController.cs:71-80) — so its 404 IS the oracle's null case,
 *     and anything else is the oracle's `catch`.
 *   - `{token}/accept` rejects an unknown/spent/expired token from the service
 *     and its aggregate (:82-91) — every one a 4xx, i.e. the oracle's
 *     "expired or already been used". A 5xx is the `catch`.
 *
 * Keyed on STATUS and deliberately NOT on `code`: unlike `/v1/identity/auth/*`
 * (bd memory `mfa-endpoints-mfacontroller-return-business-failures-as-a`), these
 * two endpoints send no machine-readable code at all — `NotFound()` is a bare
 * status with no body — so every rejection here is `code: "UNKNOWN"` (bd memory
 * `wallow-auth-auth-client-ts-wallowerror-code-loss`). A code-keyed mapping would
 * collapse all four messages into the generic one.
 *
 * ── WHY THERE IS NO `isSafeReturnUrl` GUARD HERE ─────────────────────────────
 *
 * Every screen that ACCEPTS a returnUrl guards it (bd memory
 * `returnurl-guard-refuse-dont-sanitize`). This one accepts none: the oracle's
 * only query parameter is `Token`, and the two returnUrls below are BUILT by this
 * screen as `/invitation?token=…` — a literal single leading `/`, which is
 * exactly what the guard checks for. Guarding a constant we just wrote would be
 * dead code. The attacker-controlled part is the TOKEN, and it is defused by
 * percent-encoding the whole returnUrl (so `x&returnUrl=//evil.example` cannot
 * smuggle a second parameter into the link), which is what the oracle's
 * `Uri.EscapeDataString` does and what the tests pin.
 */

/** The oracle's `IsNullOrWhiteSpace(Token)` guard message. */
const NO_TOKEN_MESSAGE = "No invitation token provided.";

/** The oracle's `_invitation is null` branch, reached here via HTTP 404. */
const INVALID_INVITATION_MESSAGE = "This invitation is not valid or has already been used.";

/** The oracle's `catch` around the verify call: any other failure. */
const VERIFY_FAILURE_MESSAGE = "Unable to verify this invitation. Please try again later.";

/** The oracle's `success == false` branch on accept, reached here via any 4xx. */
const ACCEPT_REJECTED_MESSAGE =
  "Unable to accept this invitation. It may have expired or already been used.";

/** The oracle's `catch` around the accept call: any other failure. */
const ACCEPT_FAILURE_MESSAGE =
  "An error occurred while accepting the invitation. Please try again.";

/** The oracle's expired `BbAlert`. */
const EXPIRED_MESSAGE =
  "This invitation has expired. Please ask your administrator to send a new one.";

/**
 * The oracle's `NavigateTo("/", forceLoad: true)` target, and the decline link's
 * `Href="/"`.
 */
const HOME_HREF: string = toAppHref("/");

/** The way out of the error state (InvitationLanding.razor:32-34). */
const SIGN_IN_HREF = "/login";

/** The only failure status `verify/{token}` has — see the seam note above. */
const NOT_FOUND_STATUS = 404;

/** The 4xx band: every way `{token}/accept` says "no" to a well-formed request. */
const CLIENT_ERROR_MIN = 400;
const CLIENT_ERROR_MAX = 500;

/**
 * The HTTP status of a rejection, if it carries one.
 *
 * Narrowed structurally rather than with `instanceof WallowError`: that class is
 * exported from the SDK's `./server` entry, and screens may not import the SDK at
 * all. Defensive for the same reason — a network-level rejection carries no
 * `status` and must fall through to the generic copy.
 */
function statusOf(cause: unknown): number | undefined {
  if (typeof cause === "object" && cause !== null && "status" in cause) {
    const status: unknown = (cause as { readonly status: unknown }).status;

    if (typeof status === "number") {
      return status;
    }
  }

  return undefined;
}

/** The oracle's two verify messages, chosen by status — see the seam note. */
function verifyFailureMessage(cause: unknown): string {
  return statusOf(cause) === NOT_FOUND_STATUS ? INVALID_INVITATION_MESSAGE : VERIFY_FAILURE_MESSAGE;
}

/**
 * The oracle's two accept messages, chosen by status. Keyed on the whole 4xx band
 * rather than 404 alone: the service throws `EntityNotFoundException` for an
 * unknown or spent token, but an EXPIRED one is refused by the aggregate, and
 * "the invitation is expired" is precisely the case this copy names. Telling that
 * user "an error occurred, please try again" would send them retrying a request
 * that can never succeed.
 */
function acceptFailureMessage(cause: unknown): string {
  const status: number | undefined = statusOf(cause);

  if (status !== undefined && status >= CLIENT_ERROR_MIN && status < CLIENT_ERROR_MAX) {
    return ACCEPT_REJECTED_MESSAGE;
  }

  return ACCEPT_FAILURE_MESSAGE;
}

/**
 * The oracle's `Status is "Expired" || ExpiresAt < UtcNow` (InvitationLanding.
 * razor:147). The OR is load-bearing: `Status` only flips when the
 * `CleanupExpiredAsync` sweep gets to it (InvitationService.cs:71-89), so between
 * the expiry instant and the sweep the date is the ONLY branch that catches it.
 *
 * An unparseable `expiresAt` yields `NaN`, and every `NaN` comparison is false —
 * so a malformed date falls through to "not expired" and lets the SERVER refuse
 * the accept, rather than this screen declaring a live invitation dead over a
 * date it could not read.
 */
function isExpired(invitation: InvitationResponse): boolean {
  return invitation.status === "Expired" || Date.parse(invitation.expiresAt) < Date.now();
}

/**
 * The link back to this screen that both anonymous actions carry, so the visitor
 * lands HERE again once they have a session — and then gets the accept button.
 *
 * The token is interpolated RAW and the whole string is encoded by the callers
 * below (the oracle's `Uri.EscapeDataString($"/invitation?token={Token}")`), so a
 * token containing `&` or `=` stays one parameter value instead of becoming a
 * second query parameter on the outer link.
 */
function selfReturnUrl(token: string): string {
  return `/invitation?token=${token}`;
}

/** The oracle's `GetRegisterUrl()` (InvitationLanding.razor:196-201). */
function registerHref(email: string, token: string): string {
  // `email` is INERT: `/register` reads only `client_id` and `returnUrl`
  // (Wallow-vec7.3.8).
  // Kept because it is the oracle's link contract and a `/register` that prefills
  // the invited address is a plausible follow-up — not because it prefills today.
  // Both links go through `toAppHref` because they render as raw `<a href>`s the
  // router never sees. `selfReturnUrl` stays unprefixed on purpose: it is cargo
  // the destination screen replays through `navigate()`, which applies the base
  // path itself, so prefixing it here would double it.
  return toAppHref(
    `/register?email=${encodeURIComponent(email)}&returnUrl=${encodeURIComponent(selfReturnUrl(token))}`,
  );
}

/** The oracle's `GetLoginUrl()` (InvitationLanding.razor:203-207). */
function loginHref(token: string): string {
  return toAppHref(`/login?returnUrl=${encodeURIComponent(selfReturnUrl(token))}`);
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="text-center space-y-1">
      <Text as="h2" variant="subheading" color="onCard">
        You&apos;ve been invited
      </Text>
      <MutedText>Join {forkBranding.appName}</MutedText>
    </div>
  );
}

/** The oracle's `_isLoading` branch: the verify is in flight and nothing else. */
export function InvitationLoading() {
  return (
    <MutedText className="text-center" data-testid="invitation-loading">
      Loading invitation...
    </MutedText>
  );
}

/** The oracle's danger `BbAlert` plus the one way out of the dead end. */
function ErrorState({ message }: { readonly message: string }) {
  return (
    <div className="space-y-4">
      <ErrorBanner data-testid="invitation-error">{message}</ErrorBanner>
      <a
        href={SIGN_IN_HREF}
        className="block text-center text-sm text-muted-foreground hover:text-foreground"
      >
        Back to sign in
      </a>
    </div>
  );
}

/**
 * The oracle's info `BbAlert`. The invited ADDRESS is the substance of it: it
 * tells the visitor which identity the invitation is for, which is the difference
 * between accepting it on the right account and on the wrong one.
 */
function InvitationInfo({ email }: { readonly email: string }) {
  return (
    <Text
      as="p"
      variant="bodySm"
      className="rounded-md border border-border bg-muted/40 p-3"
      data-testid="invitation-info"
    >
      You&apos;ve been invited to join {forkBranding.appName} as {email}.
    </Text>
  );
}

/** The oracle's expired `BbAlert`, which replaces BOTH action branches. */
function ExpiredNotice() {
  return <ErrorBanner data-testid="invitation-expired">{EXPIRED_MESSAGE}</ErrorBanner>;
}

/**
 * The oracle's two `BbButton`s for a signed-in visitor.
 *
 * Decline is a LINK (`Href="/"`, InvitationLanding.razor:75-81) and stays one: it
 * does NOT revoke the invitation, which stays open for a later visit — "no
 * thanks" is a navigation, not a mutation. While an accept is in flight it loses
 * its `href` rather than merely being marked `aria-disabled`: an aria-disabled
 * anchor still navigates on click, and letting the user leave mid-POST would hide
 * the outcome of a request that is changing their tenant membership.
 */
function AcceptActions(props: { readonly isSubmitting: boolean; readonly onAccept: () => void }) {
  const { isSubmitting, onAccept } = props;

  return (
    <div className="flex gap-2">
      <Button
        type="button"
        width="auto"
        className="flex-1"
        data-testid="invitation-accept"
        disabled={isSubmitting}
        onClick={onAccept}
      >
        Yes, join
      </Button>
      {/*
        Decline stays an ANCHOR — see the note above — so it composes onto one
        through `render` rather than becoming a button. `nativeButton={false}`
        tells Base UI the rendered element is not a `<button>`, which is what
        keeps it from logging a dev-mode error. The catalog `Button` supplies the
        `link` role itself, and drops it for exactly the window where this anchor
        has no `href` and is therefore no longer a destination.
      */}
      <Button
        render={<a href={isSubmitting ? undefined : HOME_HREF} />}
        nativeButton={false}
        variant="outline"
        width="auto"
        className="flex-1"
        data-testid="invitation-decline"
        aria-disabled={isSubmitting}
      >
        No thanks
      </Button>
    </div>
  );
}

/** The oracle's authenticated branch: the prompt, any accept error, the buttons. */
function AuthenticatedActions(props: {
  readonly acceptError: string | null;
  readonly isSubmitting: boolean;
  readonly onAccept: () => void;
}) {
  const { acceptError, isSubmitting, onAccept } = props;

  return (
    <div className="space-y-4">
      <Text as="p" variant="bodySm" align="center">
        Would you like to join {forkBranding.appName}?
      </Text>
      {acceptError === null ? null : (
        <ErrorBanner data-testid="invitation-accept-error">{acceptError}</ErrorBanner>
      )}
      <AcceptActions isSubmitting={isSubmitting} onAccept={onAccept} />
    </div>
  );
}

/**
 * The oracle's anonymous branch. No accept button: the POST is `[Authorize]`d
 * (InvitationsController.cs:82-83), so offering it here would buy the visitor a
 * 401 instead of a membership.
 */
function AnonymousActions(props: { readonly email: string; readonly token: string }) {
  const { email, token } = props;

  return (
    <div className="space-y-3">
      {/*
        Both are real navigations, so they compose the recipe onto anchors the
        way `Decline` does; the catalog `Button` announces them as links.
      */}
      <Button
        render={<a href={registerHref(email, token)} />}
        nativeButton={false}
        data-testid="invitation-create-account"
      >
        Create account
      </Button>
      <Button
        render={<a href={loginHref(token)} />}
        nativeButton={false}
        variant="outline"
        data-testid="invitation-sign-in"
      >
        Sign in to accept
      </Button>
    </div>
  );
}

/**
 * The oracle's action branches, in ITS order: expiry FIRST
 * (InvitationLanding.razor:46-54). Signing in to accept a dead invitation is a
 * wasted round trip, and accepting one is a request the server will refuse.
 */
function InvitationActions(props: {
  readonly invitation: InvitationResponse;
  readonly token: string;
  readonly isAuthenticated: boolean;
  readonly acceptError: string | null;
  readonly isSubmitting: boolean;
  readonly onAccept: () => void;
}) {
  const { invitation, token, isAuthenticated, acceptError, isSubmitting, onAccept } = props;

  if (isExpired(invitation)) {
    return <ExpiredNotice />;
  }

  if (!isAuthenticated) {
    return <AnonymousActions email={invitation.email} token={token} />;
  }

  return (
    <AuthenticatedActions
      acceptError={acceptError}
      isSubmitting={isSubmitting}
      onAccept={onAccept}
    />
  );
}

export interface InvitationScreenProps {
  /**
   * The `token` query parameter — the oracle's `[SupplyParameterFromQuery]
   * Token`. `undefined` when the link omits it (or carries a non-string, which
   * TanStack's `validateSearch` will have JSON-parsed into a boolean/number).
   */
  readonly token?: string;
  /**
   * Whether the visitor already has a session — the oracle's
   * `AuthStateProvider.GetAuthenticationStateAsync()` branch. Supplied as a prop
   * so this component stays a pure function of its inputs, matching the seam
   * `ResetPasswordForm`/`ConsentScreen` established. The route answers it with
   * the SDK's `getCurrentUser` probe (Wallow-vec7.2.4).
   */
  readonly isAuthenticated: boolean;
}

export function InvitationScreen({ token, isAuthenticated }: InvitationScreenProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });

  // The oracle's `IsNullOrWhiteSpace(Token)`: whitespace is not a token, and this
  // guard runs BEFORE the call, so `?token=%20` never reaches the endpoint.
  const tokenIsPresent: boolean = token !== undefined && token.trim() !== "";

  // The `?? ""` is unreachable — `enabled` gates the read on `tokenIsPresent` —
  // and is present only to narrow the prop to the `string` the factory takes,
  // without a cast.
  const query = useQuery({
    ...invitationsVerifyOptions({ client: sdk.client, path: { token: token ?? "" } }),
    // Carries the oracle's guard to React Query: a tokenless link short-circuits
    // to the error state without ever going to the network.
    enabled: tokenIsPresent,
  });

  // SPREAD, never passed straight through: the generated factory carries only a
  // `mutationFn`, so handing it to `useMutation` as the whole options object would
  // silently drop the `onSuccess` navigation below.
  const acceptMutation = useMutation({
    ...invitationsAcceptMutation({ client: sdk.client }),
    onSuccess: () => {
      // A FULL navigation, not `navigate()` — the oracle's
      // `NavigateTo("/", forceLoad: true)` (:179). The reload is load-bearing:
      // accepting changes the user's tenant membership, and a client-side
      // transition would carry the pre-acceptance session state into the
      // destination.
      globalThis.location.href = HOME_HREF;
    },
  });

  if (!tokenIsPresent || token === undefined) {
    return (
      <Card>
        <CardHeading />
        <ErrorState message={NO_TOKEN_MESSAGE} />
      </Card>
    );
  }

  return (
    <Card>
      <CardHeading />
      <InvitationBody
        token={token}
        isAuthenticated={isAuthenticated}
        invitation={query.data ?? null}
        isPending={query.isPending}
        error={query.isError ? query.error : null}
        acceptError={acceptMutation.isError ? acceptFailureMessage(acceptMutation.error) : null}
        isSubmitting={acceptMutation.isPending}
        onAccept={() => {
          // The generated artifact's REQUEST object, not the bare token: this
          // endpoint takes the token on the PATH, so a bare string would resolve to
          // `/invitations//accept`.
          acceptMutation.mutate({ path: { token } });
        }}
      />
    </Card>
  );
}

/**
 * The screen's states, in the order the oracle's if/else-if chain applies them.
 * Split out of `InvitationScreen` so the card above stays flat and the hooks stay
 * unconditional.
 */
function InvitationBody(props: {
  readonly token: string;
  readonly isAuthenticated: boolean;
  readonly invitation: InvitationResponse | null;
  readonly isPending: boolean;
  readonly error: unknown;
  readonly acceptError: string | null;
  readonly isSubmitting: boolean;
  readonly onAccept: () => void;
}) {
  const {
    token,
    isAuthenticated,
    invitation,
    isPending,
    error,
    acceptError,
    isSubmitting,
    onAccept,
  } = props;

  if (isPending) {
    return <InvitationLoading />;
  }

  // `invitation === null` is the unreachable queryFn narrow; treated as a failure
  // rather than crashed on, since there is nothing to render without it.
  if (error !== null || invitation === null) {
    return <ErrorState message={verifyFailureMessage(error)} />;
  }

  return (
    <div className="space-y-4">
      <InvitationInfo email={invitation.email} />
      <InvitationActions
        invitation={invitation}
        token={token}
        isAuthenticated={isAuthenticated}
        acceptError={acceptError}
        isSubmitting={isSubmitting}
        onAccept={onAccept}
      />
    </div>
  );
}

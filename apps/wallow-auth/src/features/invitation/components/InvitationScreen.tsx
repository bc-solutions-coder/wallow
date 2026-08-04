import type { InvitationResponse } from "@bc-solutions-coder/sdk";
import { forkBranding } from "@bc-solutions-coder/styles";
import { Button, ErrorBanner, MutedText, QuietLink, Text } from "@bc-solutions-coder/ui";
import { useMutation, useQuery } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactElement, ReactNode } from "react";
import { invitationsAcceptMutation, invitationsVerifyOptions } from "../api";
import {
  acceptFailureMessage,
  EXPIRED_MESSAGE,
  isExpired,
  NO_TOKEN_MESSAGE,
  verifyFailureMessage,
} from "../invitation-result";
import { AuthScreen } from "@shared/components/auth-screen";
import { toAppHref } from "@shared/lib/base-path";

/**
 * The InvitationLanding screen (Wallow-vec7.3.9).
 *
 * `token` and `isAuthenticated` arrive as props rather than being read inside the
 * component: the route owns the query string (the oracle's single
 * `[SupplyParameterFromQuery] Token`) and owns the auth-state probe, which keeps
 * this component a pure function of its inputs and testable without a router.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `invitation-loading`, `invitation-error`, `invitation-info`,
 * `invitation-expired`, `invitation-accept-error`, `invitation-accept`,
 * `invitation-decline`, `invitation-create-account`, `invitation-sign-in`.
 *
 * The rejection→copy mapping and the expiry predicate live in
 * `../invitation-result`, which documents why they are keyed on HTTP status
 * rather than on a machine token.
 *
 * There is no `useAppForm` here because there is no form: the screen verifies a
 * token from the link and offers an accept BUTTON, and its one write takes the
 * token on the path with nothing typed.
 *
 * ── THE AUTHENTICATED BRANCH IS A BUG FIX, NOT A PORT ────────────────────────
 *
 * The oracle's `_isAuthenticated` is ALWAYS FALSE in production: `Wallow.Auth`
 * registers no `AddAuthentication`/`UseAuthentication`/
 * `AddCascadingAuthenticationState` at all, so the `AuthenticationStateProvider`
 * it injects (InvitationLanding.razor:7,130) is seeded from an `HttpContext.User`
 * that no auth middleware ever populates. Its accept/decline branch is dead code.
 * Porting BOTH branches here therefore fixes a bug rather than reproducing
 * behaviour: the API has always supported it —
 * `POST /v1/identity/invitations/{token}/accept` is `[Authorize]`
 * (InvitationsController.cs:82-84), `GET verify/{token}` is `[AllowAnonymous]`
 * (:67-70). The route supplies the real answer via the SDK's `getCurrentUser`
 * seam (Wallow-vec7.2.4).
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
 * smuggle a second parameter into the link).
 */

/** The oracle's `BbCardHeader`. */
const TITLE = "You've been invited";
const DESCRIPTION = `Join ${forkBranding.appName}`;

/**
 * The oracle's `NavigateTo("/", forceLoad: true)` target, and the decline link's
 * `Href="/"`.
 */
const HOME_HREF: string = toAppHref("/");

/** The way out of the error state (InvitationLanding.razor:32-34). */
const SIGN_IN_HREF = "/login";

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
  // (Wallow-vec7.3.8). Kept because it is the oracle's link contract.
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

/** The oracle's `_isLoading` branch: the verify is in flight and nothing else. */
export function InvitationLoading(): ReactElement {
  return (
    <MutedText className="text-center" data-testid="invitation-loading">
      Loading invitation...
    </MutedText>
  );
}

/** The one way out of the error state's dead end. */
function BackToSignIn(): ReactElement {
  return (
    <QuietLink href={SIGN_IN_HREF} className="block text-center">
      Back to sign in
    </QuietLink>
  );
}

/** The oracle's danger `BbAlert`, which replaces the whole body. */
function ErrorScreen({ message }: { readonly message: string }): ReactElement {
  return (
    <AuthScreen
      title={TITLE}
      description={DESCRIPTION}
      error={message}
      errorTestId="invitation-error"
      footer={<BackToSignIn />}
    />
  );
}

/**
 * The oracle's info `BbAlert`. The invited ADDRESS is the substance of it: it
 * tells the visitor which identity the invitation is for, which is the difference
 * between accepting it on the right account and on the wrong one.
 */
function InvitationInfo({ email }: { readonly email: string }): ReactElement {
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
function ExpiredNotice(): ReactElement {
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
function AcceptActions(props: {
  readonly isSubmitting: boolean;
  readonly onAccept: () => void;
}): ReactElement {
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
}): ReactElement {
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
function AnonymousActions(props: { readonly email: string; readonly token: string }): ReactElement {
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
}): ReactElement {
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
   * so this component stays a pure function of its inputs. The route answers it
   * with the SDK's `getCurrentUser` probe (Wallow-vec7.2.4).
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
    return <ErrorScreen message={NO_TOKEN_MESSAGE} />;
  }

  // `enabled: false` also reports `isPending`, so this branch is only reachable
  // once the guard above has passed.
  if (query.isPending) {
    return (
      <AuthScreen title={TITLE} description={DESCRIPTION}>
        <InvitationLoading />
      </AuthScreen>
    );
  }

  // `data === undefined` is the unreachable queryFn narrow; treated as a failure
  // rather than crashed on, since there is nothing to render without it.
  if (query.isError || query.data === undefined) {
    return <ErrorScreen message={verifyFailureMessage(query.isError ? query.error : null)} />;
  }

  return (
    <AuthScreen title={TITLE} description={DESCRIPTION}>
      <InvitationInfo email={query.data.email} />
      <InvitationActions
        invitation={query.data}
        token={token}
        isAuthenticated={isAuthenticated}
        acceptError={acceptMutation.isError ? acceptFailureMessage(acceptMutation.error) : null}
        isSubmitting={acceptMutation.isPending}
        onAccept={() => {
          // The generated artifact's REQUEST object, not the bare token: this
          // endpoint takes the token on the PATH, so a bare string would resolve to
          // `/invitations//accept`.
          acceptMutation.mutate({ path: { token } });
        }}
      />
    </AuthScreen>
  );
}

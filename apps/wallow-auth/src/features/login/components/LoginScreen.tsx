import { useNavigate } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";
import { type AuthDisposition, authDispositionOf, errorParamMessage } from "../auth-result";
import type { LoginTab } from "../panel";
import { ExternalProviders } from "./ExternalProviders";
import { MagicLinkLoginForm } from "./MagicLinkLoginForm";
import { OtpLoginForm } from "./OtpLoginForm";
import { PasswordLoginForm } from "./PasswordLoginForm";

/**
 * The Login screen (Wallow-vec7.3.11 / 2.8a), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/Login.razor`.
 *
 * `returnUrl`, `clientId` and `error` arrive as props rather than being read from
 * the router inside the component: the route owns the query string (the oracle's
 * `[SupplyParameterFromQuery]` properties) and hands them down, which keeps this
 * component a pure function of its inputs and testable without a router — the
 * seam `ResetPasswordForm` established and `ConsentScreen`/`MfaChallengeForm`
 * followed.
 *
 * ── THIS FILE IS THE SHELL, NOT THE SCREEN ───────────────────────────────────
 *
 * This is the HEAD of a five-bead chain over one screen: `.3.12` (magic-link),
 * `.3.13` (OTP), `.3.14` (external providers) and `.3.15` (MFA hand-off) all
 * extend it. So it holds ONLY what the oracle SHARES across its three tabs —
 * `_activeTab`, the one `_errorMessage` banner, `_signedIn`, the enrollment
 * banner, and `HandleSuccessfulAuth` — and delegates each tab's fields, mutation
 * and error copy to a PANEL (`../panel` states the contract; `PasswordLoginForm`
 * is the reference implementation of it).
 *
 * The navigation decision itself lives once, in `../auth-result`, and is PURE.
 * `.3.12`/`.3.13`/`.3.15` call `onAuthResult` and let it decide; they must NOT
 * re-derive it.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3), with
 * TWO exceptions the inventory records as gaps — the oracle renders these
 * elements but tags neither, so `{page}-{element}` names are invented per the
 * scout's mandate:
 *
 *     login-signed-in               the `_signedIn` success alert
 *     login-mfa-enrollment-banner   `<MfaEnrollmentBanner Visible=… />`
 *
 * ── THE ORIGIN DIVERGENCE (inherited from Wallow-vec7.3.4/.3.6) ──────────────
 *
 * The oracle's `ApiBaseUrl` prepend — `BuildApiReturnUrl`, and the hand-rolled
 * exchange-ticket URL at L544-550 — is deliberately NOT ported. This app's h3
 * server (`src/lib/auth-server.ts`) is a passthrough reverse proxy mounting
 * `/v1/**` and `/connect/**` at the ROOT, so this origin hosts them (bd memory
 * `wallow-auth-screens-must-pass-origin-same-origin`). Prepending an absolute
 * origin would send the browser cross-origin and DROP the SameSite auth cookie
 * the exchange-ticket endpoint just set — which is the entire point of the ticket.
 *
 * ── NO cookieRelay (for `.3.15`) ─────────────────────────────────────────────
 *
 * The oracle's `BuildMfaRedirectUrl` threads
 * `AuthClient.GetPendingCookieRelayKey()`. That subsystem was deliberately deleted
 * in Wallow-vec7.1.3 and the facade has no such method. The h3 proxy forwards
 * `Set-Cookie` verbatim, so the partial-auth cookie is already in the jar by the
 * time the MFA branch is taken — which is why the hand-off is `navigate()` (the
 * client router) and not a full page load. Do not re-add `cookieRelay`.
 */

/** This app's own origin — see the origin-divergence note above. */
const SAME_ORIGIN = "";

/** The oracle's `MfaEnrollmentBanner` description (Shared/MfaEnrollmentBanner.razor). */
function formatGraceDeadline(deadline: string): string {
  // The oracle's `ToString("MMMM d, yyyy")`. The locale is PINNED rather than
  // left to the host: an ambient locale would render a different date to a user
  // than the one the copy was written and reviewed against.
  return new Date(deadline).toLocaleDateString("en-US", {
    month: "long",
    day: "numeric",
    year: "numeric",
  });
}

/**
 * The oracle's `<MfaEnrollmentBanner Visible=… GraceDeadline=… />`, shown when the
 * user's org requires MFA but their grace period has not expired.
 *
 * The oracle's dismiss button is not ported: it dismisses a warning the user
 * cannot act on again until they next sign in, and the banner is only ever SEEN
 * in the one configuration where the screen does not navigate away.
 */
function MfaEnrollmentBanner({ deadline }: { readonly deadline: string }) {
  return (
    <div
      className="rounded-md border border-warning bg-warning/10 p-3 space-y-2"
      data-testid="login-mfa-enrollment-banner"
    >
      <p className="text-sm font-medium text-foreground">MFA enrollment required</p>
      <p className="text-sm text-muted-foreground">
        Your organization requires two-factor authentication. Please set it up before{" "}
        {formatGraceDeadline(deadline)}.
      </p>
      <a className="inline-block text-sm font-medium text-primary" href="/mfa/enroll">
        Set up now
      </a>
    </div>
  );
}

/** The oracle's danger `BbAlert` — the ONE banner all three tabs share. */
function ErrorBanner({ message }: { readonly message: string }) {
  return (
    <div
      className="rounded-md border border-destructive bg-destructive/10 p-3"
      data-testid="login-error"
    >
      <p className="text-sm text-destructive">{message}</p>
    </div>
  );
}

/** The oracle's `_signedIn` success `BbAlert`, which replaces the whole tab block. */
function SignedInBanner() {
  return (
    <div
      className="rounded-md border border-success bg-success/10 p-3"
      data-testid="login-signed-in"
    >
      <p className="text-sm text-foreground">You are now signed in.</p>
    </div>
  );
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1 text-center">
      <h2 className="text-lg font-semibold text-card-foreground">Sign in to your account</h2>
      <p className="text-sm text-muted-foreground">Enter your credentials to continue</p>
    </div>
  );
}

/**
 * One tab button. `aria-selected` is the observable, accessible form of the
 * oracle's `TabClass(…)` styling — a tab strip that only says which tab is active
 * in a CSS class says it to nobody using a screen reader.
 */
function TabButton(props: {
  readonly tab: LoginTab;
  readonly label: string;
  readonly active: boolean;
  readonly onSelect: (tab: LoginTab) => void;
}) {
  const { tab, label, active, onSelect } = props;

  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      className={
        active
          ? "flex-1 py-2 text-sm font-medium text-primary border-b-2 border-primary"
          : "flex-1 py-2 text-sm font-medium text-muted-foreground hover:text-primary"
      }
      data-testid={`login-tab-${tab}`}
      onClick={() => {
        onSelect(tab);
      }}
    >
      {label}
    </button>
  );
}

/** The oracle's tab strip. */
function TabStrip(props: {
  readonly activeTab: LoginTab;
  readonly onSelect: (tab: LoginTab) => void;
}) {
  const { activeTab, onSelect } = props;

  return (
    <div className="flex border-b border-border mb-4" role="tablist">
      <TabButton
        tab="password"
        label="Password"
        active={activeTab === "password"}
        onSelect={onSelect}
      />
      <TabButton
        tab="magic-link"
        label="Magic Link"
        active={activeTab === "magic-link"}
        onSelect={onSelect}
      />
      <TabButton tab="otp" label="OTP" active={activeTab === "otp"} onSelect={onSelect} />
    </div>
  );
}

/**
 * The active tab's panel — the oracle's `else if` chain, one panel at a time.
 *
 * `OtpLoginForm` (Wallow-vec7.3.13) needed no new props to host, exactly as
 * `.3.11` predicted: `SendOtpRequest` is `{ email }` and `VerifyOtpRequest` is
 * `{ email, code, rememberMe? }`, so neither `returnUrl` nor `clientId` is cargo
 * this tab carries — both halves of it are driven entirely by what the user types.
 */
function TabPanel(props: {
  readonly activeTab: LoginTab;
  readonly magicLinkToken?: string;
  readonly returnUrl?: string;
  readonly clientId?: string;
  readonly onAuthResult: (body: unknown) => void;
  readonly onError: (message: string | null) => void;
}) {
  const { activeTab, magicLinkToken, returnUrl, clientId, onAuthResult, onError } = props;

  if (activeTab === "magic-link") {
    // `returnUrl`/`clientId` are cargo for the SEND (`SendMagicLinkRequest` carries
    // them so the emailed link can resume this OIDC flow) — NOT a destination this
    // panel navigates to. It never navigates; it reports up. See `../panel`.
    return (
      <MagicLinkLoginForm
        token={magicLinkToken}
        returnUrl={returnUrl}
        clientId={clientId}
        onAuthResult={onAuthResult}
        onError={onError}
      />
    );
  }

  if (activeTab === "otp") {
    // Like the magic-link panel, this one never navigates: `otp/verify` hands back
    // the same `AuthResponse` shape, so it reports the RAW body up and the shell's
    // one `authDispositionOf` decides. See `../panel`.
    return <OtpLoginForm onAuthResult={onAuthResult} onError={onError} />;
  }

  return <PasswordLoginForm onAuthResult={onAuthResult} onError={onError} />;
}

/**
 * The oracle's `RegisterUrl`: `client_id` and `returnUrl` ride along as CARGO so
 * a user who registers instead of signing in lands back in the same OIDC flow
 * rather than at a dead end.
 *
 * No guard: this is an in-app relative link, not a destination this screen
 * navigates to — `/register` re-reads and re-guards the value itself. Each part is
 * `encodeURIComponent`-ed so a returnUrl carrying `&` cannot smuggle a second key
 * into the link.
 */
function registerHref(clientId: string | undefined, returnUrl: string | undefined): string {
  const params: string[] = [];

  if (clientId !== undefined && clientId !== "") {
    params.push(`client_id=${encodeURIComponent(clientId)}`);
  }

  if (returnUrl !== undefined && returnUrl !== "") {
    params.push(`returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  return params.length === 0 ? "/register" : `/register?${params.join("&")}`;
}

/** The oracle's `BbCardFooter` sign-up prompt. */
function RegisterPrompt({ href }: { readonly href: string }) {
  return (
    <div className="text-center text-sm text-muted-foreground">
      {"Don't have an account? "}
      <a className="font-medium text-primary" href={href} data-testid="login-register-link">
        Sign up
      </a>
    </div>
  );
}

export interface LoginScreenProps {
  /**
   * The OIDC `returnUrl` the authorize endpoint threaded through the login link.
   * RELATIVE by construction — `AuthorizationController.cs:53` builds it as
   * `Request.PathBase + Request.Path + Request.QueryString` and gates it on
   * `Url.IsLocalUrl` before redirecting here.
   */
  readonly returnUrl?: string;
  /** The OIDC `client_id` carried alongside `returnUrl`; register-link cargo. */
  readonly clientId?: string;
  /** The oracle's `[SupplyParameterFromQuery] Error` — a failure hand-back. */
  readonly error?: string;
  /**
   * The oracle's `[SupplyParameterFromQuery(Name = "magicLinkToken")]`
   * (Wallow-vec7.3.12). Present ONLY when the user arrived from the link
   * `MagicLinkRequestedNotificationHandler.cs:21` emailed them; it is redeemed on
   * load by the magic-link panel.
   */
  readonly magicLinkToken?: string;
}

export function LoginScreen({
  returnUrl,
  clientId,
  error,
  magicLinkToken,
}: LoginScreenProps): ReactNode {
  const navigate = useNavigate();
  // The oracle's `HandleVerifyMagicLink` sets `_activeTab = LoginTab.MagicLink`
  // before it does anything else (Login.razor:405), and `OnInitializedAsync` calls it
  // whenever the token is non-empty: a user who clicked a link in their inbox must
  // land on the tab where the outcome will be reported. `""` is not nullish, and
  // `IsNullOrEmpty` parity means it is not a token.
  const [activeTab, setActiveTab] = useState<LoginTab>(
    magicLinkToken === undefined || magicLinkToken === "" ? "password" : "magic-link",
  );
  // The oracle's `OnInitialized` seeds the banner from the `Error` query param.
  const [errorMessage, setErrorMessage] = useState<string | null>(() => errorParamMessage(error));
  const [signedIn, setSignedIn] = useState(false);
  const [graceDeadline, setGraceDeadline] = useState<string | null>(null);

  // EMPTINESS BEFORE SAFETY, and the `&&` short-circuit is what enforces it: `""`
  // is not nullish and IS unsafe, so consulting the guard for it would route an
  // ordinary direct sign-in to /error. `authDispositionOf` re-checks emptiness on
  // its own path; this keeps the two in agreement. See the guard note there.
  const returnUrlIsSafe: boolean =
    returnUrl !== undefined &&
    returnUrl !== "" &&
    getWallowAuthSdk().oidc.isSafeReturnUrl(returnUrl);

  /**
   * The oracle's `HandleSuccessfulAuth`, and the shell's whole reason to exist:
   * ONE copy, shared by every tab. `.3.12`/`.3.13` route their verify responses
   * here; `.3.15` owns the MFA arms of the disposition it consumes.
   */
  const handleAuthResult = (body: unknown): void => {
    const disposition: AuthDisposition = authDispositionOf(body, returnUrl, returnUrlIsSafe);

    setGraceDeadline(disposition.graceDeadline);

    const outcome = disposition.outcome;

    switch (outcome.kind) {
      case "navigate": {
        // The client router: `/mfa/*` and `/error` are in-app routes, and the
        // partial-auth cookie is already in the jar, so a full page load buys
        // nothing. (`/error` also has no registered search shape to bind to —
        // bd memory `tanstack-router-redirect-to-an-unregistered-route-use-href-not-to`.)
        void navigate({ href: outcome.href });
        return;
      }
      case "exchange-ticket": {
        // A FULL navigation: the exchange endpoint is served by the h3 reverse
        // proxy, not by the client-side route tree, which would 404 in-app.
        globalThis.location.href = getWallowAuthSdk().oidc.buildExchangeTicketUrl(
          SAME_ORIGIN,
          outcome.ticket,
          outcome.returnUrl,
        );
        return;
      }
      case "signed-in": {
        setSignedIn(true);
        return;
      }
      default: {
        setErrorMessage(outcome.message);
      }
    }
  };

  const handleSwitchTab = (tab: LoginTab): void => {
    setActiveTab(tab);
    // The oracle's `SwitchTab` resets `_errorMessage`: one banner is shared by all
    // three tabs, so a password failure must not follow the user into the
    // magic-link tab and blame it for something it did not do.
    setErrorMessage(null);
  };

  return (
    <div className="rounded-lg border border-border bg-card p-6 space-y-4">
      <CardHeading />
      {graceDeadline === null ? null : <MfaEnrollmentBanner deadline={graceDeadline} />}
      {errorMessage === null ? null : <ErrorBanner message={errorMessage} />}
      {signedIn ? (
        // The oracle renders the whole tab block inside the `else` of `if (_signedIn)`:
        // a sign-in form under a "you are now signed in" alert is an invitation to
        // do it again.
        <SignedInBanner />
      ) : (
        <TabStrip activeTab={activeTab} onSelect={handleSwitchTab} />
      )}
      {signedIn ? null : (
        <TabPanel
          activeTab={activeTab}
          magicLinkToken={magicLinkToken}
          returnUrl={returnUrl}
          clientId={clientId}
          onAuthResult={handleAuthResult}
          onError={setErrorMessage}
        />
      )}
      {/*
       * Wallow-vec7.3.14. OUTSIDE the tab chain but INSIDE the `signedIn` gate,
       * exactly as the oracle places it: "Or continue with" is an alternative to
       * all three tabs, but offering it under a "you are now signed in" alert
       * would invite the user to start over. It takes no `clientId` — the
       * `external-login` endpoint binds no such parameter; client_id rides inside
       * `returnUrl`. See the no-guard note in that file: this returnUrl is CARGO,
       * not a destination this screen picks, so `returnUrlIsSafe` is deliberately
       * not threaded into it.
       */}
      {signedIn ? null : <ExternalProviders returnUrl={returnUrl} />}
      <RegisterPrompt href={registerHref(clientId, returnUrl)} />
    </div>
  );
}

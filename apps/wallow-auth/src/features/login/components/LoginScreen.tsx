import { buildExchangeTicketUrl, isSafeReturnUrl } from "@bc-solutions-coder/sdk";
import { Card, ErrorBanner, MutedText, Tabs, Text } from "@bc-solutions-coder/ui";
import { useNavigate } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";

import {
  type AuthDisposition,
  authDispositionOf,
  errorParamMessage,
  isPasswordResetMessage,
} from "../auth-result";
import type { LoginTab } from "../panel";
import { ExternalProviders } from "./ExternalProviders";
import { MagicLinkLoginForm } from "./MagicLinkLoginForm";
import { OtpLoginForm } from "./OtpLoginForm";
import { PasswordLoginForm } from "./PasswordLoginForm";
import { BASE_PATH, toAppHref } from "@shared/lib/base-path";

/**
 * The Login screen (Wallow-vec7.3.11 / 2.8a).
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
 * exchange-ticket URL at L544-550 — is deliberately NOT ported. This app's API
 * surface (`src/lib/api-passthrough.ts`) is a passthrough reverse proxy mounting
 * `/v1/**` and `/connect/**` at the ROOT, so this origin hosts them (bd memory
 * `wallow-auth-screens-must-pass-origin-same-origin`). Prepending an absolute
 * origin would send the browser cross-origin and DROP the SameSite auth cookie
 * the exchange-ticket endpoint just set — which is the entire point of the ticket.
 *
 * ── NO cookieRelay (for `.3.15`) ─────────────────────────────────────────────
 *
 * The oracle's `BuildMfaRedirectUrl` threads
 * `AuthClient.GetPendingCookieRelayKey()`. That subsystem was deliberately deleted
 * in Wallow-vec7.1.3 and the facade has no such method. The passthrough proxy forwards
 * `Set-Cookie` verbatim, so the partial-auth cookie is already in the jar by the
 * time the MFA branch is taken — which is why the hand-off is `navigate()` (the
 * client router) and not a full page load. Do not re-add `cookieRelay`.
 */

/**
 * This app's own origin, plus the base path it is served under — see the
 * origin-divergence note above.
 */
const SAME_ORIGIN_BASE: string = BASE_PATH;

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
      <Text as="p" variant="bodySm" weight="medium">
        MFA enrollment required
      </Text>
      <MutedText>
        Your organization requires two-factor authentication. Please set it up before{" "}
        {formatGraceDeadline(deadline)}.
      </MutedText>
      <a className="inline-block text-sm font-medium text-primary" href={toAppHref("/mfa/enroll")}>
        Set up now
      </a>
    </div>
  );
}

/**
 * The success acknowledgment shown after a completed password reset
 * (Wallow-xzha.1.2). `ResetPasswordForm` navigates here with
 * `?message=password_reset`; unlike `SignedInBanner` this is INFORMATIONAL — the
 * user still has to sign in — so it sits ABOVE the tab strip and does NOT retire
 * it. Styled like `SignedInBanner` (border-success), per the DESIGN.
 */
function PasswordResetNotice() {
  return (
    <div
      className="rounded-md border border-success bg-success/10 p-3"
      data-testid="login-password-reset-notice"
    >
      <Text as="p" variant="bodySm">
        Your password has been reset. You can now sign in.
      </Text>
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
      <Text as="p" variant="bodySm">
        You are now signed in.
      </Text>
    </div>
  );
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1 text-center">
      <Text as="h2" variant="body" weight="semibold" color="onCard">
        Sign in to your account
      </Text>
      <MutedText>Enter your credentials to continue</MutedText>
    </div>
  );
}

/** The strip, in order, with the oracle's labels. */
const LOGIN_TABS: readonly { readonly tab: LoginTab; readonly label: string }[] = [
  { tab: "password", label: "Password" },
  { tab: "magic-link", label: "Magic Link" },
  { tab: "otp", label: "OTP" },
];

/**
 * Base UI types a tab's `value` as `any`, so the value coming back out of
 * `onValueChange` is narrowed here rather than asserted: the shell's `activeTab`
 * is a `LoginTab` and nothing else may set it.
 */
function isLoginTab(value: unknown): value is LoginTab {
  return LOGIN_TABS.some((entry) => entry.tab === value);
}

/**
 * The oracle's tab strip and its `else if` panel chain, on the catalog's `Tabs`
 * (Wallow-m5aq.5.2) — Base UI's Tabs, so the WAI-ARIA tab pattern arrives whole
 * rather than being re-derived here.
 *
 * What the hand-rolled version could not say, and this does: each panel is
 * `aria-labelledby` its tab and each tab `aria-controls` its panel, and the strip
 * carries ONE tab stop with the arrow keys moving inside it. Arrow keys move
 * focus WITHOUT activating (Base UI's `activateOnFocus` default) — a user
 * browsing the strip has not chosen yet, and switching tabs under them would
 * discard whatever they had typed in the panel below.
 *
 * `Tabs.Panel` deliberately takes NO `keepMounted`: one panel is in the DOM at a
 * time, as the `else if` chain did. A hidden second sign-in form is a second form
 * users can tab into. That unmount is also what resets each panel's local state
 * on a tab switch — `OtpLoginForm`'s `sent`/`code`/`rememberMe` and
 * `MagicLinkLoginForm`'s redemption latch all rely on it.
 *
 * `Tabs.Indicator` replaces the active tab's hand-rolled `border-b-2
 * border-primary`: the same rule, drawn once and slid, rather than a border
 * toggled on three buttons.
 */
function LoginTabs(props: {
  readonly activeTab: LoginTab;
  readonly magicLinkToken?: string;
  readonly returnUrl?: string;
  readonly clientId?: string;
  readonly onSelect: (tab: LoginTab) => void;
  readonly onAuthResult: (body: unknown) => void;
  readonly onError: (message: string | null) => void;
}) {
  const { activeTab, magicLinkToken, returnUrl, clientId, onSelect, onAuthResult, onError } = props;

  return (
    <Tabs.Root
      value={activeTab}
      onValueChange={(value: unknown) => {
        if (isLoginTab(value)) {
          onSelect(value);
        }
      }}
    >
      <Tabs.List>
        {LOGIN_TABS.map((entry) => (
          <Tabs.Tab
            key={entry.tab}
            value={entry.tab}
            className="flex-1"
            data-testid={`login-tab-${entry.tab}`}
          >
            {entry.label}
          </Tabs.Tab>
        ))}
        <Tabs.Indicator />
      </Tabs.List>
      <Tabs.Panel value="password">
        <PasswordLoginForm onAuthResult={onAuthResult} onError={onError} />
      </Tabs.Panel>
      <Tabs.Panel value="magic-link">
        {/*
         * `returnUrl`/`clientId` are cargo for the SEND (`SendMagicLinkRequest`
         * carries them so the emailed link can resume this OIDC flow) — NOT a
         * destination this panel navigates to. It never navigates; it reports up.
         * See `../panel`.
         */}
        <MagicLinkLoginForm
          token={magicLinkToken}
          returnUrl={returnUrl}
          clientId={clientId}
          onAuthResult={onAuthResult}
          onError={onError}
        />
      </Tabs.Panel>
      <Tabs.Panel value="otp">
        {/*
         * `OtpLoginForm` (Wallow-vec7.3.13) needed no new props to host, exactly as
         * `.3.11` predicted: `SendOtpRequest` is `{ email }` and `VerifyOtpRequest`
         * is `{ email, code, rememberMe? }`, so neither `returnUrl` nor `clientId`
         * is cargo this tab carries — both halves of it are driven entirely by what
         * the user types. Like the magic-link panel it never navigates: it reports
         * the RAW body up and the shell's one `authDispositionOf` decides.
         */}
        <OtpLoginForm onAuthResult={onAuthResult} onError={onError} />
      </Tabs.Panel>
    </Tabs.Root>
  );
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

  // Through `toAppHref` because this is rendered as a raw `<a href>` the router
  // never sees; the `returnUrl` cargo is left alone, since `/register` hands it
  // to `navigate()` and the router applies the base path itself.
  return toAppHref(params.length === 0 ? "/register" : `/register?${params.join("&")}`);
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
  /**
   * The `message` query param (Wallow-xzha.1.2). `ResetPasswordForm` navigates to
   * `/login?message=password_reset` after a successful reset; the screen renders a
   * one-line success banner acknowledging it when the value is the recognised
   * `password_reset` token (`isPasswordResetMessage`), and ignores anything else.
   */
  readonly message?: string;
}

export function LoginScreen({
  returnUrl,
  clientId,
  error,
  magicLinkToken,
  message,
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
    returnUrl !== undefined && returnUrl !== "" && isSafeReturnUrl(returnUrl);

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
        // A FULL navigation: the exchange endpoint is served by the passthrough reverse
        // proxy, not by the client-side route tree, which would 404 in-app.
        globalThis.location.href = buildExchangeTicketUrl(
          SAME_ORIGIN_BASE,
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
    <Card spacing="p-6 space-y-4">
      <CardHeading />
      {isPasswordResetMessage(message) ? <PasswordResetNotice /> : null}
      {graceDeadline === null ? null : <MfaEnrollmentBanner deadline={graceDeadline} />}
      {errorMessage === null ? null : (
        <ErrorBanner data-testid="login-error">{errorMessage}</ErrorBanner>
      )}
      {signedIn ? (
        // The oracle renders the whole tab block inside the `else` of `if (_signedIn)`:
        // a sign-in form under a "you are now signed in" alert is an invitation to
        // do it again. Strip and panels are ONE block now (the `Tabs.Root` that
        // pairs them), so the gate that used to be written twice is written once.
        <SignedInBanner />
      ) : (
        <LoginTabs
          activeTab={activeTab}
          magicLinkToken={magicLinkToken}
          returnUrl={returnUrl}
          clientId={clientId}
          onSelect={handleSwitchTab}
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
    </Card>
  );
}

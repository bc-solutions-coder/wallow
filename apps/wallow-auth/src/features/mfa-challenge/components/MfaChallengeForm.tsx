import { buildExchangeTicketUrl } from "@bc-solutions-coder/sdk";
import {
  Button,
  Card,
  ErrorBanner,
  Field,
  Input,
  Label,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useMutation } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";
import { accountVerifyMfaChallengeMutation } from "../api";
import { BASE_PATH, toAppHref } from "@shared/lib/base-path";
import { readErrorCode, readMember } from "@shared/lib/error-code";
import { useRedirectVerdict } from "../hooks/use-redirect-verdict";

/**
 * The MfaChallenge screen (Wallow-vec7.3.6).
 *
 * `returnUrl` arrives as a prop rather than being read from the router inside
 * the component: the route owns the query string (the oracle's single
 * `[SupplyParameterFromQuery]` property) and hands it down, which keeps this
 * component a pure function of its inputs and testable without a router — the
 * seam `ResetPasswordForm` established and `ConsentScreen` followed.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3). The
 * "Back to sign in" footer link ships without a testid in the oracle and keeps
 * it that way.
 *
 * Mutations call the GENERATED operations and reads use the generated
 * `{op}Options()` factories, both bound to the request-scoped SDK off the router
 * context (`useRouteContext({ from: "__root__" })`). The OIDC URL builders are
 * pure and imported directly. There is no app-level facade (Wallow-pu6a.5.5).
 *
 * ── THE ERROR BRANCHES ────────────────────────────────────────────────────────
 *
 * `AccountController.VerifyMfaChallenge` (api/.../Controllers/AccountController.cs:167-236)
 * fails in exactly three ways, each a non-2xx with a bare `{ succeeded, error }`
 * body (NOT problem details):
 *
 *     401 error "no_mfa_session"   partial-auth cookie missing or expired
 *     401 error "invalid_code"     no user / no TOTP secret / code rejected
 *     423 error "mfa_locked_out"   already locked, or locked by this attempt
 *
 * `unwrap()` throws on all three, and `toWallowError()` recovers the token: as of
 * Wallow-vec7.7 `readCode` probes `extensions.code > code > error`, so the `error`
 * member of that anon body reaches this screen as `WallowError.code`. (Before
 * that it did not, and this screen narrowed on HTTP status alone — which could
 * not tell `no_mfa_session` from `invalid_code`, since they share a 401.)
 *
 * The oracle's own switch is only partly worth porting:
 *
 *   - Its `"expired_challenge"` branch is DEAD CODE — this endpoint never emits
 *     that string. The expired-cookie case is `no_mfa_session`, which the oracle
 *     drops into its `_` tail. `SESSION_EXPIRED_MESSAGE` says what that dead
 *     branch was reaching for, keyed on the token the API actually sends.
 *   - The API's error tail can render `result.Error` RAW, exposing the literal
 *     "no_mfa_session". `code` is a machine token and is never rendered
 *     here: it is matched against KNOWN values, and anything else — including a
 *     401 carrying an unrecognised code — falls to the generic message rather
 *     than guessing.
 *
 * Narrowing is STRUCTURAL rather than `instanceof WallowError`, because that
 * class is exported from the SDK's `./server` entry and screens may not import
 * the SDK at all. A network-level rejection carries neither `code` nor `status`
 * and must fall through to the generic message rather than throw.
 *
 * ── THE ORIGIN DIVERGENCE (inherited from Wallow-vec7.3.4) ────────────────────
 *
 * The oracle prepends an absolute API origin (`Configuration["ApiBaseUrl"]`) to
 * BOTH navigation targets. That prepend is deliberately NOT ported: this app's API
 * surface (`src/shared/lib/api-passthrough.server.ts`) is a passthrough reverse proxy mounting
 * `/v1/**` and `/connect/**` at the ROOT, so this origin hosts them and the
 * origin argument is `""`. Going cross-origin would drop the `SameSite`
 * partial-auth cookie that `mfa/verify` reads and the exchange-ticket endpoint
 * upgrades — the round-trip this screen exists to prove.
 */

/**
 * This app's own origin, plus the base path it is served under — see the
 * origin-divergence note above.
 */
const SAME_ORIGIN_BASE: string = BASE_PATH;

/** The oracle's blank-input guards, mode-sensitive as the oracle's are. */
const BLANK_CODE_MESSAGE = "Please enter the verification code.";
const BLANK_BACKUP_CODE_MESSAGE = "Please enter a backup code.";

/** The oracle's `"invalid_code" =>` branch, both halves of it. */
const INVALID_CODE_MESSAGE = "Invalid verification code. Please try again.";
const INVALID_BACKUP_CODE_MESSAGE = "Invalid backup code. Please try again.";

/**
 * `no_mfa_session`: the challenge session is gone, so nothing the user types
 * here can work. The message is about the SESSION, not the input — telling a
 * user their valid code was rejected would send them round a loop that burns
 * their five attempts against a cookie that no longer exists.
 */
const SESSION_EXPIRED_MESSAGE = "Your verification session has expired. Please sign in again.";

/** `mfa_locked_out`: the oracle printed the raw token here. */
const LOCKED_OUT_MESSAGE =
  "Too many failed attempts. Your account is temporarily locked. Please try again later.";

/** The oracle's `_ =>` tail, minus its raw-string leak. */
const GENERIC_FAILURE_MESSAGE = "Verification failed. Please try again.";

/** The API's machine tokens for this endpoint. Matched against, never rendered. */
const INVALID_CODE = "invalid_code";
const NO_MFA_SESSION = "no_mfa_session";
const MFA_LOCKED_OUT = "mfa_locked_out";

/**
 * Retained as a status-level fallback alongside the `mfa_locked_out` token: 423
 * identifies this failure on its own, and the cost of missing it — a locked user
 * retyping codes that cannot work, re-locking themselves — is worth the extra rule.
 */
const LOCKED_OUT_STATUS = 423;

/** Map a rejection onto user-facing copy — see the error-branch note above. */
function verifyFailureMessage(cause: unknown, useBackupCode: boolean): string {
  const code: string | undefined = readErrorCode(cause);

  if (code === INVALID_CODE) {
    return useBackupCode ? INVALID_BACKUP_CODE_MESSAGE : INVALID_CODE_MESSAGE;
  }

  if (code === NO_MFA_SESSION) {
    return SESSION_EXPIRED_MESSAGE;
  }

  if (code === MFA_LOCKED_OUT || readMember(cause, "status") === LOCKED_OUT_STATUS) {
    return LOCKED_OUT_MESSAGE;
  }

  return GENERIC_FAILURE_MESSAGE;
}

/** What a verified challenge resolves to. Both fields are absent on a direct sign-in. */
interface VerifyResult {
  readonly signInTicket?: string;
}

/** The oracle's `BbCardHeader`, whose description branches on the mode. */
function CardHeading({ useBackupCode }: { readonly useBackupCode: boolean }) {
  return (
    <div className="space-y-1">
      <Text as="h2" variant="subheading" color="onCard">
        Two-factor authentication
      </Text>
      <MutedText>
        {useBackupCode
          ? "Enter one of your backup codes to continue."
          : "Enter the code from your authenticator app to continue."}
      </MutedText>
    </div>
  );
}

/** The oracle's success `BbAlert`, which replaces the form on `_verified`. */
function SuccessBanner() {
  return (
    <div
      className="rounded-md border border-success bg-success/10 p-3"
      data-testid="mfa-challenge-success"
    >
      <Text as="p" variant="bodySm">
        Verification successful. Redirecting...
      </Text>
    </div>
  );
}

/**
 * The single code field. The two testids are mutually exclusive branches of the
 * oracle's one `if (_useBackupCode)` — two visible code boxes would be a
 * genuinely confusing form.
 */
function CodeField(props: {
  readonly useBackupCode: boolean;
  readonly value: string;
  readonly onChange: (value: string) => void;
}) {
  const { useBackupCode, value, onChange } = props;

  return (
    <Field>
      <Label htmlFor="code">{useBackupCode ? "Backup code" : "Verification code"}</Label>
      <Input
        id="code"
        type="text"
        placeholder={useBackupCode ? "Enter backup code" : "Enter 6-digit code"}
        data-testid={useBackupCode ? "mfa-challenge-backup-code" : "mfa-challenge-code"}
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
    </Field>
  );
}

/** The oracle's toggle, which names the DESTINATION mode rather than the current one. */
function ToggleBackupCode(props: {
  readonly useBackupCode: boolean;
  readonly onToggle: () => void;
}) {
  const { useBackupCode, onToggle } = props;

  return (
    <div className="text-center">
      {/*
        `width="auto"` is mandatory here — the recipe's width defaults to `full`
        for the eleven call sites that want it, and this quiet toggle sitting
        under a full-width Verify must not become a second button competing
        with it. `link` is the variant that draws no box at all.
      */}
      <Button
        type="button"
        variant="link"
        width="auto"
        data-testid="mfa-challenge-toggle-backup"
        onClick={onToggle}
      >
        {useBackupCode ? "Use authenticator code instead" : "Use backup code instead"}
      </Button>
    </div>
  );
}

/** The oracle's `BbCardFooter` — the way out for a user whose session is gone. */
function BackToSignIn() {
  return (
    <div className="text-center w-full">
      <a href={toAppHref("/login")} className="text-sm text-muted-foreground hover:text-foreground">
        Back to sign in
      </a>
    </div>
  );
}

/** The oracle's `<form>`: the code field, the submit, and the mode toggle. */
function ChallengeFields(props: {
  readonly useBackupCode: boolean;
  readonly code: string;
  readonly pending: boolean;
  readonly onCodeChange: (value: string) => void;
  readonly onToggle: () => void;
  readonly onSubmit: () => void;
}) {
  const { useBackupCode, code, pending, onCodeChange, onToggle, onSubmit } = props;

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        onSubmit();
      }}
    >
      <CodeField useBackupCode={useBackupCode} value={code} onChange={onCodeChange} />
      <Button
        type="submit"
        // The oracle's `Disabled="_isSubmitting"` — one click, one attempt. This
        // screen is rate-limited into a 5-strike lockout, so a double submit can
        // cost the user two of their five.
        disabled={pending}
        data-testid="mfa-challenge-submit"
      >
        {pending ? "Verifying..." : "Verify"}
      </Button>
      <ToggleBackupCode useBackupCode={useBackupCode} onToggle={onToggle} />
    </form>
  );
}

export interface MfaChallengeFormProps {
  /** The `returnUrl` query parameter — `undefined` on a direct (non-OIDC) sign-in. */
  readonly returnUrl?: string;
  /**
   * The client that started the flow, from the `client_id` the external-login
   * callback redirects here with — `undefined` on the password path, which
   * carries none. It scopes BOTH of this screen's uses of the returnUrl: the
   * allow-list probe and the exchange-ticket hand-off.
   */
  readonly clientId?: string;
}

export function MfaChallengeForm({ returnUrl, clientId }: MfaChallengeFormProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });
  const [code, setCode] = useState("");
  const [useBackupCode, setUseBackupCode] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [verified, setVerified] = useState(false);

  // A present-but-blank `client_id` is not a client, and an unknown client fails
  // CLOSED to the AuthUrl-only origin set on both endpoints — relaying "" would
  // refuse the very returnUrl the user is mid-journey to. Normalized once here
  // so blank is absent at BOTH hops below.
  const scopedClientId: string | undefined =
    clientId === undefined || clientId.trim() === "" ? undefined : clientId;

  // The guard, evaluated before anything else happens. Feature-local, because
  // this is the app's one returnUrl that may be ABSOLUTE — see the hook.
  const guard = useRedirectVerdict(returnUrl, scopedClientId);

  // The generated factory's response type governs; {@link VerifyResult} stays as the
  // narrowing at the call site's `onSuccess`, because the schema declares
  // `signInTicket` required and a direct sign-in really does omit it.
  const mutation = useMutation(accountVerifyMfaChallengeMutation({ client: sdk.client }));

  const redirect = (ticket: string | undefined): void => {
    // Unsafe values were refused at mount, so a present returnUrl here is safe,
    // and safe implies non-empty.
    if (returnUrl === undefined || returnUrl === "") {
      // The oracle's trailing comment: "No ReturnUrl — direct login, not OIDC.
      // Show success state without redirecting." No "/" fallback.
      return;
    }

    // FULL navigations, not `navigate()`: both targets are served by the passthrough
    // reverse proxy, not by the client-side route tree, which would 404 in-app.
    //
    // `IsNullOrEmpty(result.SignInTicket)`: `buildExchangeTicketUrl` THROWS on a
    // blank ticket ("ticket is required", auth-oidc.ts:131), so calling it anyway
    // would replace the user's redirect with a crash.
    if (ticket === undefined || ticket === "") {
      // The oracle's `BuildApiReturnUrl`, whose `ApiBaseUrl` prepend is the
      // identity function once the origin is this one.
      globalThis.location.href = returnUrl;
      return;
    }

    // The allow-list verdict that admitted an absolute returnUrl at mount travels
    // WITH it rather than being re-derived here (Wallow-a6jr) — see the hook.
    const handOff = guard.handOff(returnUrl);

    // The id has to survive this last hop too: `ExchangeTicket` re-validates the
    // returnUrl before setting the cookie, and falls back to the union origin set
    // without one. Passed only when there IS one, so a flow that carries no client
    // asks the exact request it asks today.
    globalThis.location.href =
      scopedClientId === undefined
        ? buildExchangeTicketUrl(SAME_ORIGIN_BASE, ticket, handOff)
        : buildExchangeTicketUrl(SAME_ORIGIN_BASE, ticket, handOff, scopedClientId);
  };

  const handleToggle = (): void => {
    setUseBackupCode(!useBackupCode);
    // The oracle's `_code = string.Empty;` — a TOTP code left sitting in the
    // backup-code box would be submitted to the wrong branch and burn one of the
    // user's five attempts.
    setCode("");
    // The oracle's `_errorMessage = null;` — "Invalid verification code" hanging
    // over a freshly-opened backup-code box is a lie.
    setError(null);
  };

  const handleSubmit = (): void => {
    // The oracle's `if (string.IsNullOrWhiteSpace(_code))`. A blank submit cannot
    // succeed and costs a lockout attempt, so it never reaches `mfa/verify`.
    if (code.trim() === "") {
      setError(useBackupCode ? BLANK_BACKUP_CODE_MESSAGE : BLANK_CODE_MESSAGE);
      return;
    }

    // The oracle's `_errorMessage = null;` at the top of `HandleVerify`: a stale
    // "invalid code" banner above a successful verification would be a lie.
    setError(null);

    mutation.mutate(
      // The generated artifact's REQUEST object, not a bare body. The same API op
      // carries `useBackupCode: true/false` — crossing them would send a recovery
      // code to the TOTP validator.
      { body: { code, useBackupCode } },
      {
        // Resolution IS success: every failure this endpoint has is non-2xx, so
        // `unwrap()` has already thrown by the time this runs.
        onSuccess: (result: VerifyResult) => {
          setVerified(true);
          redirect(result.signInTicket);
        },
        onError: (cause: unknown) => {
          // The form deliberately stays up: the user has attempts left and no way
          // to spend them if it is gone.
          setError(verifyFailureMessage(cause, useBackupCode));
        },
      },
    );
  };

  if (guard.verdict !== "accept") {
    // "refuse": the guard is navigating away. "pending": the allow-list has
    // not answered yet. Rendering the form in either state would invite the user to
    // produce a second factor for a destination that is refused or undecided — and
    // a form retracted late is a form a fast user has already submitted.
    return null;
  }

  return (
    <Card>
      <CardHeading useBackupCode={useBackupCode} />
      {error === null ? null : <ErrorBanner data-testid="mfa-challenge-error">{error}</ErrorBanner>}
      {verified ? (
        <SuccessBanner />
      ) : (
        <ChallengeFields
          useBackupCode={useBackupCode}
          code={code}
          pending={mutation.isPending}
          onCodeChange={setCode}
          onToggle={handleToggle}
          onSubmit={handleSubmit}
        />
      )}
      <BackToSignIn />
    </Card>
  );
}

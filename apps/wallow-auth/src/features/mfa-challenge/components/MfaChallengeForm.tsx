import { Button, Card, CardTitle, ErrorBanner, Field, Input, Label } from "@bc-solutions-coder/ui";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { type ReactNode, useEffect, useState } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";

/**
 * The MfaChallenge screen (Wallow-vec7.3.6), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/MfaChallenge.razor`.
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
 * The API is reached through `getWallowAuthSdk()`, never `@bc-solutions-coder/sdk`
 * directly — that facade is this app's only permitted importer of the SDK.
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
 *   - Its `_` tail renders `result.Error` RAW, so a Blazor user can be shown the
 *     literal "no_mfa_session". `code` is a machine token and is never rendered
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
 * BOTH navigation targets. That prepend is deliberately NOT ported: this app's h3
 * server (`src/lib/auth-server.ts`) is a passthrough reverse proxy mounting
 * `/v1/**` and `/connect/**` at the ROOT, so this origin hosts them and the
 * origin argument is `""`. Going cross-origin would drop the `SameSite`
 * partial-auth cookie that `mfa/verify` reads and the exchange-ticket endpoint
 * upgrades — the round-trip this screen exists to prove.
 */

/** This app's own origin — see the origin-divergence note above. */
const SAME_ORIGIN = "";

/** The bail target for an unsafe returnUrl, matching the ConsentScreen port. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

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

/**
 * The `{ allowed }` narrowing for `auth.validateRedirectUri`, owned at this
 * boundary exactly as the LogoutScreen port owns its own (LogoutScreen.tsx:102).
 *
 * The facade types the call `Promise<unknown>` (auth-client.ts:164) because the
 * endpoint returns an anonymous `Ok(new { allowed = … })` (AccountController.cs:
 * 601-612) that the OpenAPI spec declares with no schema. The comparison is
 * STRICT, mirroring the C# `body?.Allowed == true`: anything that is not literally
 * `allowed: true` — a missing key, the STRING "true", a non-object body — is NOT
 * allowed. Leaning on JS truthiness instead would admit `allowed: "false"`.
 */
function isRedirectUriAllowed(body: unknown): boolean {
  if (typeof body !== "object" || body === null || !("allowed" in body)) {
    return false;
  }

  return body.allowed === true;
}

/**
 * What can be settled about `returnUrl` WITHOUT a network call, and the one case
 * that cannot be ("ask" — an absolute URL, where only the server's allow-list
 * knows).
 */
type LocalDecision = "accept" | "refuse" | "ask";

/** The mount guard's answer. "pending" is its own state: see `verdictOf`. */
type ReturnUrlVerdict = "accept" | "refuse" | "pending";

/**
 * The half of the guard that needs no network (Wallow-vec7.3.17).
 *
 * `isRelativeSafe` is `isSafeReturnUrl`'s answer, which proves a value can only
 * resolve against THIS origin. It is passed in already computed rather than as a
 * callback, so the SDK facade's method is never called unbound.
 */
function localDecisionOf(returnUrl: string | undefined, isRelativeSafe: boolean): LocalDecision {
  if (returnUrl === undefined) {
    // The oracle's ordinary direct (non-OIDC) sign-in. No destination to decide;
    // routing it to /error would break every direct login.
    return "accept";
  }

  if (isRelativeSafe) {
    // The password path (`Login.razor`:509 -> `BuildMfaRedirectUrl` threads the
    // relative OIDC returnUrl). The common case, decided for free.
    return "accept";
  }

  if (returnUrl === "") {
    // `IsNullOrEmpty` parity: `?returnUrl=` is a PRESENT value that fails
    // `IsNullOrWhiteSpace`, so it is the unsafe case, not the nullish one. A
    // malformed link is not a destination worth asking the server about — the
    // LogoutScreen's `hasRedirectUri` short-circuit (LogoutScreen.tsx:219-221).
    return "refuse";
  }

  // Absolute: either the external-login hand-off's allow-listed returnUrl or an
  // attack, and `isSafeReturnUrl` is false for BOTH. Only the allow-list can tell.
  return "ask";
}

/**
 * FAIL CLOSED, in every direction.
 *
 * A rejection (the facade's `unwrap()` throws on non-2xx — the C#
 * `!IsSuccessStatusCode -> false` arm) leaves `allowed` undefined, and an
 * unreachable validator must never become a reason to TRUST a URI. In flight it is
 * undefined too, which is why "pending" is a verdict of its own rather than
 * collapsing into "accept": the caller renders nothing until the answer lands.
 */
function verdictOf(
  local: LocalDecision,
  allowListPending: boolean,
  allowed: boolean | undefined,
): ReturnUrlVerdict {
  if (local !== "ask") {
    return local;
  }

  if (allowListPending) {
    return "pending";
  }

  return allowed === true ? "accept" : "refuse";
}

/** Read a member off an unknown rejection without asserting its shape. */
function readMember(cause: unknown, name: string): unknown {
  if (typeof cause !== "object" || cause === null || !(name in cause)) {
    return undefined;
  }

  return (cause as Record<string, unknown>)[name];
}

/** Map a rejection onto user-facing copy — see the error-branch note above. */
function verifyFailureMessage(cause: unknown, useBackupCode: boolean): string {
  const code: unknown = readMember(cause, "code");

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
      <CardTitle>Two-factor authentication</CardTitle>
      <p className="text-sm text-muted-foreground">
        {useBackupCode
          ? "Enter one of your backup codes to continue."
          : "Enter the code from your authenticator app to continue."}
      </p>
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
      <p className="text-sm text-foreground">Verification successful. Redirecting...</p>
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
      <button
        type="button"
        className="text-sm text-muted-foreground hover:text-foreground"
        data-testid="mfa-challenge-toggle-backup"
        onClick={onToggle}
      >
        {useBackupCode ? "Use authenticator code instead" : "Use backup code instead"}
      </button>
    </div>
  );
}

/** The oracle's `BbCardFooter` — the way out for a user whose session is gone. */
function BackToSignIn() {
  return (
    <div className="text-center w-full">
      <a href="/login" className="text-sm text-muted-foreground hover:text-foreground">
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
}

export function MfaChallengeForm({ returnUrl }: MfaChallengeFormProps): ReactNode {
  const navigate = useNavigate();
  const [code, setCode] = useState("");
  const [useBackupCode, setUseBackupCode] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [verified, setVerified] = useState(false);

  // The guard, evaluated before anything else happens — see `localDecisionOf`.
  const local: LocalDecision = localDecisionOf(
    returnUrl,
    returnUrl !== undefined && getWallowAuthSdk().oidc.isSafeReturnUrl(returnUrl),
  );

  const validation = useQuery({
    queryKey: ["mfa-challenge-return-url", returnUrl],
    queryFn: async (): Promise<boolean> => {
      if (returnUrl === undefined) {
        // Unreachable: `enabled` gates this on `local === "ask"`, and a nullish
        // returnUrl is decided "accept". Present only to narrow the prop to the
        // `string` the call takes, without a cast.
        return false;
      }

      return isRedirectUriAllowed(await getWallowAuthSdk().auth.validateRedirectUri(returnUrl));
    },
    // The ONLY case that costs a request: an absolute returnUrl. The password path
    // and the direct sign-in are already decided, and must not pay for a probe
    // that would sit between the user and their code field.
    enabled: local === "ask",
    // A URI the allow-list refuses will not be on it a second later, and the
    // refusal arm is already the safe one; retrying only delays the answer.
    retry: false,
  });

  const returnUrlVerdict: ReturnUrlVerdict = verdictOf(
    local,
    validation.isPending,
    validation.data,
  );

  // REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`);
  // the oracle instead nulls an unsafe returnUrl and shows a bare success,
  // silently swallowing the open-redirect attempt. Refused as soon as the verdict
  // lands, following the ConsentScreen port and `Login.razor` L533-540: do not
  // make a user burn a one-time second factor on a destination already decided
  // against.
  useEffect(() => {
    if (returnUrlVerdict === "refuse") {
      void navigate({ href: ERROR_HREF });
    }
  }, [returnUrlVerdict, navigate]);

  const mutation = useMutation({
    mutationFn: async (attempt: { readonly code: string; readonly useBackupCode: boolean }) => {
      const auth = getWallowAuthSdk().auth;

      // The same API op with `useBackupCode: true/false` — crossing them would
      // send a recovery code to the TOTP validator.
      return attempt.useBackupCode
        ? ((await auth.useBackupCode(attempt.code)) as VerifyResult)
        : ((await auth.verifyMfa(attempt.code)) as VerifyResult);
    },
  });

  const redirect = (ticket: string | undefined): void => {
    // Unsafe values were refused at mount, so a present returnUrl here is safe,
    // and safe implies non-empty.
    if (returnUrl === undefined || returnUrl === "") {
      // The oracle's trailing comment: "No ReturnUrl — direct login, not OIDC.
      // Show success state without redirecting." No "/" fallback.
      return;
    }

    // FULL navigations, not `navigate()`: both targets are served by the h3
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

    globalThis.location.href = getWallowAuthSdk().oidc.buildExchangeTicketUrl(
      SAME_ORIGIN,
      ticket,
      returnUrl,
    );
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
      { code, useBackupCode },
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

  if (returnUrlVerdict !== "accept") {
    // "refuse": the effect above is navigating away. "pending": the allow-list has
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

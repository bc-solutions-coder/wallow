import { Button, Card, CardTitle, ErrorBanner, Field, Input, Label } from "@bc-solutions-coder/ui";
import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { QRCodeSVG } from "qrcode.react";
import { type ReactNode, useCallback, useEffect, useRef, useState } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";

/**
 * The MfaEnroll screen (Wallow-vec7.3.7), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/MfaEnroll.razor`.
 *
 * Both query parameters arrive as props rather than being read from the router
 * inside the component: the route owns the query string (the oracle's two
 * `[SupplyParameterFromQuery]` properties) and hands them down, which keeps this
 * component a pure function of its inputs and testable without a router — the
 * seam `ResetPasswordForm` established and `ConsentScreen`/`MfaChallengeForm`
 * followed.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3), with
 * ONE exception: `mfa-enroll-done`. The oracle's Done button carries no testid,
 * but the success hand-off — the whole point of the returnUrl thread — is
 * untestable without one, so it is invented under the `{page}-{element}`
 * kebab-case rule (`.claude/rules/E2E.md`).
 *
 * The API is reached through `getWallowAuthSdk()`, never `@bc-solutions-coder/sdk`
 * directly — that facade is this app's only permitted importer of the SDK.
 *
 * ── THE RELAY IS GONE (this screen's whole reason to exist) ───────────────────
 *
 * The oracle enrolls during PRERENDER (the only place `HttpContext` supplies the
 * partial-auth cookie) and smuggles the secret, the qr uri AND THE RAW COOKIE
 * HEADER through `PersistentComponentState` so the interactive circuit — where
 * `HttpContext` is null — can restore them and re-inject the cookie on the
 * confirm call (`PersistedEnrollment`, `ApiCookieJar`, `SeedFromBrowserCookies`).
 *
 * None of that is ported. This app's h3 server (`src/lib/auth-server.ts`) is a
 * passthrough reverse proxy and the client sends `credentials: "include"`, so the
 * `Identity.MfaPartial` cookie rides ordinary same-origin requests: `enrollTotp()`
 * takes no arguments and `confirmEnrollment` receives only `{ secret, code }`,
 * because there is no longer anything to thread alongside them.
 *
 * `TryTakeFromJson<PersistedEnrollment>` also existed to stop the circuit
 * re-calling `enroll/totp`, which would mint a SECOND secret and silently
 * invalidate the QR the user had already scanned. With no prerender/circuit split
 * there is one pass and one call, and `startedRef` keeps it that way.
 *
 * ── THE ERROR BRANCHES ────────────────────────────────────────────────────────
 *
 * `MfaController` (api/.../Controllers/MfaController.cs:57-120) fails only with
 * non-2xx bodies of the bare `{ succeeded: false, error }` shape (NOT problem
 * details):
 *
 *   enroll/totp     401 "no_auth_session"
 *   enroll/confirm  401 "no_auth_session" | 400 "invalid_code"
 *                 | 400 "user_not_found"  | 400 "update_failed"
 *   enroll/exchange-token  400 "invalid_or_expired_token"
 *
 * Every failure is non-2xx, so `unwrap()` throws and a `succeeded: false` body
 * NEVER arrives as data — the oracle's `if (result.Succeeded) … else switch` is
 * unreachable through this seam, and a RESOLVED `confirmEnrollment` always means
 * success. The `else` branch is therefore not ported; the switch moves to the
 * rejection path instead.
 *
 * As of Wallow-vec7.7 the token survives: `toWallowError()`'s `readCode` probes
 * `extensions.code > code > error`, so the `error` member of that anon body
 * reaches this screen as `WallowError.code`. HTTP status is kept as a FALLBACK
 * because `code` is not a guaranteed-stable token (bd memory
 * `code-keyed-error-mapping-needs-an-unrecognised-code-test-to-bind`), and here
 * the statuses are unambiguous enough to carry it: `enroll/confirm` has exactly
 * one 401 (`no_auth_session`) and `invalid_code` is the dominant 400.
 *
 * Both wart and divergence:
 *
 *   - The oracle's `_` tail renders `result.Error` RAW, so a Blazor user can be
 *     shown the literal "update_failed". `code` is a machine token and is never
 *     rendered here: it is matched against KNOWN values and anything else falls
 *     to the generic message rather than guessing.
 *   - `no_auth_session` gets a SIGN-IN message, not the oracle's "try again".
 *     That tail loops the user forever — no number of retries mints a cookie.
 *   - `user_not_found`/`update_failed` get the generic message rather than the
 *     status-fallback's "invalid code": telling a user whose WRITE failed to
 *     retype a correct code is the same infinite loop in miniature.
 *
 * Narrowing is STRUCTURAL rather than `instanceof WallowError`, because that
 * class is exported from the SDK's `./server` entry and screens may not import
 * the SDK at all. A network-level rejection carries neither `code` nor `status`
 * and must fall through to the generic message rather than throw.
 *
 * ── THE QR CODE ───────────────────────────────────────────────────────────────
 *
 * `qrUri` is an `otpauth://` URI (`MfaService.cs:38`), NOT a renderable image, so
 * the QR has to be drawn client-side. The oracle called
 * `JS.InvokeVoidAsync("qrcode.generate", …)` — but NO `qrcode.js` exists anywhere
 * in `Wallow.Auth` (wwwroot ships three files; `App.razor` loads only
 * `blazor.web.js`), so that interop ALWAYS throws and the `catch (JSException)`
 * always swallows it. The oracle's QR div renders empty every time; the shipped
 * screen is manual-entry-only. This port draws the QR for real.
 *
 * `data-qr-uri` carries the uri the API minted, which is what pins that the QR
 * encodes the API's value rather than one this screen reassembled from the
 * secret. A missing `qrUri` degrades to manual entry rather than blanking the
 * form — the oracle's "QR display is optional; secret text suffices" comment,
 * kept.
 *
 * ── THE ORIGIN DIVERGENCE (inherited from Wallow-vec7.3.4) ────────────────────
 *
 * The oracle's `BuildApiReturnUrl` prepends an absolute API origin
 * (`Configuration["ApiBaseUrl"]`) to the Done target. NOT ported (bd memory
 * `wallow-auth-same-origin-baseurl-apps-wallow-auth`): the proxy serves
 * `/connect/**` from THIS origin, and going cross-origin would drop the cookie
 * `enroll/confirm` just upgraded to full auth (`UpgradeToFullAuthAsync`,
 * MfaController:113-117) — the exact round-trip in this bead's acceptance.
 */

/** The bail target for an unsafe returnUrl, matching the ConsentScreen port. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** The oracle's `Sanitize(null)` fallback for a user who arrived without one. */
const HOME_HREF = "/";

/** The oracle's `IsNullOrWhiteSpace(_code)` guard. */
const BLANK_CODE_MESSAGE = "Please enter the verification code.";

/** The oracle's `HandleStartEnroll` failure copy. */
const START_FAILED_MESSAGE = "Failed to start MFA enrollment. Please try again.";

/** The oracle's `"invalid_code" =>` branch. */
const INVALID_CODE_MESSAGE = "Invalid verification code. Please try again.";

/** The oracle's `_ =>` tail, minus its raw-string leak. */
const CONFIRM_FAILED_MESSAGE = "Failed to confirm MFA enrollment. Please try again.";

/**
 * `no_auth_session`: the enrollment session is gone, so nothing on this screen
 * can work. The message is about the SESSION rather than the input, because
 * retrying cannot mint a cookie — the oracle's "try again" tail loops forever.
 */
const NO_SESSION_MESSAGE = "Your enrollment session has expired. Please sign in again.";

/**
 * `invalid_or_expired_token`: the settings hand-off token lives 60 seconds
 * (`_enrollmentTokenLifetime`), which is easy to miss. Naming the LINK is what
 * separates this from a generic failure — the user's fix is to start setup again
 * from the app that sent them, not to retry here.
 */
const EXPIRED_TOKEN_MESSAGE =
  "This enrollment link has expired. Please start setup again from your account settings.";

/** The API's machine tokens for these endpoints. Matched against, never rendered. */
const NO_AUTH_SESSION = "no_auth_session";
const INVALID_CODE = "invalid_code";
const USER_NOT_FOUND = "user_not_found";
const UPDATE_FAILED = "update_failed";
const INVALID_OR_EXPIRED_TOKEN = "invalid_or_expired_token";

/**
 * Status fallbacks, for when `code` is absent or unrecognised. `enroll/confirm`
 * emits exactly one 401 (`no_auth_session`, from `ResolveEnrollmentUserIdAsync`
 * returning null), and `invalid_code` is by far the dominant 400 — the other two
 * need a race to reach.
 */
const UNAUTHORIZED_STATUS = 401;
const BAD_REQUEST_STATUS = 400;

/** Read a member off an unknown rejection without asserting its shape. */
function readMember(cause: unknown, name: string): unknown {
  if (typeof cause !== "object" || cause === null || !(name in cause)) {
    return undefined;
  }

  return (cause as Record<string, unknown>)[name];
}

/** Map an `enroll/totp` rejection onto user-facing copy. */
function startFailureMessage(cause: unknown): string {
  const code: unknown = readMember(cause, "code");

  if (code === NO_AUTH_SESSION || readMember(cause, "status") === UNAUTHORIZED_STATUS) {
    return NO_SESSION_MESSAGE;
  }

  return START_FAILED_MESSAGE;
}

/** Map an `enroll/exchange-token` rejection onto user-facing copy. */
function exchangeFailureMessage(cause: unknown): string {
  if (readMember(cause, "code") === INVALID_OR_EXPIRED_TOKEN) {
    return EXPIRED_TOKEN_MESSAGE;
  }

  // A 400 is the ONLY failure this endpoint has, so an unrecognised code with one
  // is still the expired-token case; anything else is a genuine unknown.
  if (readMember(cause, "status") === BAD_REQUEST_STATUS) {
    return EXPIRED_TOKEN_MESSAGE;
  }

  return START_FAILED_MESSAGE;
}

/** Map an `enroll/confirm` rejection onto user-facing copy — see the note above. */
function confirmFailureMessage(cause: unknown): string {
  const code: unknown = readMember(cause, "code");

  if (code === INVALID_CODE) {
    return INVALID_CODE_MESSAGE;
  }

  if (code === NO_AUTH_SESSION) {
    return NO_SESSION_MESSAGE;
  }

  // The should-never-happen writes. Both are 400s, so WITHOUT the token they
  // would fall to the status rule below and wrongly blame the user's code.
  if (code === USER_NOT_FOUND || code === UPDATE_FAILED) {
    return CONFIRM_FAILED_MESSAGE;
  }

  const status: unknown = readMember(cause, "status");

  if (status === UNAUTHORIZED_STATUS) {
    return NO_SESSION_MESSAGE;
  }

  if (status === BAD_REQUEST_STATUS) {
    return INVALID_CODE_MESSAGE;
  }

  return CONFIRM_FAILED_MESSAGE;
}

/** What `enroll/totp` resolves to. */
interface EnrollResult {
  readonly secret?: string;
  readonly qrUri?: string | null;
}

/** What `enroll/confirm` resolves to. `backupCodes` is absent on the oracle's `?? Array.Empty` path. */
interface ConfirmResult {
  readonly backupCodes?: readonly string[];
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1 text-center">
      <CardTitle>Set up two-factor authentication</CardTitle>
      <p className="text-sm text-muted-foreground">
        Scan the QR code with your authenticator app, then enter the code to confirm.
      </p>
    </div>
  );
}

/**
 * The QR itself. `data-qr-uri` is the assertable seam for "this encodes the uri
 * the API minted" — see the QR note above.
 */
function QrPanel({ qrUri }: { readonly qrUri: string }) {
  return (
    <div className="flex justify-center" data-testid="mfa-enroll-qr" data-qr-uri={qrUri}>
      <QRCodeSVG value={qrUri} size={180} marginSize={2} />
    </div>
  );
}

/** The oracle's QR block and its manual-entry fallback. */
function SecretPanel({
  qrUri,
  secret,
}: {
  readonly qrUri: string | null;
  readonly secret: string;
}) {
  return (
    <div className="text-center space-y-2">
      <p className="text-sm text-muted-foreground">
        Scan this QR code with your authenticator app:
      </p>
      {qrUri === null ? null : <QrPanel qrUri={qrUri} />}
      <p className="text-xs text-muted-foreground">Or enter this secret manually:</p>
      <div className="bg-muted rounded-md p-2 font-mono text-sm" data-testid="mfa-enroll-secret">
        {secret}
      </div>
    </div>
  );
}

/** The oracle's `<form>`: the code field and the submit. */
function ConfirmFields(props: {
  readonly code: string;
  readonly pending: boolean;
  readonly onCodeChange: (value: string) => void;
  readonly onSubmit: () => void;
}) {
  const { code, pending, onCodeChange, onSubmit } = props;

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        onSubmit();
      }}
    >
      <Field>
        <Label htmlFor="code">Verification code</Label>
        <Input
          id="code"
          type="text"
          placeholder="Enter 6-digit code"
          data-testid="mfa-enroll-code"
          value={code}
          onChange={(e) => {
            onCodeChange(e.target.value);
          }}
        />
      </Field>
      <Button
        type="submit"
        // The oracle's `Loading`/`Disabled="_isSubmitting"`. A double submit burns
        // the 30-second TOTP window and mints a second set of backup codes over
        // the ones the user is mid-way through writing down.
        disabled={pending}
        data-testid="mfa-enroll-submit"
      >
        {pending ? "Verifying..." : "Verify and enable MFA"}
      </Button>
    </form>
  );
}

/** The oracle's success `BbAlert` plus its backup-code list. */
function BackupCodesPanel({ codes }: { readonly codes: readonly string[] }) {
  return (
    <>
      <div className="rounded-md border border-success bg-success/10 p-3">
        <p className="text-sm font-medium text-foreground">MFA enabled successfully</p>
        <p className="text-sm text-muted-foreground">
          Save these backup codes in a safe place. They can be used to access your account if you
          lose your authenticator device.
        </p>
      </div>
      <div
        className="bg-muted rounded-md p-4 font-mono text-sm space-y-1"
        data-testid="mfa-enroll-backup-codes"
      >
        {codes.map((backupCode) => (
          <div key={backupCode}>{backupCode}</div>
        ))}
      </div>
    </>
  );
}

/** The oracle's "what this is and what you'll need" copy. */
function IntroCopy() {
  return (
    <div className="text-sm text-muted-foreground space-y-2">
      <p>
        Two-factor authentication adds an extra layer of security to your account by requiring a
        code from an authenticator app when you sign in.
      </p>
      <p className="font-semibold text-foreground">You will need:</p>
      <ul className="list-disc list-inside space-y-1">
        <IntroRequirements />
      </ul>
    </div>
  );
}

/** Split out purely to keep the list items within the `jsx-max-depth` budget. */
function IntroRequirements() {
  return (
    <>
      <li>An authenticator app (Google Authenticator, Authy, 1Password, etc.)</li>
      <li>Access to your device during sign-in</li>
    </>
  );
}

/**
 * The oracle's intro branch. NOT a first screen: `OnInitializedAsync` fires
 * `HandleStartEnroll()` unconditionally, so `_secret` being null by the time this
 * renders means enrollment has already FAILED. `mfa-enroll-begin-setup` is a
 * RETRY in all but name, and its presence in the oracle is not evidence of a
 * happy-path gate — adding one would be invention.
 */
function IntroPanel({
  loading,
  onBeginSetup,
}: {
  readonly loading: boolean;
  readonly onBeginSetup: () => void;
}) {
  return (
    <div className="space-y-4">
      <IntroCopy />
      {loading ? (
        <Button type="button" disabled>
          Preparing setup…
        </Button>
      ) : (
        <Button type="button" data-testid="mfa-enroll-begin-setup" onClick={onBeginSetup}>
          Begin setup
        </Button>
      )}
    </div>
  );
}

/** The oracle's `BbCardFooter` — the way out for a user who opened this by mistake. */
function CancelLink() {
  return (
    <div className="text-center w-full">
      <a
        href={HOME_HREF}
        className="text-sm text-muted-foreground hover:text-primary"
        data-testid="mfa-enroll-cancel"
      >
        Cancel
      </a>
    </div>
  );
}

export interface MfaEnrollFormProps {
  /** The `returnUrl` query parameter — `undefined` on a direct (non-OIDC) enrollment. */
  readonly returnUrl?: string;
  /**
   * The `enrollToken` query parameter — present only on the settings-triggered
   * flow, where the Web app hands over a short-lived token that is exchanged for
   * an `Identity.MfaPartial` cookie before enrollment can authenticate.
   */
  readonly enrollToken?: string;
}

export function MfaEnrollForm({ returnUrl, enrollToken }: MfaEnrollFormProps): ReactNode {
  const navigate = useNavigate();
  const [secret, setSecret] = useState<string | null>(null);
  const [qrUri, setQrUri] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [backupCodes, setBackupCodes] = useState<readonly string[] | null>(null);

  // The guard, evaluated before anything else happens. A NULLISH returnUrl is not
  // hostile — it is the oracle's ordinary direct-enrollment path — so only a
  // PRESENT value is checked. An empty string IS present, and `isSafeReturnUrl("")`
  // is false, so it is the unsafe case rather than the nullish no-redirect one.
  const returnUrlIsUnsafe: boolean =
    returnUrl !== undefined && !getWallowAuthSdk().oidc.isSafeReturnUrl(returnUrl);

  /**
   * The oracle's `TryTakeFromJson` suppression, kept as a ref: enrollment must
   * fire exactly once per mount, and a second `enroll/totp` would mint a second
   * secret and invalidate the QR the user already scanned. A ref rather than
   * state because the mount effect must not re-run when it flips.
   */
  const startedRef = useRef(false);

  /** The oracle's `HandleStartEnroll`. Shared by the mount effect and the retry. */
  const startEnroll = useCallback(async (): Promise<void> => {
    // The oracle's `_errorMessage = null;` — a stale error sitting above a
    // freshly-minted QR code is a lie.
    setErrorMessage(null);
    setLoading(true);

    try {
      const result = (await getWallowAuthSdk().auth.enrollTotp()) as EnrollResult;

      setSecret(result.secret ?? null);
      setQrUri(result.qrUri ?? null);
    } catch (error: unknown) {
      setErrorMessage(startFailureMessage(error));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // Refused destinations never enroll: do not make a user set up a second factor
    // for somewhere already decided against.
    if (returnUrlIsUnsafe) {
      void navigate({ href: ERROR_HREF });
      return;
    }

    if (startedRef.current) {
      return;
    }
    startedRef.current = true;

    void (async () => {
      // The oracle's `if (!string.IsNullOrEmpty(EnrollToken)) await
      // ExchangeEnrollmentTokenAsync(EnrollToken); await HandleStartEnroll();`
      //
      // Order is the whole contract: the exchange is what mints the
      // `Identity.MfaPartial` cookie, and an `enroll/totp` fired first has no
      // session to resolve and 401s — blaming the wrong thing.
      if (enrollToken !== undefined && enrollToken !== "") {
        setLoading(true);
        try {
          await getWallowAuthSdk().auth.exchangeEnrollmentToken(enrollToken);
        } catch (error: unknown) {
          // Enrolling anyway would just 401 and report a session problem when the
          // real fault is the expired link.
          setErrorMessage(exchangeFailureMessage(error));
          setLoading(false);
          return;
        }
      }

      await startEnroll();
    })();
  }, [returnUrlIsUnsafe, navigate, enrollToken, startEnroll]);

  const mutation = useMutation({
    mutationFn: async (attempt: { readonly secret: string; readonly code: string }) =>
      (await getWallowAuthSdk().auth.confirmEnrollment({
        secret: attempt.secret,
        code: attempt.code,
      })) as ConfirmResult,
  });

  const handleDone = (): void => {
    // Unsafe values were refused at mount, so a present returnUrl here is safe.
    // A FULL navigation, not `navigate()`: `/connect/**` is served by the h3
    // reverse proxy, not by the client-side route tree, which would 404 in-app.
    // No origin prepend — see the origin-divergence note above.
    globalThis.location.href = returnUrl ?? HOME_HREF;
  };

  const handleSubmit = (): void => {
    // The oracle's `if (string.IsNullOrWhiteSpace(_code))`, which guards ahead of
    // the call — a blank submit cannot succeed.
    if (code.trim() === "") {
      setErrorMessage(BLANK_CODE_MESSAGE);
      return;
    }

    if (secret === null) {
      return;
    }

    // The oracle's `_errorMessage = null;` at the top of `HandleConfirm`.
    setErrorMessage(null);

    mutation.mutate(
      // The secret MUST be the one `enroll/totp` minted — the server re-validates
      // the TOTP against the secret in the body before storing it. Nothing else
      // rides along: the oracle needed a cookie header here, and this does not.
      { secret, code },
      {
        // Resolution IS success: every failure this endpoint has is non-2xx, so
        // `unwrap()` has already thrown by the time this runs.
        onSuccess: (result: ConfirmResult) => {
          // The oracle's `result.BackupCodes ?? Array.Empty<string>()`. An empty
          // list is still a successful enrollment and MFA really is on; falling
          // back to the error state would tell the user a lie about their account.
          setBackupCodes(result.backupCodes ?? []);
        },
        onError: (cause: unknown) => {
          // The form deliberately stays up, and the SECRET is deliberately kept:
          // the TOTP window rolls every 30 seconds, so a stale code is the common
          // cause, and re-enrolling behind the user's back would invalidate the
          // authenticator entry they just made.
          setErrorMessage(confirmFailureMessage(cause));
          // The CODE, by contrast, is cleared — a DIVERGENCE from the oracle,
          // which leaves `_code` sitting in the box. A rejected TOTP is single-use
          // and time-boxed: it is guaranteed dead, so keeping it makes the user
          // select-all-delete before every retry and invites a resubmit of the
          // identical failing value. Same reasoning as the sibling
          // `MfaChallengeForm`'s clear-on-toggle.
          setCode("");
        },
      },
    );
  };

  if (returnUrlIsUnsafe) {
    // The effect above is navigating away; rendering the form would invite the
    // user to produce a second factor for a destination already refused.
    return null;
  }

  return (
    <Card>
      <CardHeading />
      {errorMessage === null ? null : (
        <ErrorBanner data-testid="mfa-enroll-error">{errorMessage}</ErrorBanner>
      )}
      {renderBody({
        backupCodes,
        secret,
        qrUri,
        code,
        loading,
        pending: mutation.isPending,
        onCodeChange: setCode,
        onSubmit: handleSubmit,
        onDone: handleDone,
        onBeginSetup: () => {
          void startEnroll();
        },
      })}
      <CancelLink />
    </Card>
  );
}

/**
 * The oracle's three-way render branch, in its order:
 * `_backupCodes is not null` > `_secret` > the intro. Success winning over the
 * secret is what retires the code form — leaving a live code box under the
 * success state invites a second submit that regenerates the very codes the user
 * is copying down.
 */
function renderBody(props: {
  readonly backupCodes: readonly string[] | null;
  readonly secret: string | null;
  readonly qrUri: string | null;
  readonly code: string;
  readonly loading: boolean;
  readonly pending: boolean;
  readonly onCodeChange: (value: string) => void;
  readonly onSubmit: () => void;
  readonly onDone: () => void;
  readonly onBeginSetup: () => void;
}): ReactNode {
  if (props.backupCodes !== null) {
    return (
      <>
        <BackupCodesPanel codes={props.backupCodes} />
        <Button type="button" data-testid="mfa-enroll-done" onClick={props.onDone}>
          Done
        </Button>
      </>
    );
  }

  if (props.secret !== null) {
    return (
      <>
        <SecretPanel qrUri={props.qrUri} secret={props.secret} />
        <ConfirmFields
          code={props.code}
          pending={props.pending}
          onCodeChange={props.onCodeChange}
          onSubmit={props.onSubmit}
        />
      </>
    );
  }

  return <IntroPanel loading={props.loading} onBeginSetup={props.onBeginSetup} />;
}

import {
  Button,
  Card,
  CardTitle,
  ErrorBanner,
  Field as FieldRow,
  Input,
  Label,
} from "@bc-solutions-coder/ui";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";

/**
 * The Register screen (Wallow-vec7.3.8).
 *
 * `clientId` and `returnUrl` arrive as props rather than being read from the
 * router inside the component: the route owns the query string (the oracle's two
 * `[SupplyParameterFromQuery]` properties) and hands them down, which keeps this
 * component a pure function of its inputs and testable without a router — the
 * seam `ResetPasswordForm` established and `MfaChallengeForm` followed.
 *
 * Testids `register-error`, `register-email`, `register-password`,
 * `register-confirm-password`, `register-terms`, `register-privacy` and
 * `register-submit` come verbatim from the oracle. The strength meter and the
 * passwordless toggle ship without testids in the oracle, so those are minted
 * under the `{page}-{element}` rule.
 *
 * The API is reached through `getWallowAuthSdk()`, never `@bc-solutions-coder/sdk`
 * directly — that facade is this app's only permitted importer of the SDK.
 *
 * ── THE ERROR BRANCHES (REVISED — see Wallow-vec7.7) ─────────────────────────
 *
 * `AccountController.Register` (api/.../Controllers/AccountController.cs:639-724)
 * fails in four ways, each a 400 with a bare `{ succeeded, error }` body (NOT
 * problem details):
 *
 *     400 error "passwords_do_not_match"   line 648
 *     400 error "invalid_client_id"        line 658
 *     400 error "email_taken"              ~686, from DuplicateEmail/DuplicateUserName
 *     400 error <raw IdentityResult sentence>   the `_ =>` fallback
 *
 * `unwrap()` throws on all four. This screen first shipped ONE generic message
 * for every rejection, because `toWallowError()` built its `code` from
 * `extensions.code ?? code` and these endpoints emit neither — so the token under
 * `error` was never read and `code` was always "UNKNOWN". Status could not
 * substitute either: all four share a 400, so unlike the sibling ResetPassword
 * port (one failure reason, so 400 *meant* invalid_token) there was nothing to
 * narrow on.
 *
 * Wallow-vec7.7 closed that: `readCode` now probes `extensions.code > code >
 * error`, so the API's token reaches this screen intact and THREE of the four
 * branches are recoverable. The fourth stays generic on purpose — its "code" is a
 * raw English sentence from Identity, not a token, so there is nothing stable to
 * key on.
 *
 * The oracle's own switch is only partly worth porting:
 *
 *   - Its `"password_too_weak"` branch is DEAD CODE — the controller never emits
 *     that string. That case arrives as the raw sentence, and lands on the
 *     generic tail here.
 *   - The API's error tail renders `result.Error` RAW, so a user really can be
 *     shown Identity's own prose ("Passwords must have at least one digit
 *     ('0'-'9')."). `code` is a machine member here: it is matched against KNOWN
 *     tokens and NEVER rendered. Anything unrecognised — including a 400 carrying
 *     a token added tomorrow — falls to the generic message rather than guessing.
 *
 * Narrowing is STRUCTURAL rather than `instanceof WallowError`, because that
 * class is exported from the SDK's `./server` entry and screens may not import
 * the SDK at all. A network-level rejection carries neither `code` nor `status`
 * and must fall through to the generic message rather than throw.
 *
 * ── NO ApiBaseUrl PREPEND (inherited from Wallow-vec7.3.4) ───────────────────
 *
 * The oracle builds external-login links as `{ApiBaseUrl}/v1/...` against a
 * cross-origin API. That prepend is NOT ported: this app's h3 server
 * (`src/lib/auth-server.ts`) is a passthrough reverse proxy mounting `/v1/**` at
 * the ROOT, so this origin hosts them and the origin is `""`.
 */

/** This app's own origin — see the origin note above. */
const SAME_ORIGIN = "";

/** The bail target for an unsafe returnUrl, matching the sibling ports. */
const ERROR_HREF = "/error?reason=invalid_redirect_uri";

/** The oracle's client-side guards, in the oracle's own order. */
const BLANK_EMAIL_MESSAGE = "Please enter your email address.";
const BLANK_PASSWORD_MESSAGE = "Please enter a password.";
const PASSWORD_MISMATCH_MESSAGE = "Passwords do not match.";
const TERMS_REQUIRED_MESSAGE = "You must agree to the Terms of Service.";
const PRIVACY_REQUIRED_MESSAGE = "You must agree to the Privacy Policy.";

/** The oracle's `"email_taken" =>` branch, reachable again as of Wallow-vec7.7. */
const EMAIL_TAKEN_MESSAGE = "An account with this email already exists. Please sign in instead.";

/**
 * `passwords_do_not_match`: the server-side echo of the local guard, so it says
 * the same thing the local guard says.
 */
const SERVER_MISMATCH_MESSAGE = PASSWORD_MISMATCH_MESSAGE;

/**
 * `invalid_client_id`: the `client_id` came off the QUERY STRING, not the form.
 * Nothing the user typed is wrong and retyping it cannot help, so the copy points
 * at the link rather than blaming their input.
 */
const INVALID_CLIENT_MESSAGE =
  "The sign-up link you followed is not valid. Please go back to the application you came from and try again.";

/**
 * The oracle's `_ =>` tail, minus its raw-string leak. Also the honest home of
 * the weak-password rejection, whose reason arrives as an English sentence rather
 * than a token — see the error-branch note above.
 */
const GENERIC_FAILURE_MESSAGE = "An error occurred. Please try again.";

/** The API's machine tokens for this endpoint. Matched against, never rendered. */
const EMAIL_TAKEN = "email_taken";
const PASSWORDS_DO_NOT_MATCH = "passwords_do_not_match";
const INVALID_CLIENT_ID = "invalid_client_id";

/** The oracle's `"passwordless"` sentinel, compared case-insensitively server-side. */
const PASSWORDLESS = "passwordless";

/** The oracle's two `UpdatePasswordStrength` length thresholds. */
const STRONG_MIN_LENGTH = 12;
const FAIR_MIN_LENGTH = 8;

/** Read a member off an unknown rejection without asserting its shape. */
function readMember(cause: unknown, name: string): unknown {
  if (typeof cause !== "object" || cause === null || !(name in cause)) {
    return undefined;
  }

  return (cause as Record<string, unknown>)[name];
}

/**
 * Map a rejection onto user-facing copy.
 *
 * A `Map` rather than a `switch` on an attacker-influenced value is not needed
 * here — `code` comes from the API, not the query string — but the same rule
 * applies: an unrecognised code falls to the generic tail rather than being
 * rendered.
 */
function registerFailureMessage(cause: unknown): string {
  const code: unknown = readMember(cause, "code");

  if (code === EMAIL_TAKEN) {
    return EMAIL_TAKEN_MESSAGE;
  }

  if (code === PASSWORDS_DO_NOT_MATCH) {
    return SERVER_MISMATCH_MESSAGE;
  }

  if (code === INVALID_CLIENT_ID) {
    return INVALID_CLIENT_MESSAGE;
  }

  return GENERIC_FAILURE_MESSAGE;
}

/** The oracle's `UpdatePasswordStrength`, ported predicate-for-predicate. */
interface PasswordStrength {
  readonly label: string;
  readonly percent: number;
  readonly indicatorClass: string;
}

/**
 * `null` for an empty password — the oracle's
 * `@if (!string.IsNullOrEmpty(_password))` gate on the whole meter.
 *
 * The character classes mirror `char.IsUpper` / `IsLower` / `IsDigit` /
 * `!IsLetterOrDigit`, which are Unicode-aware in .NET, so the Unicode property
 * escapes are used rather than `[A-Z]` — a port that narrowed to ASCII would rate
 * a perfectly strong non-Latin password Weak.
 */
function passwordStrength(password: string): PasswordStrength | null {
  if (password === "") {
    return null;
  }

  const hasUpper: boolean = /\p{Lu}/u.test(password);
  const hasLower: boolean = /\p{Ll}/u.test(password);
  const hasDigit: boolean = /\p{Nd}/u.test(password);
  const hasSpecial: boolean = /[^\p{L}\p{N}]/u.test(password);
  const hasMix: boolean = hasUpper && hasLower && (hasDigit || hasSpecial);

  // Length ALONE is not enough: the oracle's `Length >= 12 && hasMix`. A 12-char
  // all-lowercase password falls through to Fair.
  if (password.length >= STRONG_MIN_LENGTH && hasMix) {
    return { label: "Strong", percent: 100, indicatorClass: "bg-green-500" };
  }

  if (password.length >= FAIR_MIN_LENGTH) {
    return { label: "Fair", percent: 50, indicatorClass: "bg-yellow-500" };
  }

  return { label: "Weak", percent: 25, indicatorClass: "bg-red-500" };
}

/**
 * The oracle's `VerifyEmailUrl`, with the open-redirect guard the oracle lacks.
 *
 * REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`): an
 * unsafe returnUrl routes to `/error?reason=invalid_redirect_uri` rather than
 * silently falling back to "/", which would swallow the attempt.
 *
 * An absent returnUrl is NOT an attack — it is the oracle's ordinary direct-signup
 * path — so the guard runs on a PRESENT value only. "" counts as absent, matching
 * the oracle's `string.IsNullOrEmpty(ReturnUrl)` and keeping a bare `?returnUrl=`
 * off the error page.
 */
function verifyEmailTarget(returnUrl: string | undefined): string {
  if (returnUrl === undefined || returnUrl === "") {
    return "/verify-email";
  }

  if (!getWallowAuthSdk().oidc.isSafeReturnUrl(returnUrl)) {
    return ERROR_HREF;
  }

  return `/verify-email?returnUrl=${encodeURIComponent(returnUrl)}`;
}

/**
 * The oracle's `GetExternalLoginUrl`, minus the `ApiBaseUrl` prepend.
 *
 * The oracle round-trips the user back to `Navigation.Uri` — an ABSOLUTE URL,
 * which it can afford because its returnUrl travels to a cross-origin API. Here
 * the path is same-origin and relative, which is both sufficient and what the
 * server's own redirect validator accepts: a single leading "/" and not "//". An
 * absolute URL would be refused.
 */
function externalLoginUrl(provider: string): string {
  const returnUrl = `${globalThis.location.pathname}${globalThis.location.search}`;

  return (
    `${SAME_ORIGIN}/v1/identity/auth/external-login` +
    `?provider=${encodeURIComponent(provider)}` +
    `&returnUrl=${encodeURIComponent(returnUrl)}`
  );
}

/** What the informational client-tenant lookup resolves to. */
interface ClientTenant {
  readonly orgName?: string;
}

/** The registration request body, matching `AccountRegisterRequest`. */
interface RegisterRequest {
  readonly email: string;
  readonly password: string;
  readonly confirmPassword: string;
  readonly clientId?: string;
  readonly loginMethod: string | null;
  readonly returnUrl?: string;
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1 text-center">
      <CardTitle>Create an account</CardTitle>
      <p className="text-sm text-muted-foreground">Enter your details to get started</p>
    </div>
  );
}

/** The oracle's "You're registering for @_orgName" info `BbAlert`. */
function OrgNameBanner({ orgName }: { readonly orgName: string }) {
  return (
    <div className="rounded-md border border-border bg-muted p-3" data-testid="register-org-name">
      <p className="text-sm text-foreground">You&apos;re registering for {orgName}</p>
    </div>
  );
}

/** Shown while the oracle's concurrent `OnInitializedAsync` is in flight. */
function InitLoading() {
  return (
    <div className="py-6 text-center" data-testid="register-loading">
      <p className="text-sm text-muted-foreground">Loading...</p>
    </div>
  );
}

/** The oracle's `BbProgress` + label, gated on a non-empty password. */
function StrengthMeter({ strength }: { readonly strength: PasswordStrength }) {
  return (
    <div className="space-y-1" data-testid="register-password-strength">
      <div className="h-1.5 w-full overflow-hidden rounded-full bg-muted">
        <div
          className={`h-full ${strength.indicatorClass}`}
          style={{ width: `${strength.percent}%` }}
        />
      </div>
      <p className="text-xs text-muted-foreground">{strength.label}</p>
    </div>
  );
}

/** One text/password input. */
function Field(props: {
  readonly id: string;
  readonly label: string;
  readonly type: string;
  readonly testId: string;
  readonly placeholder: string;
  readonly value: string;
  readonly onChange: (value: string) => void;
}) {
  const { id, label, type, testId, placeholder, value, onChange } = props;

  return (
    <FieldRow>
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type={type}
        placeholder={placeholder}
        data-testid={testId}
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
    </FieldRow>
  );
}

/**
 * The two password blocks, which the oracle wraps in one `@if (!_isPasswordless)`.
 * Extracted so the passwordless branch is a single decision in one place rather
 * than two conditionals that could drift apart.
 */
function PasswordBlock(props: {
  readonly password: string;
  readonly confirmPassword: string;
  readonly onPasswordChange: (value: string) => void;
  readonly onConfirmChange: (value: string) => void;
}) {
  const { password, confirmPassword, onPasswordChange, onConfirmChange } = props;
  const strength: PasswordStrength | null = passwordStrength(password);

  return (
    <>
      <div className="space-y-2">
        <Field
          id="password"
          label="Password"
          type="password"
          testId="register-password"
          placeholder="Create a password"
          value={password}
          onChange={onPasswordChange}
        />
        {strength === null ? null : <StrengthMeter strength={strength} />}
      </div>
      <div className="space-y-2">
        <Field
          id="confirmPassword"
          label="Confirm Password"
          type="password"
          testId="register-confirm-password"
          placeholder="Confirm your password"
          value={confirmPassword}
          onChange={onConfirmChange}
        />
        {confirmPassword !== "" && password !== confirmPassword ? (
          <p className="text-xs text-destructive">{PASSWORD_MISMATCH_MESSAGE}</p>
        ) : null}
      </div>
    </>
  );
}

/** One consent checkbox with its inline link. */
function ConsentCheckbox(props: {
  readonly id: string;
  readonly testId: string;
  readonly checked: boolean;
  readonly href: string;
  readonly linkText: string;
  readonly onChange: (checked: boolean) => void;
}) {
  const { id, testId, checked, href, linkText, onChange } = props;

  return (
    <div className="flex items-start space-x-2">
      <input
        id={id}
        type="checkbox"
        className="mt-1"
        data-testid={testId}
        checked={checked}
        onChange={(e) => {
          onChange(e.target.checked);
        }}
      />
      <label className="text-sm font-normal leading-snug text-foreground" htmlFor={id}>
        I agree to the{" "}
        <a href={href} className="text-primary underline-offset-4 hover:underline">
          {linkText}
        </a>
      </label>
    </div>
  );
}

/** The oracle's "Or continue with" block, gated on `_externalProviders.Count > 0`. */
function ExternalProviders({ providers }: { readonly providers: readonly string[] }) {
  if (providers.length === 0) {
    return null;
  }

  return (
    <div className="space-y-3" data-testid="register-external-providers">
      <p className="text-center text-xs uppercase text-muted-foreground">Or continue with</p>
      <div className="grid grid-cols-2 gap-2">
        {providers.map((provider) => (
          <a
            key={provider}
            href={externalLoginUrl(provider)}
            className="rounded-md border border-border px-3 py-2 text-center text-sm text-foreground"
            data-testid={`register-external-${provider.toLowerCase()}`}
          >
            {provider}
          </a>
        ))}
      </div>
    </div>
  );
}

/** The oracle's `BbCardFooter`. */
function AlreadyHaveAccount({ returnUrl }: { readonly returnUrl?: string }) {
  // The oracle's `LoginUrl`. Unsafe values are refused at the redirect, not here:
  // this is an href the user chooses to follow, and /login runs its own guard.
  const href: string =
    returnUrl === undefined || returnUrl === ""
      ? "/login"
      : `/login?returnUrl=${encodeURIComponent(returnUrl)}`;

  return (
    <div className="w-full text-center">
      <p className="text-sm text-muted-foreground">
        Already have an account?{" "}
        <a href={href} className="text-primary underline-offset-4 hover:underline">
          Sign in
        </a>
      </p>
    </div>
  );
}

/** The oracle's `<form>`, from the email field down to the submit. */
function RegisterFields(props: {
  readonly email: string;
  readonly password: string;
  readonly confirmPassword: string;
  readonly isPasswordless: boolean;
  readonly termsAccepted: boolean;
  readonly privacyAccepted: boolean;
  readonly pending: boolean;
  readonly onEmailChange: (value: string) => void;
  readonly onPasswordChange: (value: string) => void;
  readonly onConfirmChange: (value: string) => void;
  readonly onPasswordlessChange: (value: boolean) => void;
  readonly onTermsChange: (value: boolean) => void;
  readonly onPrivacyChange: (value: boolean) => void;
  readonly onSubmit: () => void;
}) {
  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        props.onSubmit();
      }}
    >
      <Field
        id="email"
        label="Email"
        type="email"
        testId="register-email"
        placeholder="name@example.com"
        value={props.email}
        onChange={props.onEmailChange}
      />

      <div className="flex items-center space-x-2">
        <input
          id="passwordless"
          type="checkbox"
          data-testid="register-passwordless-toggle"
          checked={props.isPasswordless}
          onChange={(e) => {
            props.onPasswordlessChange(e.target.checked);
          }}
        />
        <label className="text-sm font-normal leading-snug text-foreground" htmlFor="passwordless">
          Sign up without a password
        </label>
      </div>

      {props.isPasswordless ? null : (
        <PasswordBlock
          password={props.password}
          confirmPassword={props.confirmPassword}
          onPasswordChange={props.onPasswordChange}
          onConfirmChange={props.onConfirmChange}
        />
      )}

      <div className="space-y-3">
        <ConsentCheckbox
          id="termsAccepted"
          testId="register-terms"
          checked={props.termsAccepted}
          href="/terms"
          linkText="Terms of Service"
          onChange={props.onTermsChange}
        />
        <ConsentCheckbox
          id="privacyAccepted"
          testId="register-privacy"
          checked={props.privacyAccepted}
          href="/privacy"
          linkText="Privacy Policy"
          onChange={props.onPrivacyChange}
        />
      </div>

      <Button
        type="submit"
        // The oracle's `Disabled="_isSubmitting"` — one click, one account.
        disabled={props.pending}
        data-testid="register-submit"
      >
        {props.pending ? "Creating account..." : "Create account"}
      </Button>
    </form>
  );
}

export interface RegisterFormProps {
  /** The `client_id` query parameter — `undefined` when the link omits it. */
  readonly clientId?: string;
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
}

export function RegisterForm({ clientId, returnUrl }: RegisterFormProps): ReactNode {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [isPasswordless, setIsPasswordless] = useState(false);
  const [termsAccepted, setTermsAccepted] = useState(false);
  const [privacyAccepted, setPrivacyAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // The oracle's two concurrent `OnInitializedAsync` calls. Two independent
  // `useQuery` hooks BOTH fire on mount, which is what makes them concurrent —
  // an `await` chain in one query fn would collapse them back into two
  // sequential round-trips, the exact thing the oracle's comment calls out.
  const providersQuery = useQuery({
    queryKey: ["auth", "external-providers"],
    queryFn: async (): Promise<readonly string[]> =>
      (await getWallowAuthSdk().auth.getExternalProviders()) as readonly string[],
  });

  // Collapsed to a plain string so the `enabled` gate and the query fn agree
  // WITHOUT a cast: the oracle's `IsNullOrEmpty` treats absent and blank alike,
  // and "" is the one value the gate refuses.
  const tenantClientId: string = clientId ?? "";

  const tenantQuery = useQuery({
    queryKey: ["auth", "client-tenant", tenantClientId],
    // The oracle's `if (!string.IsNullOrEmpty(ClientId))` gate.
    enabled: tenantClientId !== "",
    queryFn: async (): Promise<ClientTenant> =>
      (await getWallowAuthSdk().auth.getClientTenant(tenantClientId)) as ClientTenant,
  });

  const registerMutation = useMutation({
    mutationFn: async (request: RegisterRequest): Promise<void> => {
      await getWallowAuthSdk().auth.register(request);
    },
  });

  /** Create the account. The ONLY caller of `register`. */
  const submitRegistration = (request: RegisterRequest): void => {
    registerMutation.mutate(request, {
      // Resolution IS success: every failure this endpoint has is non-2xx, so
      // `unwrap()` has already thrown by the time this runs.
      onSuccess: () => {
        void navigate({ href: verifyEmailTarget(returnUrl) });
      },
      onError: (cause: unknown) => {
        // No account was created, so there IS something to fix. Every reason this
        // endpoint rejects for is actionable only on the fields, so drop back to
        // the form.
        setError(registerFailureMessage(cause));
      },
    });
  };

  const handleSubmit = (): void => {
    // The oracle's guards, in the oracle's own order.
    if (email.trim() === "") {
      setError(BLANK_EMAIL_MESSAGE);
      return;
    }

    // Both password guards sit inside the oracle's `if (!_isPasswordless)`: a
    // passwordless signup has no password to check, so demanding one would make
    // the toggle unusable.
    if (!isPasswordless) {
      if (password.trim() === "") {
        setError(BLANK_PASSWORD_MESSAGE);
        return;
      }

      if (password !== confirmPassword) {
        setError(PASSWORD_MISMATCH_MESSAGE);
        return;
      }
    }

    if (!termsAccepted) {
      setError(TERMS_REQUIRED_MESSAGE);
      return;
    }

    if (!privacyAccepted) {
      setError(PRIVACY_REQUIRED_MESSAGE);
      return;
    }

    // The oracle's `_errorMessage = null;` at the top of HandleRegister: a stale
    // failure sitting above a successful registration would be a lie.
    setError(null);

    const request: RegisterRequest = {
      email,
      password,
      confirmPassword,
      clientId,
      loginMethod: isPasswordless ? PASSWORDLESS : null,
      returnUrl,
    };

    submitRegistration(request);
  };

  if (providersQuery.isLoading || tenantQuery.isLoading) {
    // The oracle renders nothing until both calls settle (prerender: false).
    return (
      <Card spacing="p-6">
        <InitLoading />
      </Card>
    );
  }

  // The org name is INFORMATIONAL ONLY: a failed lookup (this endpoint 404s for
  // an unknown client) leaves it undefined and the form stays usable, per the
  // oracle's swallowed `HttpRequestException`. A cosmetic banner must never block
  // a registration.
  const orgName: string | undefined = tenantQuery.data?.orgName;

  return (
    <Card>
      <CardHeading />
      {orgName === undefined || orgName === "" ? null : <OrgNameBanner orgName={orgName} />}
      {error === null ? null : <ErrorBanner data-testid="register-error">{error}</ErrorBanner>}
      <RegisterFields
        email={email}
        password={password}
        confirmPassword={confirmPassword}
        isPasswordless={isPasswordless}
        termsAccepted={termsAccepted}
        privacyAccepted={privacyAccepted}
        // "One click, one account": the submit must not stay live while a
        // registration is in flight.
        pending={registerMutation.isPending}
        onEmailChange={setEmail}
        onPasswordChange={setPassword}
        onConfirmChange={setConfirmPassword}
        onPasswordlessChange={setIsPasswordless}
        onTermsChange={setTermsAccepted}
        onPrivacyChange={setPrivacyAccepted}
        onSubmit={handleSubmit}
      />
      <ExternalProviders providers={providersQuery.data ?? []} />
      <AlreadyHaveAccount returnUrl={returnUrl} />
    </Card>
  );
}

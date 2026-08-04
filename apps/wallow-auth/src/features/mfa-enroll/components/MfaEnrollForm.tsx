import {
  AppForm,
  type AppFormApi,
  FormError,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import type { MfaEnrollmentConfirmedResponse } from "@bc-solutions-coder/sdk";
import { Button, Card, ErrorBanner, MutedText, NoticeBanner, Text } from "@bc-solutions-coder/ui";
import { useMutation } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { QRCodeSVG } from "qrcode.react";
import { type ReactElement, type ReactNode, useState } from "react";
import { z } from "zod";
import { mfaConfirmEnrollmentMutation } from "../api";
import { confirmFailureMessage, confirmGuardMessage, type ConfirmValues } from "../enroll-result";
import { useEnrollmentStart } from "../hooks/use-enrollment-start";
import { toAppHref } from "@shared/lib/base-path";
import { useReturnUrlGuard } from "@shared/hooks/use-return-url-guard";

/**
 * The MfaEnroll screen (Wallow-vec7.3.7).
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
 * The API is reached through the request-scoped SDK on the router context
 * (`useRouteContext({ from: "__root__" })`), calling the generated operations
 * directly — there is no app-level facade (Wallow-pu6a.5.5).
 *
 * ── THE RELAY IS GONE (this screen's whole reason to exist) ───────────────────
 *
 * The oracle enrolls during PRERENDER (the only place `HttpContext` supplies the
 * partial-auth cookie) and smuggles the secret, the qr uri AND THE RAW COOKIE
 * HEADER through `PersistentComponentState` so the interactive circuit — where
 * `HttpContext` is null — can restore them and re-inject the cookie on the
 * confirm call (`PersistedEnrollment`, `ApiCookieJar`, `SeedFromBrowserCookies`).
 *
 * None of that is ported. This app's API surface (`src/shared/lib/api-passthrough.server.ts`) is a
 * passthrough reverse proxy and the client sends `credentials: "include"`, so the
 * `Identity.MfaPartial` cookie rides ordinary same-origin requests: `enrollTotp()`
 * takes no arguments and `confirmEnrollment` receives only `{ secret, code }`,
 * because there is no longer anything to thread alongside them.
 *
 * `TryTakeFromJson<PersistedEnrollment>` also existed to stop the circuit
 * re-calling `enroll/totp`, which would mint a SECOND secret and silently
 * invalidate the QR the user had already scanned. With no prerender/circuit split
 * there is one pass and one call, and `useEnrollmentStart` keeps it that way.
 *
 * ── THE ERROR BRANCHES ────────────────────────────────────────────────────────
 *
 * All three endpoints' failure copy lives in `../enroll-result`, which documents
 * the matrix, the code-versus-status fallback and the divergences from the
 * oracle. This screen owns only the CONFIRM side of it; the start and the token
 * exchange report through `useEnrollmentStart`.
 *
 * `mfa-enroll-error` is stamped in TWO places, and that is deliberate rather than
 * a duplicate: the start-side banner sits at the card level, because a start that
 * failed has no secret and therefore no form to hang a banner inside, while the
 * confirm-side banner is `<FormError/>` inside `ConfirmForm`. The two can never
 * stand at once — a confirm needs a secret, which needs a start that succeeded,
 * and the intro's retry clears the start error before refiring — so exactly one
 * element ever carries that id.
 *
 * ── WHY THE CONFIRM RUNS THE FORMS PACKAGE "SIDEWAYS" ─────────────────────────
 *
 * `ConfirmForm` takes the plain-`onSubmit` escape hatch instead of handing
 * `useAppForm` the generated mutation, for this endpoint's own reasons:
 * `splitServerError` reads RFC 7807 members and `enroll/confirm` answers with a
 * bare `{ succeeded, error }` body, so only `confirmFailureMessage` can tell its
 * rejections apart; and the blank-code guard shares that one banner, which a zod
 * rule could not do — it would abort `handleSubmit` before the callback ran. The
 * schema below is therefore rule-free, and the banner text is this component's
 * own `useState` handed to the shell as an EXPLICIT `serverError` prop.
 *
 * ── THE QR CODE ───────────────────────────────────────────────────────────────
 *
 * `qrUri` is an `otpauth://` URI (`MfaService.cs:38`), NOT a renderable image, so
 * the QR has to be drawn client-side. This screen draws the QR for real.
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

/** The oracle's `Sanitize(null)` fallback for a user who arrived without one. */
const HOME_HREF: string = toAppHref("/");

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1 text-center">
      <Text as="h2" variant="subheading" color="onCard">
        Set up two-factor authentication
      </Text>
      <MutedText>
        Scan the QR code with your authenticator app, then enter the code to confirm.
      </MutedText>
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
      <MutedText>Scan this QR code with your authenticator app:</MutedText>
      {qrUri === null ? null : <QrPanel qrUri={qrUri} />}
      <Text as="p" variant="caption" color="muted">
        Or enter this secret manually:
      </Text>
      <div className="bg-muted rounded-md p-2 font-mono text-sm" data-testid="mfa-enroll-secret">
        {secret}
      </div>
    </div>
  );
}

/**
 * RULE-FREE on purpose. `revalidateLogic` runs this on submit and aborts
 * `handleSubmit` on any failure, which would preempt the banner
 * `confirmGuardMessage` shares with the rejection copy — see the header note.
 * It is here for the value type alone.
 */
const confirmSchema = z.object({ code: z.string() });

const EMPTY_CONFIRM: ConfirmValues = { code: "" };

/**
 * The oracle's `<form>`: the code field and the submit, on the shared forms
 * layer. `testIdPrefix="mfa-enroll"` derives `mfa-enroll-code` (from the field
 * name), `mfa-enroll-submit` and `mfa-enroll-error` — the ids the E2E suite
 * already selects.
 */
function ConfirmForm({
  secret,
  onConfirmed,
}: {
  readonly secret: string;
  readonly onConfirmed: (codes: readonly string[]) => void;
}): ReactElement {
  const { sdk } = useRouteContext({ from: "__root__" });
  const [formError, setFormError] = useState<string | null>(null);

  // Its own mutation, never folded into the start: different endpoint, different
  // lifetime (the start fires once on mount, the confirm once per submit and
  // again in place after a rejected code).
  const confirmMutation = useMutation(mfaConfirmEnrollmentMutation({ client: sdk.client }));

  // Annotated rather than inferred: `onSubmit` below reads `form` back to clear
  // the code, and an unannotated `const` referenced inside its own initializer
  // is a circular inference TypeScript refuses.
  const form: AppFormApi<ConfirmValues> = useAppForm<ConfirmValues>({
    schema: confirmSchema,
    defaultValues: EMPTY_CONFIRM,
    onSubmit: async (values: ConfirmValues): Promise<void> => {
      const guard: string | null = confirmGuardMessage(values);

      if (guard !== null) {
        setFormError(guard);
        return;
      }

      // The oracle's `_errorMessage = null;` at the top of `HandleConfirm`.
      setFormError(null);

      let result: MfaEnrollmentConfirmedResponse;

      try {
        // The generated artifact's REQUEST object, not a bare body. The secret MUST
        // be the one `enroll/totp` minted — the server re-validates the TOTP against
        // the secret in the body before storing it. Nothing else rides along: the
        // oracle needed a cookie header here, and this does not.
        result = await confirmMutation.mutateAsync({ body: { secret, code: values.code } });
      } catch (error: unknown) {
        // The form deliberately stays up, and the SECRET is deliberately kept: the
        // TOTP window rolls every 30 seconds, so a stale code is the common cause,
        // and re-enrolling behind the user's back would invalidate the authenticator
        // entry they just made.
        setFormError(confirmFailureMessage(error));
        // The CODE, by contrast, is cleared — a DIVERGENCE from the oracle, which
        // leaves `_code` sitting in the box. A rejected TOTP is single-use and
        // time-boxed: it is guaranteed dead, so keeping it makes the user
        // select-all-delete before every retry and invites a resubmit of the
        // identical failing value. Same reasoning as the sibling
        // `MfaChallengeForm`'s clear-on-toggle.
        form.setFieldValue("code", "");
        return;
      }

      // Reached only on a RESOLVED call, which is always a success: every failure
      // this endpoint has is non-2xx, so `unwrap()` has already thrown above.
      //
      // The `??` survives the move to the generated type even though that type
      // declares `backupCodes` REQUIRED: the declaration is the schema's claim, not
      // the wire's. `MfaController` returns `Ok(new { succeeded = true, backupCodes })`
      // and a null list serializes to an absent member, which would reach
      // `BackupCodesPanel` as `undefined.map` — the success screen replaced by an
      // error boundary on an enrollment that WORKED. An empty list is still a
      // successful enrollment and MFA really is on.
      onConfirmed(result.backupCodes ?? []);
    },
  });

  return (
    <AppForm form={form} testIdPrefix="mfa-enroll" serverError={formError} className="space-y-4">
      <FormError />
      <form.AppField name="code">
        {(field) => <field.TextField label="Verification code" placeholder="Enter 6-digit code" />}
      </form.AppField>
      {/* The oracle's `Loading`/`Disabled="_isSubmitting"`, now the shell's:
          a double submit burns the 30-second TOTP window and mints a second set
          of backup codes over the ones the user is mid-way through writing down. */}
      <SubmitButton pendingLabel="Verifying...">Verify and enable MFA</SubmitButton>
    </AppForm>
  );
}

/** The oracle's success `BbAlert` plus its backup-code list. */
function BackupCodesPanel({ codes }: { readonly codes: readonly string[] }) {
  return (
    <>
      <NoticeBanner>
        <Text as="p" variant="bodySm" weight="medium">
          MFA enabled successfully
        </Text>
        <MutedText>
          Save these backup codes in a safe place. They can be used to access your account if you
          lose your authenticator device.
        </MutedText>
      </NoticeBanner>
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
      <MutedText>
        Two-factor authentication adds an extra layer of security to your account by requiring a
        code from an authenticator app when you sign in.
      </MutedText>
      <Text as="p" variant="bodySm" weight="semibold">
        You will need:
      </Text>
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
  const [backupCodes, setBackupCodes] = useState<readonly string[] | null>(null);

  // Evaluated before anything else happens; the hook owns the bail navigation.
  const returnUrlVerdict = useReturnUrlGuard(returnUrl);

  // The opening sequence — exchange the settings hand-off token, then start. A
  // refused destination never enrolls, so the guard's verdict suspends it: do not
  // make a user set up a second factor for somewhere already decided against.
  const enrollment = useEnrollmentStart(enrollToken, returnUrlVerdict === "refuse");

  const handleDone = (): void => {
    // Unsafe values were refused at mount, so a present returnUrl here is safe.
    // A FULL navigation, not `navigate()`: `/connect/**` is served by the passthrough
    // reverse proxy, not by the client-side route tree, which would 404 in-app.
    // No origin prepend — see the origin-divergence note above.
    globalThis.location.href = returnUrl ?? HOME_HREF;
  };

  if (returnUrlVerdict === "refuse") {
    // The guard is navigating away; rendering the form would invite the user to
    // produce a second factor for a destination already refused.
    return null;
  }

  return (
    <Card>
      <CardHeading />
      {/* The start-side half of `mfa-enroll-error` — see the header note on why
          the id is stamped in two places that can never both render. */}
      {enrollment.error === null ? null : (
        <ErrorBanner data-testid="mfa-enroll-error">{enrollment.error}</ErrorBanner>
      )}
      {renderBody({
        backupCodes,
        secret: enrollment.secret,
        qrUri: enrollment.qrUri,
        loading: enrollment.loading,
        onConfirmed: setBackupCodes,
        onDone: handleDone,
        onBeginSetup: enrollment.beginSetup,
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
  readonly loading: boolean;
  readonly onConfirmed: (codes: readonly string[]) => void;
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
        <ConfirmForm secret={props.secret} onConfirmed={props.onConfirmed} />
      </>
    );
  }

  return <IntroPanel loading={props.loading} onBeginSetup={props.onBeginSetup} />;
}

import {
  AppForm,
  type AppFormApi,
  FormError,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import { buildExchangeTicketUrl } from "@bc-solutions-coder/sdk";
import { Button, Card, MutedText, Text } from "@bc-solutions-coder/ui";
import { useMutation } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { type ReactElement, type ReactNode, useState } from "react";
import { z } from "zod";
import { accountVerifyMfaChallengeMutation } from "../api";
import {
  challengeGuardMessage,
  type ChallengeValues,
  verifyFailureMessage,
} from "../challenge-result";
import { BASE_PATH, toAppHref } from "@shared/lib/base-path";
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
 * The blank-input guard and the rejection→copy mapping live in
 * `../challenge-result`, which documents the endpoint's three failures, the
 * token-versus-status narrowing and the divergences from the oracle.
 *
 * ── WHY THIS SCREEN RUNS THE FORMS PACKAGE "SIDEWAYS" ────────────────────────
 *
 * It takes the plain-`onSubmit` escape hatch rather than handing `useAppForm`
 * the generated mutation, for two reasons that are this endpoint's own:
 * `splitServerError` reads RFC 7807 members and `mfa/verify` answers with a bare
 * `{ succeeded, error }` body, so only `verifyFailureMessage` can tell its three
 * rejections apart; and the blank-code guard shares that one banner, which a zod
 * rule could not do — it would abort `handleSubmit` before the callback ran. The
 * schema below is therefore rule-free, and the banner text is this screen's own
 * `useState` handed to the shell as an EXPLICIT `serverError` prop.
 *
 * The MODE (`useBackupCode`) is screen state rather than a form value, because
 * the card heading above the form branches on it too. It reaches the submit
 * through the callback's closure, which is fresh on every render.
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

/**
 * RULE-FREE on purpose. `revalidateLogic` runs this on submit and aborts
 * `handleSubmit` on any failure, which would preempt the banner
 * `challengeGuardMessage` shares with the rejection copy — see the header note.
 * It is here for the value type alone.
 */
const challengeSchema = z.object({ code: z.string() });

const EMPTY_VALUES: ChallengeValues = { code: "" };

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

/**
 * The oracle's `<form>`: the code field, the submit, and the mode toggle.
 *
 * The one field PINS its testid rather than deriving `mfa-challenge-code` from
 * its name, because the two ids are mutually exclusive branches of the oracle's
 * single `if (_useBackupCode)` — two visible code boxes would be a genuinely
 * confusing form.
 */
function ChallengeFields(props: {
  readonly form: AppFormApi<ChallengeValues>;
  readonly useBackupCode: boolean;
  readonly error: string | null;
  readonly onToggle: () => void;
}): ReactElement {
  const { form, useBackupCode, error, onToggle } = props;

  return (
    <AppForm form={form} testIdPrefix="mfa-challenge" serverError={error} className="space-y-4">
      <FormError />
      <form.AppField name="code">
        {(field) => (
          <field.TextField
            label={useBackupCode ? "Backup code" : "Verification code"}
            placeholder={useBackupCode ? "Enter backup code" : "Enter 6-digit code"}
            testId={useBackupCode ? "mfa-challenge-backup-code" : "mfa-challenge-code"}
          />
        )}
      </form.AppField>
      {/* The oracle's `Disabled="_isSubmitting"`, now the shell's — one click,
          one attempt. This screen is rate-limited into a 5-strike lockout, so a
          double submit can cost the user two of their five. */}
      <SubmitButton pendingLabel="Verifying...">Verify</SubmitButton>
      <ToggleBackupCode useBackupCode={useBackupCode} onToggle={onToggle} />
    </AppForm>
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
  const [useBackupCode, setUseBackupCode] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
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

  // Annotated rather than inferred: the toggle and `onSubmit` below read `form`
  // back, and an unannotated `const` referenced inside its own initializer is a
  // circular inference TypeScript refuses.
  const form: AppFormApi<ChallengeValues> = useAppForm<ChallengeValues>({
    schema: challengeSchema,
    defaultValues: EMPTY_VALUES,
    onSubmit: async (values: ChallengeValues): Promise<void> => {
      const guardMessage: string | null = challengeGuardMessage(values, useBackupCode);

      if (guardMessage !== null) {
        setFormError(guardMessage);
        return;
      }

      // The oracle's `_errorMessage = null;` at the top of `HandleVerify`: a stale
      // "invalid code" banner above a successful verification would be a lie.
      setFormError(null);

      let result: VerifyResult;

      try {
        // The generated artifact's REQUEST object, not a bare body. The same API op
        // carries `useBackupCode: true/false` — crossing them would send a recovery
        // code to the TOTP validator.
        result = await mutation.mutateAsync({ body: { code: values.code, useBackupCode } });
      } catch (error: unknown) {
        // The form deliberately stays up: the user has attempts left and no way to
        // spend them if it is gone.
        setFormError(verifyFailureMessage(error, useBackupCode));
        return;
      }

      // Reached only on a RESOLVED call, which is always a success: every failure
      // this endpoint has is non-2xx, so `unwrap()` has already thrown above.
      setVerified(true);
      redirect(result.signInTicket);
    },
  });

  const handleToggle = (): void => {
    setUseBackupCode(!useBackupCode);
    // The oracle's `_code = string.Empty;` — a TOTP code left sitting in the
    // backup-code box would be submitted to the wrong branch and burn one of the
    // user's five attempts.
    form.setFieldValue("code", "");
    // The oracle's `_errorMessage = null;` — "Invalid verification code" hanging
    // over a freshly-opened backup-code box is a lie.
    setFormError(null);
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
      {verified ? (
        <SuccessBanner />
      ) : (
        <ChallengeFields
          form={form}
          useBackupCode={useBackupCode}
          error={formError}
          onToggle={handleToggle}
        />
      )}
      <BackToSignIn />
    </Card>
  );
}

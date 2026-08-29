import {
  AppForm,
  type AppFormApi,
  FormError,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import { useMutation, useQuery } from "@bc-solutions-coder/query";
import { Button, Card, MutedText, Text } from "@bc-solutions-coder/ui";
import { useNavigate, useRouteContext } from "@tanstack/react-router";
import { type ReactElement, type ReactNode, useState } from "react";
import { z } from "zod";
import {
  accountGetClientTenantOptions,
  accountGetExternalProvidersOptions,
  accountRegisterMutation,
} from "../api";
import {
  PASSWORD_MISMATCH_MESSAGE,
  registerFailureMessage,
  registerGuardMessage,
  type RegisterValues,
} from "../register-result";
import { StrengthMeter } from "@shared/components/strength-meter";
import { passwordStrength, type PasswordStrength } from "@shared/lib/password-strength";
import { AuthScreen } from "@shared/components/auth-screen";
import { PRIVACY_CONSENT_LABEL, TERMS_CONSENT_LABEL } from "@shared/components/consent-labels";
import { BASE_PATH } from "@shared/lib/base-path";
import { ERROR_HREF, decideReturnUrl } from "@shared/lib/return-url";

/**
 * The Register screen (Wallow-vec7.3.8).
 *
 * `clientId` and `returnUrl` arrive as props rather than being read from the
 * router inside the component: the route owns the query string (the oracle's two
 * `[SupplyParameterFromQuery]` properties) and hands them down, which keeps this
 * component a pure function of its inputs and testable without a router.
 *
 * Testids `register-error`, `register-email`, `register-password`,
 * `register-confirm-password`, `register-terms`, `register-privacy` and
 * `register-submit` come verbatim from the oracle; the first four fall out of
 * `testIdPrefix="register"` by derivation, and the two consent boxes pin theirs
 * (their field names would derive `-terms-accepted`). The strength meter and the
 * passwordless toggle ship without testids in the oracle, so those are minted
 * under the `{page}-{element}` rule.
 *
 * The rejection→copy mapping and the five submit guards live in
 * `../register-result`; the strength rating in `@shared/lib/password-strength`
 * (shared with the setup screen, like the meter that renders it).
 *
 * ── WHY THIS SCREEN RUNS THE FORMS PACKAGE "SIDEWAYS" ────────────────────────
 *
 * Like `ResetPasswordForm`, it uses the plain-`onSubmit` escape hatch rather
 * than handing `useAppForm` the generated mutation, for two reasons that are
 * this endpoint's own:
 *
 *  1. **The failures are not RFC 7807.** `splitServerError` reads problem-details
 *     members to decide field message vs banner, and this endpoint answers with a
 *     bare `{ succeeded, error }` body. `registerFailureMessage` is the only
 *     thing that can tell its four rejections apart.
 *  2. **The guards are ORDERED and share one banner.** A zod rule would abort
 *     `handleSubmit` before the callback ran and report every field at once —
 *     see `registerGuardMessage`. The schema below is therefore rule-free.
 *
 * The banner text is consequently this screen's own `useState`, handed to the
 * shell as an EXPLICIT `serverError` prop (which `AppForm` prefers over
 * `form.wallow.serverError`, still `null` here because every rejection is caught
 * below). `<FormError />` derives `register-error` from the prefix. Navigation
 * happens at the END of the callback: an early `return` out of a guard resolves
 * the internal mutation just as a success does, so `onSuccess` could not tell
 * them apart.
 *
 * ── NO ApiBaseUrl PREPEND (inherited from Wallow-vec7.3.4) ───────────────────
 *
 * The oracle builds external-login links as `{ApiBaseUrl}/v1/...` against a
 * cross-origin API. That prepend is NOT ported: this app's API surface
 * (`src/shared/lib/api-passthrough.server.ts`) is a passthrough reverse proxy
 * mounting `/v1/**` at the ROOT, so this origin hosts them.
 */

/** This app's own origin, plus the base path it is served under. */
const SAME_ORIGIN_BASE: string = BASE_PATH;

/** The oracle's `"passwordless"` sentinel, compared case-insensitively server-side. */
const PASSWORDLESS = "passwordless";

/**
 * RULE-FREE on purpose. `revalidateLogic` runs this on submit and aborts
 * `handleSubmit` on any failure, which would preempt the ordered single banner
 * `registerGuardMessage` owns — see the header note. It is here for the value
 * types alone.
 */
const registerSchema = z.object({
  email: z.string(),
  password: z.string(),
  confirmPassword: z.string(),
  isPasswordless: z.boolean(),
  termsAccepted: z.boolean(),
  privacyAccepted: z.boolean(),
});

const EMPTY_VALUES: RegisterValues = {
  email: "",
  password: "",
  confirmPassword: "",
  isPasswordless: false,
  termsAccepted: false,
  privacyAccepted: false,
};

/**
 * The oracle's `VerifyEmailUrl`, with the open-redirect guard the oracle lacks.
 *
 * REFUSE, don't sanitize (bd memory `returnurl-guard-refuse-dont-sanitize`): an
 * unsafe returnUrl routes to `/error?reason=invalid_redirect_uri` rather than
 * silently falling back to "/", which would swallow the attempt. The mode is
 * `"empty-ok"`: an absent returnUrl is NOT an attack — it is the oracle's
 * ordinary direct-signup path — and "" counts as absent, matching the oracle's
 * `string.IsNullOrEmpty(ReturnUrl)` and keeping a bare `?returnUrl=` off the
 * error page.
 */
function verifyEmailTarget(returnUrl: string | undefined): string {
  const destination = decideReturnUrl(returnUrl, "empty-ok");

  if (destination.verdict === "absent") {
    return "/verify-email";
  }

  if (destination.verdict === "refuse") {
    return ERROR_HREF;
  }

  return `/verify-email?returnUrl=${encodeURIComponent(destination.returnUrl)}`;
}

/**
 * The oracle's `GetExternalLoginUrl`, minus the `ApiBaseUrl` prepend.
 *
 * The oracle round-trips the user back to `Navigation.Uri` — an ABSOLUTE URL,
 * which it can afford because its returnUrl travels to a cross-origin API. Here
 * the path is same-origin and relative, which is both sufficient and what the
 * server's own redirect validator accepts: a single leading "/" and not "//".
 */
function externalLoginUrl(provider: string): string {
  const returnUrl = `${globalThis.location.pathname}${globalThis.location.search}`;

  return (
    `${SAME_ORIGIN_BASE}/v1/identity/auth/external-login` +
    `?provider=${encodeURIComponent(provider)}` +
    `&returnUrl=${encodeURIComponent(returnUrl)}`
  );
}

/** The oracle's "You're registering for @_orgName" info `BbAlert`. */
function OrgNameBanner({ orgName }: { readonly orgName: string }) {
  return (
    <div className="rounded-md border border-border bg-muted p-3" data-testid="register-org-name">
      <Text as="p" variant="bodySm">
        You&apos;re registering for {orgName}
      </Text>
    </div>
  );
}

/** Shown while the oracle's concurrent `OnInitializedAsync` is in flight. */
function InitLoading() {
  return (
    <div className="py-6 text-center" data-testid="register-loading">
      <MutedText>Loading...</MutedText>
    </div>
  );
}

/** The live confirmation hint, which the submit-time guard repeats in the banner. */
function MismatchHint() {
  return (
    <Text as="p" variant="caption" color="destructive">
      {PASSWORD_MISMATCH_MESSAGE}
    </Text>
  );
}

/**
 * The two password fields and the two things that read them live — the strength
 * meter and the confirmation hint.
 *
 * Rendered under one `form.Subscribe` rather than from inside the fields' own
 * render props: the hint needs BOTH values at once, and a field only re-renders
 * for its own.
 */
function PasswordSection({
  form,
  values,
}: {
  readonly form: AppFormApi<RegisterValues>;
  readonly values: RegisterValues;
}) {
  const strength: PasswordStrength | null = passwordStrength(values.password);
  const mismatched: boolean =
    values.confirmPassword !== "" && values.password !== values.confirmPassword;

  return (
    <>
      <form.AppField name="password">
        {(field) => <field.PasswordField label="Password" placeholder="Create a password" />}
      </form.AppField>
      {strength === null ? null : (
        <StrengthMeter strength={strength} testId="register-password-strength" />
      )}
      <form.AppField name="confirmPassword">
        {(field) => (
          <field.PasswordField label="Confirm Password" placeholder="Confirm your password" />
        )}
      </form.AppField>
      {mismatched ? <MismatchHint /> : null}
    </>
  );
}

/**
 * One provider's challenge link, split out of the grid below so the mapped
 * element stays inside the JSX depth budget.
 */
function ProviderLink({ provider }: { readonly provider: string }) {
  return (
    <Button
      render={<a href={externalLoginUrl(provider)} />}
      nativeButton={false}
      variant="outline"
      data-testid={`register-external-${provider.toLowerCase()}`}
    >
      {provider}
    </Button>
  );
}

/** The oracle's "Or continue with" block, gated on `_externalProviders.Count > 0`. */
function ExternalProviders({ providers }: { readonly providers: readonly string[] }) {
  if (providers.length === 0) {
    return null;
  }

  return (
    <div className="space-y-3" data-testid="register-external-providers">
      <Text as="p" variant="caption" color="muted" align="center" className="uppercase">
        Or continue with
      </Text>
      <div className="grid grid-cols-2 gap-2">
        {providers.map((provider) => (
          <ProviderLink key={provider} provider={provider} />
        ))}
      </div>
    </div>
  );
}

/**
 * The oracle's `BbCardFooter`.
 *
 * NOT `QuietLink`: that recipe is the muted standalone secondary link, and this
 * one is an accent-coloured call to action sitting inside muted prose — the two
 * would become indistinguishable.
 */
function AlreadyHaveAccount({ returnUrl }: { readonly returnUrl?: string }) {
  // The oracle's `LoginUrl`. Unsafe values are refused at the redirect, not here:
  // this is an href the user chooses to follow, and /login runs its own guard.
  const href: string =
    returnUrl === undefined || returnUrl === ""
      ? "/login"
      : `/login?returnUrl=${encodeURIComponent(returnUrl)}`;

  return (
    <div className="w-full text-center">
      <MutedText>
        Already have an account?{" "}
        <a href={href} className="text-primary underline-offset-4 hover:underline">
          Sign in
        </a>
      </MutedText>
    </div>
  );
}

/** The oracle's `<form>`, from the email field down to the submit. */
function RegisterFields({ clientId, returnUrl }: RegisterFormProps): ReactElement {
  const { sdk } = useRouteContext({ from: "__root__" });
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string | null>(null);

  const registerMutation = useMutation(accountRegisterMutation({ client: sdk.client }));

  const form = useAppForm({
    schema: registerSchema,
    defaultValues: EMPTY_VALUES,
    onSubmit: async (values: RegisterValues): Promise<void> => {
      const guard: string | null = registerGuardMessage(values);

      if (guard !== null) {
        setFormError(guard);
        return;
      }

      // The oracle's `_errorMessage = null;` at the top of HandleRegister, moved
      // past the guards so a guard's own message survives the submit that
      // produced it. A stale failure above a successful registration is a lie.
      setFormError(null);

      try {
        // The generated artifact's REQUEST object, not a bare body: the factory
        // assembles the request itself, and a bare body would send an empty one.
        await registerMutation.mutateAsync({
          body: {
            email: values.email,
            password: values.password,
            confirmPassword: values.confirmPassword,
            clientId,
            loginMethod: values.isPasswordless ? PASSWORDLESS : null,
            returnUrl,
          },
        });
      } catch (error: unknown) {
        // No account was created, and every reason this endpoint rejects for is
        // actionable only on the fields, so drop back to the form.
        setFormError(registerFailureMessage(error));
        return;
      }

      void navigate({ href: verifyEmailTarget(returnUrl) });
    },
  });

  return (
    <AppForm form={form} testIdPrefix="register" serverError={formError} className="space-y-4">
      <FormError />

      <form.AppField name="email">
        {(field) => <field.TextField label="Email" type="email" placeholder="name@example.com" />}
      </form.AppField>

      <form.AppField name="isPasswordless">
        {(field) => (
          <field.CheckboxField
            label="Sign up without a password"
            testId="register-passwordless-toggle"
          />
        )}
      </form.AppField>

      {/* The oracle wraps both password blocks in one `@if (!_isPasswordless)`,
          so the branch is a single decision rather than two that could drift. */}
      <form.Subscribe<RegisterValues> selector={(state) => state.values}>
        {(values: RegisterValues) =>
          values.isPasswordless ? null : <PasswordSection form={form} values={values} />
        }
      </form.Subscribe>

      <form.AppField name="termsAccepted">
        {(field) => <field.CheckboxField label={TERMS_CONSENT_LABEL} testId="register-terms" />}
      </form.AppField>

      <form.AppField name="privacyAccepted">
        {(field) => <field.CheckboxField label={PRIVACY_CONSENT_LABEL} testId="register-privacy" />}
      </form.AppField>

      {/* The oracle's `Disabled="_isSubmitting"` — one click, one account. */}
      <SubmitButton pendingLabel="Creating account...">Create account</SubmitButton>
    </AppForm>
  );
}

export interface RegisterFormProps {
  /** The `client_id` query parameter — `undefined` when the link omits it. */
  readonly clientId?: string;
  /** The `returnUrl` query parameter — `undefined` when the link omits it. */
  readonly returnUrl?: string;
}

export function RegisterForm({ clientId, returnUrl }: RegisterFormProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });

  // The oracle's two concurrent `OnInitializedAsync` calls. Two independent
  // `useQuery` hooks BOTH fire on mount, which is what makes them concurrent —
  // an `await` chain in one query fn would collapse them back into two
  // sequential round-trips, the exact thing the oracle's comment calls out.
  //
  // Both this screen and the login screen read the provider list through the SAME
  // factory, so they share one cache entry.
  const providersQuery = useQuery(accountGetExternalProvidersOptions({ client: sdk.client }));

  // Collapsed to a plain string so the `enabled` gate and the query fn agree
  // WITHOUT a cast: the oracle's `IsNullOrEmpty` treats absent and blank alike,
  // and "" is the one value the gate refuses.
  const tenantClientId: string = clientId ?? "";

  const tenantQuery = useQuery({
    ...accountGetClientTenantOptions({ client: sdk.client, path: { clientId: tenantClientId } }),
    // The oracle's `if (!string.IsNullOrEmpty(ClientId))` gate.
    enabled: tenantClientId !== "",
  });

  if (providersQuery.isLoading || tenantQuery.isLoading) {
    // The oracle renders nothing until both calls settle (prerender: false), so
    // this branch stays a bare surface rather than an `AuthScreen` whose heading
    // would flash above a form that is not there yet.
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
  const orgName: string | undefined = tenantQuery.data?.orgName ?? undefined;

  return (
    <AuthScreen
      title="Create an account"
      description="Enter your details to get started"
      footer={<AlreadyHaveAccount returnUrl={returnUrl} />}
    >
      {orgName === undefined || orgName === "" ? null : <OrgNameBanner orgName={orgName} />}
      <RegisterFields clientId={clientId} returnUrl={returnUrl} />
      <ExternalProviders providers={providersQuery.data ?? []} />
    </AuthScreen>
  );
}

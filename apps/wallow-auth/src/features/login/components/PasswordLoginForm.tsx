import { AppForm, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useMutation } from "@bc-solutions-coder/query";
import { QuietLink } from "@bc-solutions-coder/ui";
import { toAppHref } from "@shared/lib/base-path";
import { useRouteContext } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { z } from "zod";
import { accountLoginMutation } from "../api";
import { BLANK_CREDENTIALS_MESSAGE, loginFailureMessage } from "../auth-result";
import type { LoginPanelProps } from "../panel";

/**
 * The PASSWORD tab of the login screen (Wallow-vec7.3.11 / 2.8a), ported from the
 * oracle's `_activeTab == LoginTab.Password` branch and its `HandleLogin`
 * (`api/src/Wallow.Auth/Components/Pages/Login.razor`:60-104, :321-360).
 *
 * This panel owns ONLY its own three fields, its own mutation and its own error
 * copy — the three things the oracle keeps per-tab. It never navigates: the
 * result goes UP through `onAuthResult` and the shell decides. See `../panel`.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `login-email`, `login-forgot-password`, `login-password`, `login-remember-me`,
 * `login-submit`. All but the link derive from the `login` prefix plus the field
 * name, so none of them is spelled twice.
 *
 * The API is reached through the request-scoped SDK on the router context
 * (`useRouteContext({ from: "__root__" })`), through the GENERATED
 * `{operation}Mutation()` factory — the app hand-rolls no `mutationFn` of its own
 * (Wallow-x4qn.9.3).
 *
 * ── WHY THE ESCAPE HATCH, NOT THE `mutation` OPTION ──────────────────────────
 *
 * `MfaChallengeForm`'s two reasons, both of which hold here. `splitServerError`
 * reads RFC 7807 members, and three of this endpoint's four outcomes arrive as
 * 200 bodies the shell has to narrow — only `../auth-result` can tell them apart.
 * And the blank-credentials guard reports into the SHELL's shared banner, which a
 * zod rule could not do: it would abort `handleSubmit` before the callback ran.
 * The schema below is therefore rule-free, and this panel renders no `FormError`.
 */

/** RULE-FREE on purpose — see the header. Here for the value types alone. */
const loginSchema = z.object({
  email: z.string(),
  password: z.string(),
  rememberMe: z.boolean(),
});

type LoginValues = z.infer<typeof loginSchema>;

/** Unchecked by DEFAULT: a long-lived session is the user's choice, never a screen's. */
const NO_CREDENTIALS: LoginValues = { email: "", password: "", rememberMe: false };

/**
 * The oracle's forgot-password escape hatch, which shares the password label's
 * line. A real href rather than a router link, like every other anchor in this
 * app — wallow-auth navigates across origins.
 *
 * A module-scope ELEMENT rather than a component rendered inline in the
 * `labelAction` prop: `react/jsx-max-depth` is 2 here and counts a prop's JSX at
 * the depth of the element carrying it, so spelled inline it would sit one level
 * past the budget. It closes over nothing, so hoisting it costs nothing either.
 */
const FORGOT_PASSWORD_LINK: ReactNode = (
  <QuietLink href={toAppHref("/forgot-password")} data-testid="login-forgot-password">
    Forgot password?
  </QuietLink>
);

export function PasswordLoginForm({ onAuthResult, onError }: LoginPanelProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });

  // The generated factory's response type governs, and the narrowing still
  // belongs to the shell — `onAuthResult` takes the RAW body and `../panel`
  // decides, because three of this endpoint's four outcomes are 200s.
  const mutation = useMutation(accountLoginMutation({ client: sdk.client }));

  const form = useAppForm<LoginValues>({
    schema: loginSchema,
    defaultValues: NO_CREDENTIALS,
    onSubmit: async (values: LoginValues): Promise<void> => {
      // The oracle's `IsNullOrWhiteSpace` guard — note WHITEspace, so "   " is
      // blank. A blank submit cannot succeed and would spend one of the user's
      // attempts against the lockout counter, so it never reaches the API.
      if (values.email.trim() === "" || values.password.trim() === "") {
        onError(BLANK_CREDENTIALS_MESSAGE);
        return;
      }

      // The oracle's `_errorMessage = null;` at the top of `HandleLogin`: a stale
      // "invalid password" banner hanging over an in-flight retry is a lie.
      // Cleared HERE rather than at the top of the callback, so the guard's own
      // message survives the submit that produced it.
      onError(null);

      let body: unknown;

      try {
        // The oracle's `LoginRequest(_email, _password, _rememberMe)`, spelled as
        // the generated artifact's REQUEST object rather than a bare body: the
        // factory assembles `{ body }`/`{ path }`/`{ query }` itself, and a bare
        // body here would send an empty request.
        body = await mutation.mutateAsync({
          body: { email: values.email, password: values.password, rememberMe: values.rememberMe },
        });
      } catch (error: unknown) {
        // The form deliberately stays up — the user has attempts left and no way
        // to spend them if it is gone.
        onError(loginFailureMessage(error));
        return;
      }

      // Resolution is NOT success here: three of this endpoint's four outcomes
      // arrive as 200 bodies. The shell narrows and branches.
      onAuthResult(body);
    },
  });

  return (
    <AppForm form={form} testIdPrefix="login" className="space-y-4">
      <form.AppField name="email">
        {(field) => <field.TextField label="Email" type="email" placeholder="name@example.com" />}
      </form.AppField>
      <form.AppField name="password">
        {(field) => (
          <field.PasswordField
            label="Password"
            placeholder="Enter your password"
            autoComplete="current-password"
            labelAction={FORGOT_PASSWORD_LINK}
          />
        )}
      </form.AppField>
      <form.AppField name="rememberMe">
        {(field) => <field.CheckboxField label="Remember me" />}
      </form.AppField>
      {/* The oracle's `Disabled="_isSubmitting"` — one click, one attempt. A
          double submit costs the user two of the five tries they get before
          lockout. */}
      <SubmitButton pendingLabel="Signing in...">Sign in</SubmitButton>
    </AppForm>
  );
}

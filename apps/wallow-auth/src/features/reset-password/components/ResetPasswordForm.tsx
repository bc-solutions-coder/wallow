import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { ErrorCode, type FailureMessageRegistry } from "@bc-solutions-coder/api-errors";
import { Card, MutedText, Text, useFailureMessage } from "@bc-solutions-coder/ui";
import { useNavigate, useRouteContext } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";
import { z } from "zod";
import { accountResetPassword } from "../api";
import { toAppHref } from "@shared/lib/base-path";

/**
 * The ResetPassword screen (Wallow-vec7.3.2).
 *
 * `email` and `token` arrive as props rather than being read from the router
 * inside the component: the route owns the query string (the oracle's two
 * `[SupplyParameterFromQuery]` properties) and hands them down, which keeps this
 * component a pure function of its inputs and testable without a router.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `reset-password-error`, `reset-password-new-password`, `reset-password-confirm`,
 * `reset-password-submit`. The "Back to sign in" footer link ships without a
 * testid in the oracle and keeps it that way.
 *
 * The API is reached through the request-scoped SDK on the router context
 * (`useRouteContext({ from: "__root__" })`), calling the generated operations
 * directly — there is no app-level facade (Wallow-pu6a.5.5).
 *
 * ── HOW THE ORACLE'S ERROR SWITCH IS PORTED ───────────────────────────────────
 *
 * The oracle switches its message on `result.Error`:
 *
 *     "invalid_token" => "This reset link is invalid or has expired..."
 *     _               => "Failed to reset password. Please try again."
 *
 * `AccountController.ResetPassword` answers RFC 7807 problems: `Auth.TokenInvalid`
 * and `Auth.TokenExpired` for a link that cannot be redeemed (an unknown email
 * deliberately collapses into the same code, so the screen cannot be used to
 * enumerate users), and `Validation.Failed` carrying the password policy's own
 * `detail` for a weak password. The generated client THROWS on any non-2xx and
 * the fetch layer parses the body into an `ApiFailure`; this screen keeps the
 * raw rejection as state and words it through `useFailureMessage`, with the
 * oracle's two branches as the call-site `RESET_MESSAGES` and the tail sentence
 * as the `fallback`. Anything else — the weak-password detail, a 5xx, a dead
 * network — reads the model's own copy. The screen never narrows the rejection
 * itself, so there is no `instanceof ApiFailure` anywhere in it.
 *
 * ── WHY THIS SCREEN RUNS THE FORMS PACKAGE "SIDEWAYS" (Wallow-ov6w.3.2) ───────
 *
 * The screen now renders on `@bc-solutions-coder/forms`, but it deliberately
 * uses NEITHER of the two things `useAppForm` normally supplies for a failure:
 *
 *  1. **No `mutation` option, and so no `splitServerError`.** That split would
 *     put the failure on the form, but the two client-side guards below share
 *     ONE banner with the rejection copy and must fire BEFORE any request. The
 *     whole existing guard order — link check, mismatch check, clear, call,
 *     keep the rejection — is preserved verbatim inside the hook's
 *     plain-`onSubmit` escape hatch.
 *  2. **No `onSuccess` option.** The escape hatch's trap is that an early
 *     `return` out of the submit callback still RESOLVES the internal mutation,
 *     which would fire `onSuccess` and bounce the user to the login banner as
 *     though the reset had happened. Navigation therefore happens at the end of
 *     the callback, on the one path that actually reached the endpoint.
 *
 * Consequently the banner text is this screen's own `useState` and is handed to
 * the shell as an EXPLICIT `serverError` prop, which `AppForm` prefers over
 * `form.wallow.serverError` (that stays `null` here — every rejection is caught
 * below). `<FormError />` still derives `reset-password-error` from the prefix.
 */

/** The oracle's guard for a link missing either half of its identity. */
const INVALID_LINK_MESSAGE = "Invalid reset link. Please request a new password reset.";

/** The oracle's client-side confirmation guard. */
const PASSWORD_MISMATCH_MESSAGE = "Passwords do not match.";

/** The oracle's `"invalid_token" =>` branch, reached here via the token problems. */
const EXPIRED_LINK_MESSAGE = "This reset link is invalid or has expired. Please request a new one.";

/** The oracle's `_ =>` branch: any other failure, including a network-level one. */
const GENERIC_FAILURE_MESSAGE = "Failed to reset password. Please try again.";

/**
 * This screen's sentences, ahead of the app registry: a bad or expired token is
 * about the RESET link specifically, not the registry's generic "link". A weak
 * password answers `Validation.Failed` and reads the policy's own `detail`.
 */
const RESET_MESSAGES: FailureMessageRegistry = {
  [ErrorCode.AUTH_TOKEN_INVALID]: () => EXPIRED_LINK_MESSAGE,
  [ErrorCode.AUTH_TOKEN_EXPIRED]: () => EXPIRED_LINK_MESSAGE,
};

/**
 * The screen's only field-level rule, and the one the oracle's local check
 * expressed: without it an empty password would POST, the server would fail
 * `ResetPasswordAsync` and answer a 400 `Validation.Failed` quoting the password
 * policy — a lecture about length to a user who typed nothing.
 *
 * `confirmPassword` carries NO rule of its own on purpose: an empty confirmation
 * against a typed password is a genuine mismatch, and the mismatch guard below
 * already says so in the form-level banner. A per-field message here would
 * report the same fault twice, in two different places.
 */
const resetPasswordSchema = z.object({
  newPassword: z.string().min(1, "New password is required"),
  confirmPassword: z.string(),
});

export interface ResetPasswordFormProps {
  /** The `email` query parameter — `undefined` when the reset link omits it. */
  readonly email?: string;
  /** The `token` query parameter — `undefined` when the reset link omits it. */
  readonly token?: string;
}

/**
 * The reset itself: the two password fields, the banner and the submit, on the
 * shared form shell. It owns the whole request — schema, submit and pending
 * state all live in the `useAppForm` instance — so the card around it stays a
 * layout component that knows nothing about the form.
 *
 * `testIdPrefix="reset-password"` reproduces every id the suites select
 * (`reset-password-form`, `-new-password`, `-new-password-error`, `-error`,
 * `-submit`) by derivation. The single exception is the confirmation control,
 * whose id predates the convention — see the `testId` note below.
 */
function ResetForm({ email, token }: ResetPasswordFormProps) {
  const { sdk } = useRouteContext({ from: "__root__" });
  const navigate = useNavigate();
  const [formError, setFormError] = useState<string | null>(null);
  const [failure, setFailure] = useState<unknown>(null);
  const failureMessage: string | null = useFailureMessage(failure, {
    messages: RESET_MESSAGES,
    fallback: GENERIC_FAILURE_MESSAGE,
  });

  const form = useAppForm({
    schema: resetPasswordSchema,
    defaultValues: { newPassword: "", confirmPassword: "" },
    // The no-mutation escape hatch: the guards, the clear and the status-code
    // narrowing all have to stay on this one path, in this order — see the
    // header note on why the built-in mutation/server-error split cannot serve
    // this endpoint.
    onSubmit: async (values): Promise<void> => {
      // The oracle's `IsNullOrEmpty(Token) || IsNullOrEmpty(Email)` — an empty
      // string is a missing one, so `?token=` never reaches the endpoint. Checked
      // before the mismatch guard, matching the oracle's order, and narrowing both
      // to `string` for the request below without a cast.
      if (email === undefined || email === "" || token === undefined || token === "") {
        setFailure(null);
        setFormError(INVALID_LINK_MESSAGE);
        return;
      }

      if (values.newPassword !== values.confirmPassword) {
        setFailure(null);
        setFormError(PASSWORD_MISMATCH_MESSAGE);
        return;
      }

      // The oracle's `_error = null;` immediately before the call: a stale "link
      // expired" — or "passwords do not match" — banner sitting above a
      // successful reset would be a lie. It clears HERE rather than at the top of
      // the callback so a guard's own message survives the submit that produced it.
      setFormError(null);
      setFailure(null);

      try {
        await accountResetPassword({
          client: sdk.client,
          body: { email, token, newPassword: values.newPassword },
        });
      } catch (error: unknown) {
        setFailure(error);
        return;
      }

      // `href` (a raw location) rather than `to` + `search`: /login's
      // `validateSearch` is owned by the in-flight Login task and this screen
      // must not couple to it (bd memory
      // `tanstack-router-redirect-to-an-unregistered-route-use-href-not-to`).
      // Reached only from the end of the successful path, never from a guard —
      // an early `return` above resolves this callback just as normally as a
      // completed reset does, so `onSuccess` could not tell them apart.
      void navigate({ href: "/login?message=password_reset" });
    },
  });

  return (
    <AppForm form={form} testIdPrefix="reset-password" serverError={formError ?? failureMessage}>
      <FormError />

      <form.AppField name="newPassword">
        {(field) => <field.PasswordField label="New password" />}
      </form.AppField>

      {/* `newPassword` kebab-derives to `reset-password-new-password` (and its
          message to `-error`), which is what the suites already select, so it
          needs no override. `confirmPassword` would derive to
          `reset-password-confirm-password`, so its `testId` IS load-bearing: the
          E2E suite fills `reset-password-confirm`. */}
      <form.AppField name="confirmPassword">
        {(field) => (
          <field.PasswordField label="Confirm new password" testId="reset-password-confirm" />
        )}
      </form.AppField>

      <SubmitButton pendingLabel="Resetting...">Reset password</SubmitButton>
    </AppForm>
  );
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1">
      <Text as="h2" variant="subheading" color="onCard">
        Reset your password
      </Text>
      <MutedText>Enter your new password below.</MutedText>
    </div>
  );
}

/** The oracle's `BbCardFooter`. */
function BackToSignIn() {
  return (
    <div className="text-center w-full">
      <a href={toAppHref("/login")} className="text-sm text-muted-foreground hover:text-foreground">
        Back to sign in
      </a>
    </div>
  );
}

export function ResetPasswordForm({ email, token }: ResetPasswordFormProps): ReactNode {
  return (
    <Card>
      <CardHeading />
      <ResetForm email={email} token={token} />
      <BackToSignIn />
    </Card>
  );
}

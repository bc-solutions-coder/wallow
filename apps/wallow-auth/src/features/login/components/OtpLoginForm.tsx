import { AppForm, type AppFormApi, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useMutation } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { type ReactElement, type ReactNode, useState } from "react";
import { z } from "zod";
import { accountSendOtpMutation, accountVerifyOtpMutation } from "../api";
import { GENERIC_MESSAGE } from "../auth-result";
import {
  OTP_BLANK_CODE_MESSAGE,
  OTP_BLANK_EMAIL_MESSAGE,
  otpWasSent,
  sendOtpFailureMessage,
  verifyOtpFailureMessage,
} from "../otp-result";
import type { LoginPanelProps } from "../panel";

/**
 * The OTP tab of the login screen (Wallow-vec7.3.13 / 2.8c), ported from the
 * oracle's `_activeTab == LoginTab.Otp` branch, its `HandleSendOtp` and its
 * `HandleVerifyOtp` (`api/src/Wallow.Auth/Components/Pages/Login.razor`
 * :139-186, :430-462, :464-500).
 *
 * Per the contract Wallow-vec7.3.11 left on the bead, this panel owns ONLY what the
 * oracle keeps per-tab — its own fields, its own mutations, its own error copy —
 * and NEVER navigates. On a verify response it calls `onAuthResult` with the RAW
 * body and stops: the shell's single `authDispositionOf` (`../auth-result`) owns
 * the MFA branches, the open-redirect guard and the ticket exchange. Three panels
 * re-deriving that table would be three chances to disagree about where a
 * half-authenticated user lands.
 *
 * Testids come verbatim from the oracle: `login-otp-email`, `login-otp-send-submit`,
 * `login-otp-sent`, `login-otp-code`, `login-otp-verify-submit`. The ONE exception is
 * `login-otp-remember-me`, which has no oracle counterpart because the oracle never
 * renders a box on this tab — see `VerifyFields` below (Wallow-98st). Errors go to
 * the shell's ONE shared `login-error` banner via `onError`.
 *
 * ── THE TWO HALVES OF THIS TAB ARE TWO FORMS ─────────────────────────────────
 *
 * SEND: the user types an address and the API emails them a six-digit code. No
 *   `returnUrl`/`clientId` cargo — `SendOtpRequest` is `{ email }` alone. Nothing
 *   is emailed that must resume the OIDC flow: unlike a magic link, the code comes
 *   back to THIS live form, which still has the flow's context in the shell above
 *   it.
 *
 * VERIFY: the user types the code and submits. There is no auto-verify and no
 *   query parameter — which is why `routes/login.tsx` needed no change for this
 *   bead, and why the effect-latch `.3.12` needed has no counterpart here.
 *
 * They are TWO `useAppForm` instances rather than one form with a branch. A single
 * form would need `code` optional-then-required, so the schema would have to encode
 * the phase and the send submit would have to skip validating a field the user has
 * not been shown. `sent` stays panel state: it is the branch BETWEEN the two forms,
 * not a field of either. Both hooks are called from this component (never from the
 * branch that renders them), so the address the user typed survives the swap —
 * unmounting a field resets its meta but leaves its value in the form's store.
 *
 * ── THE EMAIL IS NEVER RE-TYPED ──────────────────────────────────────────────
 *
 * The oracle's OTP tab binds the SHARED `_email` and its code form re-reads it.
 * Here the verify submit reads it back off the SEND form: `ValidateOtpAsync` keys
 * Redis on the address the code was minted for (PasswordlessService.cs:161), so the
 * two halves MUST agree, and the code form deliberately gives the user nothing to
 * disagree with.
 *
 * ── WHY BOTH FORMS RUN THE FORMS PACKAGE "SIDEWAYS" ──────────────────────────
 *
 * Both take the plain-`onSubmit` escape hatch rather than handing `useAppForm` a
 * generated mutation, for `MfaChallengeForm`'s two reasons: `splitServerError`
 * reads RFC 7807 members and these endpoints answer with a bare
 * `{ succeeded, error }` body, so only `../otp-result` can tell their rejections
 * apart; and each blank-input guard reports into the SHELL's banner, which a zod
 * rule could not do — it would abort `handleSubmit` before the callback ran. Both
 * schemas below are therefore rule-free, and this panel renders no `FormError`: it
 * has no banner of its own to render one in.
 */

/** RULE-FREE on purpose — see the header. Here for the value type alone. */
const sendSchema = z.object({ email: z.string() });

/** RULE-FREE on purpose — see the header. Here for the value types alone. */
const verifySchema = z.object({ code: z.string(), rememberMe: z.boolean() });

type SendValues = z.infer<typeof sendSchema>;
type VerifyValues = z.infer<typeof verifySchema>;

const NO_EMAIL: SendValues = { email: "" };

/** Unchecked by DEFAULT: a long-lived session is the user's choice, never a screen's. */
const NO_CODE: VerifyValues = { code: "", rememberMe: false };

/** The oracle's email form: one address, one send. */
function SendFields({ form }: { readonly form: AppFormApi<SendValues> }): ReactElement {
  return (
    <AppForm form={form} testIdPrefix="login-otp" className="space-y-4">
      <form.AppField name="email">
        {(field) => <field.TextField label="Email" type="email" placeholder="name@example.com" />}
      </form.AppField>
      {/* The oracle's `Disabled="_isSubmitting"`, now the shell's — one click, one
          code. A second send OVERWRITES the Redis key (PasswordlessService.cs:144),
          silently invalidating the code already sitting in the user's inbox, so the
          impatient user is the one who gets locked out. */}
      <SubmitButton pendingLabel="Sending..." testId="login-otp-send-submit">
        Send code
      </SubmitButton>
    </AppForm>
  );
}

/**
 * The oracle's code form. The `<form>` element itself carries `login-otp-sent` —
 * the oracle's `_otpSent` marker every suite waits on — because the form's
 * presence IS that state; a wrapper whose only job was to hold the id would say
 * the same thing one element lower.
 *
 * The remember-me box (Wallow-98st) is a deliberate divergence from the oracle,
 * which passes `_rememberMe` to `VerifyOtpAsync` while rendering the checkbox only
 * inside the password tab (Login.razor:87-92) and never resetting it in
 * `SwitchTab`. On the oracle's OTP tab the flag is therefore whatever a detour
 * through the password tab left behind: an INVISIBLE control setting the user's
 * session lifetime. This gives the tab a VISIBLE box instead.
 *
 * Its testid carries this tab's `login-otp-*` prefix — derived from the field name,
 * not the password tab's `login-remember-me`: two controls with two independent
 * states must not share one name, or nothing could tell them apart, least of all a
 * test for the leak. It lives on the CODE form because `rememberMe` is consumed by
 * `otp/verify` alone (`SendOtpRequest` has nowhere to put it), so a box on the email
 * form would vanish at the moment it took effect.
 */
function VerifyFields({ form }: { readonly form: AppFormApi<VerifyValues> }): ReactElement {
  return (
    <AppForm form={form} testIdPrefix="login-otp" testId="login-otp-sent" className="space-y-4">
      <form.AppField name="code">
        {(field) => (
          <field.TextField
            label="Enter the 6-digit code sent to your email"
            // The oracle's `Type="InputType.Text"`, NOT number: the code is
            // `ToString("D6")` (PasswordlessService.cs:141), so it is zero-PADDED,
            // and a number input would happily eat the leading zero of "042317".
            // The digits-only keypad travels on `inputMode` instead.
            inputMode="numeric"
            // A one-time code is not a password to be remembered and not a word to
            // be autocorrected; `one-time-code` is what lets the OS offer it from
            // the inbox.
            autoComplete="one-time-code"
            placeholder="000000"
          />
        )}
      </form.AppField>
      <form.AppField name="rememberMe">
        {(field) => <field.CheckboxField label="Remember me" />}
      </form.AppField>
      {/* THE ONE-TIME-USE GUARD. `ValidateOtpAsync` DELETES the code on success
          (PasswordlessService.cs:178), so a double submit redeems a spent code and
          paints "Invalid or expired code" over a sign-in that just succeeded. Same
          hazard `.3.12` hit on the magic-link token, different vector: there an
          effect re-fired, here a user double-clicks. */}
      <SubmitButton pendingLabel="Verifying..." testId="login-otp-verify-submit">
        Verify code
      </SubmitButton>
    </AppForm>
  );
}

export type OtpLoginFormProps = LoginPanelProps;

export function OtpLoginForm({ onAuthResult, onError }: OtpLoginFormProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });
  /**
   * The oracle's `_otpSent`, which flips the email form to the code form. The
   * oracle's `SwitchTab` also resets it (and `_otpCode`, and — in this port —
   * the remember-me box); here that is free, because switching tabs unmounts the
   * whole panel and the shell needs no reset it would otherwise have to grow.
   */
  const [sent, setSent] = useState(false);

  const sendMutation = useMutation(accountSendOtpMutation({ client: sdk.client }));

  const verifyMutation = useMutation(accountVerifyOtpMutation({ client: sdk.client }));

  const sendForm = useAppForm<SendValues>({
    schema: sendSchema,
    defaultValues: NO_EMAIL,
    onSubmit: async (values: SendValues): Promise<void> => {
      // The oracle's `IsNullOrWhiteSpace(_email)` guard — WHITEspace, so "   " is
      // blank. A blank send cannot succeed and would spend rate-limit allowance.
      if (values.email.trim() === "") {
        onError(OTP_BLANK_EMAIL_MESSAGE);
        return;
      }

      // The oracle's `_errorMessage = null` at the top of `HandleSendOtp`: a stale
      // banner hanging over an in-flight retry is a lie about the current attempt.
      // Cleared HERE rather than at the top of the callback, so a guard's own
      // message survives the submit that produced it.
      onError(null);

      let body: unknown;

      try {
        // The generated artifact's REQUEST object, not a bare body: the factory
        // assembles the request itself, and a bare body would send an empty one.
        body = await sendMutation.mutateAsync({ body: { email: values.email } });
      } catch (error: unknown) {
        // The form deliberately stays up — the user's address may simply have been
        // mistyped, and they need somewhere to fix it.
        onError(sendOtpFailureMessage(error));
        return;
      }

      if (!otpWasSent(body)) {
        // Fail closed: a body this screen cannot read is not a sent code, and
        // sending the user to watch an empty inbox is worse than an error.
        onError(GENERIC_MESSAGE);
        return;
      }

      // The oracle's `_otpSent = true`. Reached for an address with NO account too:
      // `SendOtpAsync` returns the identical `200 { succeeded: true }`
      // (PasswordlessService.cs:134-140) precisely so this screen cannot be used to
      // enumerate users, and the screen must stay identical for both (bd memory
      // `anti-enumeration-pattern-for-endpoints-that-must-not`).
      setSent(true);
    },
  });

  const verifyForm = useAppForm<VerifyValues>({
    schema: verifySchema,
    defaultValues: NO_CODE,
    onSubmit: async (values: VerifyValues): Promise<void> => {
      // The oracle's `IsNullOrWhiteSpace(_otpCode)` guard (:471).
      if (values.code.trim() === "") {
        onError(OTP_BLANK_CODE_MESSAGE);
        return;
      }

      // The oracle's `_errorMessage = null` at the top of `HandleVerifyOtp`.
      onError(null);

      let body: unknown;

      try {
        // `rememberMe` is sent EXPLICITLY, never omitted. The field is optional and
        // the endpoint defaults it false (AccountController.cs:895), so `false` and
        // omission buy the same session — but only one of them states on the wire
        // which session the user asked for. This also matches `PasswordLoginForm`.
        // The value read here is this panel's own box, never the password tab's.
        body = await verifyMutation.mutateAsync({
          body: {
            // Read back off the SEND form, never re-typed — see the header.
            email: sendForm.state.values.email,
            code: values.code,
            rememberMe: values.rememberMe,
          },
        });
      } catch (error: unknown) {
        onError(verifyOtpFailureMessage(error));
        return;
      }

      // The RAW body goes up. Resolution is not, on its own, a destination: the
      // shell narrows it and decides (see `../panel`).
      onAuthResult(body);
    },
  });

  return sent ? <VerifyFields form={verifyForm} /> : <SendFields form={sendForm} />;
}

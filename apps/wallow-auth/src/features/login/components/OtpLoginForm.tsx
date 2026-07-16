import { useMutation } from "@tanstack/react-query";
import { type ReactNode, useState } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";
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
 * `login-otp-sent`, `login-otp-code`, `login-otp-verify-submit`. Errors go to the
 * shell's ONE shared `login-error` banner via `onError`.
 *
 * ── THE TWO HALVES OF THIS TAB ───────────────────────────────────────────────
 *
 * SEND: the user types an address, the API emails them a six-digit code, and
 *   `_otpSent` flips the panel to the code form. No `returnUrl`/`clientId` cargo —
 *   `SendOtpRequest` is `{ email }` alone. Nothing is emailed that must resume the
 *   OIDC flow: unlike a magic link, the code comes back to THIS live form, which
 *   still has the flow's context in the shell above it.
 *
 * VERIFY: the user types the code and submits. There is no auto-verify and no
 *   query parameter — which is why `routes/login.tsx` needed no change for this
 *   bead, and why the effect-latch `.3.12` needed has no counterpart here.
 *
 * ── THE EMAIL IS PANEL STATE, NOT A SECOND FIELD ─────────────────────────────
 *
 * The oracle's OTP tab binds the SHARED `_email` and its code form re-reads it. Here
 * the address is captured on send and held in state: `ValidateOtpAsync` keys Redis
 * on the address the code was minted for (PasswordlessService.cs:161), so the two
 * halves MUST agree, and the code form deliberately gives the user nothing to
 * disagree with.
 */

/** The oracle's OTP email `BbInput`. */
function EmailField(props: { readonly value: string; readonly onChange: (v: string) => void }) {
  const { value, onChange } = props;

  return (
    <div className="space-y-2">
      <label className="text-sm font-medium text-foreground" htmlFor="otpEmail">
        Email
      </label>
      <input
        id="otpEmail"
        type="email"
        className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
        placeholder="name@example.com"
        data-testid="login-otp-email"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
    </div>
  );
}

/** The oracle's OTP code `BbInput`, under its "6-digit code" label. */
function CodeField(props: { readonly value: string; readonly onChange: (v: string) => void }) {
  const { value, onChange } = props;

  return (
    <div className="space-y-2">
      <label className="text-sm font-medium text-foreground" htmlFor="otpCode">
        Enter the 6-digit code sent to your email
      </label>
      <input
        id="otpCode"
        // The oracle's `Type="InputType.Text"`, NOT number: the code is
        // `ToString("D6")` (PasswordlessService.cs:141), so it is zero-PADDED, and a
        // number input would happily eat the leading zero of "042317".
        type="text"
        // A one-time code is not a password to be remembered and not a word to be
        // autocorrected; `one-time-code` is what lets the OS offer it from the inbox.
        inputMode="numeric"
        autoComplete="one-time-code"
        className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm text-foreground"
        placeholder="000000"
        data-testid="login-otp-code"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
    </div>
  );
}

/** The oracle's send `BbButton`, with its `Loading`/`Disabled="_isSubmitting"`. */
function SendButton({ pending }: { readonly pending: boolean }) {
  return (
    <button
      type="submit"
      className="w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
      // One click, one code. A second send OVERWRITES the Redis key
      // (PasswordlessService.cs:144), silently invalidating the code already sitting
      // in the user's inbox — so the impatient user is the one who gets locked out.
      disabled={pending}
      data-testid="login-otp-send-submit"
    >
      {pending ? "Sending..." : "Send code"}
    </button>
  );
}

/** The oracle's verify `BbButton`. */
function VerifyButton({ pending }: { readonly pending: boolean }) {
  return (
    <button
      type="submit"
      className="w-full rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
      // THE ONE-TIME-USE GUARD. `ValidateOtpAsync` DELETES the code on success
      // (PasswordlessService.cs:178), so a double submit redeems a spent code and
      // paints "Invalid or expired code" over a sign-in that just succeeded. Same
      // hazard `.3.12` hit on the magic-link token, different vector: there an effect
      // re-fired, here a user double-clicks.
      disabled={pending}
      data-testid="login-otp-verify-submit"
    >
      {pending ? "Verifying..." : "Verify code"}
    </button>
  );
}

export type OtpLoginFormProps = LoginPanelProps;

export function OtpLoginForm({ onAuthResult, onError }: OtpLoginFormProps): ReactNode {
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  /** The oracle's `_otpSent`, which flips the email form to the code form. */
  const [sent, setSent] = useState(false);

  const sendMutation = useMutation({
    // `Promise<unknown>`: the C# endpoint returns an anonymous `Ok(new { … })` with
    // no OpenAPI schema, so there is no generated type to lean on.
    mutationFn: async (address: string): Promise<unknown> =>
      await getWallowAuthSdk().auth.sendOtp({ email: address }),
  });

  const verifyMutation = useMutation({
    mutationFn: async (value: string): Promise<unknown> =>
      // No `rememberMe`, and that is a DISCLOSED DIVERGENCE (see the bead). The
      // oracle passes `_rememberMe` — but that checkbox is rendered only inside the
      // PASSWORD tab (Login.razor:87-92), so on the OTP tab it is whatever a visit to
      // another tab happened to leave behind: a control the user cannot see silently
      // setting their session lifetime. The endpoint defaults it false
      // (AccountController.cs:876), which is the only value the oracle can produce for
      // a user who stayed on this tab. A code mailed to an inbox is not a "trust this
      // device" signal.
      await getWallowAuthSdk().auth.verifyOtp({ email, code: value }),
  });

  const handleSend = (): void => {
    // The oracle's `IsNullOrWhiteSpace(_email)` guard — WHITEspace, so "   " is
    // blank. A blank send cannot succeed and would spend rate-limit allowance.
    if (email.trim() === "") {
      onError(OTP_BLANK_EMAIL_MESSAGE);
      return;
    }

    // The oracle's `_errorMessage = null` at the top of `HandleSendOtp`: a stale
    // banner hanging over an in-flight retry is a lie about the current attempt.
    onError(null);

    sendMutation.mutate(email, {
      onSuccess: (body: unknown) => {
        if (!otpWasSent(body)) {
          // Fail closed: a body this screen cannot read is not a sent code, and
          // sending the user to watch an empty inbox is worse than an error.
          onError(GENERIC_MESSAGE);
          return;
        }

        // The oracle's `_otpSent = true`. Note this is reached for an address with NO
        // account too: `SendOtpAsync` returns the identical `200 { succeeded: true }`
        // (PasswordlessService.cs:134-140) precisely so this screen cannot be used to
        // enumerate users, and the screen must stay identical for both (bd memory
        // `anti-enumeration-pattern-for-endpoints-that-must-not`).
        setSent(true);
      },
      onError: (cause: unknown) => {
        // The form deliberately stays up — the user's address may simply have been
        // mistyped, and they need somewhere to fix it.
        onError(sendOtpFailureMessage(cause));
      },
    });
  };

  const handleVerify = (): void => {
    // The oracle's `IsNullOrWhiteSpace(_otpCode)` guard (:471).
    if (code.trim() === "") {
      onError(OTP_BLANK_CODE_MESSAGE);
      return;
    }

    // The oracle's `_errorMessage = null` at the top of `HandleVerifyOtp`.
    onError(null);

    verifyMutation.mutate(code, {
      // The RAW body goes up. Resolution is not, on its own, a destination: the shell
      // narrows it and decides (see `../panel`).
      onSuccess: onAuthResult,
      onError: (cause: unknown) => {
        onError(verifyOtpFailureMessage(cause));
      },
    });
  };

  if (sent) {
    // The oracle's `SwitchTab` also resets `_otpSent` and `_otpCode`. Here that is
    // free: both are panel-local state and switching tabs unmounts the panel, so the
    // shell needs no reset it would otherwise have to grow.
    return (
      <form
        className="space-y-4"
        onSubmit={(e) => {
          e.preventDefault();
          e.stopPropagation();
          handleVerify();
        }}
      >
        <div className="space-y-4" data-testid="login-otp-sent">
          <CodeField value={code} onChange={setCode} />
          <VerifyButton pending={verifyMutation.isPending} />
        </div>
      </form>
    );
  }

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        handleSend();
      }}
    >
      <EmailField value={email} onChange={setEmail} />
      <SendButton pending={sendMutation.isPending} />
    </form>
  );
}

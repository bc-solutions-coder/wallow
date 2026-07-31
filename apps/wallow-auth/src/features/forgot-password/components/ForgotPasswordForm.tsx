import { AppForm, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { Card, MutedText, Text } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";
import { z } from "zod";
import { accountForgotPassword } from "../api";
import { toAppHref } from "@shared/lib/base-path";

/**
 * ForgotPassword screen (Wallow-vec7.3.1).
 *
 * ANTI-ENUMERATION IS THE WHOLE POINT OF THIS SCREEN, and it is why this file
 * deliberately breaks two app-wide conventions. Read this before "fixing" it:
 *
 *  1. **The rejection is swallowed, not surfaced.** Every other form in this app
 *     renders the failure in a `{page}-error` block; here, a failure that only
 *     appears for *some* addresses tells the caller which addresses are real —
 *     exactly what the endpoint exists to hide. So the submit renders the same
 *     confirmation whether the backend accepts, 404s, rate-limits, or 500s.
 *  2. **There is no `forgot-password-error` testid**, in the oracle or here. Its
 *     absence is asserted by the spec. Do not copy an error block in from a
 *     sibling screen.
 *
 * This behaviour is deliberate: the acceptance criterion ("success message shown
 * regardless of backend outcome") is the anti-enumeration fix, not a porting
 * mistake.
 *
 * Testids come verbatim from the oracle (scout inventory on Wallow-vec7.3):
 * `forgot-password-email`, `forgot-password-submit`, `forgot-password-success`.
 * The required-field message uses the app's `{page}-{element}-error` convention
 * (`forgot-password-email-error`) — client-side only, it says nothing about the
 * backend and so leaks nothing. The "Back to sign in" footer link ships without
 * a testid in the oracle and keeps it that way.
 *
 * The API is reached through the GENERATED operation, bound to the request's own
 * SDK instance off the router context — there is no app-level facade or client
 * singleton to route through (Wallow-pu6a.5.5).
 *
 * The request half now runs on `@bc-solutions-coder/forms` (Wallow-ov6w.3.1).
 * Every testid above is DERIVED from the shell's `testIdPrefix` rather than
 * written out, and the derivation reproduces all four of them exactly — see
 * `RequestResetForm`.
 */

/**
 * The required-field rule, and the only validation this screen has ever done:
 * the oracle's `if (string.IsNullOrWhiteSpace(_email)) return;`. No format
 * check — the oracle does none, so neither does this port.
 *
 * `.trim()` here makes `"   "` fail the `min(1)`, which is the whitespace-only
 * guard. It does NOT trim the value the submit callback receives: TanStack's
 * standard-schema adapter reads only the issue list off a validation result and
 * discards the parsed output, so `form.state.values` stays raw. The submit below
 * therefore still trims by hand before the address goes out.
 */
const forgotPasswordSchema = z.object({
  email: z.string().trim().min(1, "Email is required"),
});

/**
 * The one message this screen ever shows after a submit. It is a pure constant:
 * it interpolates neither the address nor the backend's answer, which is what
 * makes the accepted and rejected branches byte-identical to anyone diffing the
 * page. Keep the copy conditional ("if an account exists") — "we've sent you a
 * link" would assert that the account exists and undo the whole screen.
 */
function SubmittedConfirmation() {
  return (
    <div
      className="rounded-md border border-border p-4 space-y-1"
      data-testid="forgot-password-success"
    >
      <Text as="p" variant="bodySm" weight="medium">
        Check your email
      </Text>
      <MutedText>
        If an account exists with that email, we&apos;ve sent a password reset link.
      </MutedText>
    </div>
  );
}

/**
 * The request half of the screen: the email field plus the submit, on the shared
 * form shell. It owns the whole request — schema, mutation and pending state all
 * live in the `useAppForm` instance — and tells the parent only that a submit
 * completed, which is the one bit the card body's swap turns on.
 *
 * `testIdPrefix="forgot-password"` reproduces every id the suites select
 * (`forgot-password-form`, `-email`, `-email-error`, `-submit`) by derivation,
 * so no `testId` override is needed on this screen. The prefix is the only place
 * they are written down now; changing it moves all four at once.
 *
 * Deliberately NO `<FormError />`: the submit swallows its rejection (see the
 * anti-enumeration note above), so there is nothing for a banner to say, and the
 * spec asserts `forgot-password-error` never appears.
 */
function RequestResetForm(props: { readonly onSubmitted: () => void }) {
  const { onSubmitted } = props;
  const { sdk } = useRouteContext({ from: "__root__" });

  const form = useAppForm({
    schema: forgotPasswordSchema,
    defaultValues: { email: "" },
    // The no-mutation escape hatch: this screen cannot use the built-in
    // mutation/server-error split, because a failure must never reach the page.
    onSubmit: async (values): Promise<void> => {
      try {
        // Trimmed HERE, not by the schema — see `forgotPasswordSchema`.
        await accountForgotPassword({ client: sdk.client, body: { email: values.email.trim() } });
      } catch {
        // Swallowed deliberately — see the anti-enumeration note above. The
        // reason never escapes this callback, so the mutation cannot enter an
        // error state and there is no error surface for a branch to render.
      }
    },
    onSuccess: onSubmitted,
  });

  return (
    <AppForm form={form} testIdPrefix="forgot-password">
      {/* Left at the default `type="text"`, mirroring the oracle's untyped
          `BbInput`. `type="email"` would have the browser strip surrounding
          whitespace under the value sanitisation algorithm, quietly doing the
          blank guard's job for it; the guard is specified against
          `IsNullOrWhiteSpace` and is kept honest here. */}
      <form.AppField name="email">
        {(field) => <field.TextField label="Email" placeholder="you@example.com" />}
      </form.AppField>

      <SubmitButton pendingLabel="Sending...">Send reset link</SubmitButton>
    </AppForm>
  );
}

/** The card heading, mirroring the oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1">
      <Text as="h2" variant="subheading" color="onCard">
        Forgot your password?
      </Text>
      <MutedText>Enter your email address and we&apos;ll send you a reset link.</MutedText>
    </div>
  );
}

/** The oracle's `BbCardFooter` — shown in both states, so it cannot distinguish them. */
function BackToSignIn() {
  return (
    <div className="text-center w-full">
      <a href={toAppHref("/login")} className="text-sm text-muted-foreground hover:text-foreground">
        Back to sign in
      </a>
    </div>
  );
}

export function ForgotPasswordForm(): ReactNode {
  // `_submitted` in the oracle: it swaps the whole card body, so the form goes
  // away and the confirmation is all that is left to read. This swap is the
  // screen's own state, not the form's — the shell knows nothing about it.
  const [submitted, setSubmitted] = useState(false);

  return (
    <Card>
      <CardHeading />
      {submitted ? (
        <SubmittedConfirmation />
      ) : (
        <RequestResetForm
          onSubmitted={() => {
            setSubmitted(true);
          }}
        />
      )}
      <BackToSignIn />
    </Card>
  );
}

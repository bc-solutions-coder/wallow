import { Button, Card, CardTitle, Field, Input, Label } from "@bc-solutions-coder/ui";
import { useForm } from "@tanstack/react-form";
import { useMutation } from "@tanstack/react-query";
import { type ReactNode, useState } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";

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
 * The API is reached through `getWallowAuthSdk()`, never `@bc-solutions-coder/sdk`
 * directly — that facade is this app's only permitted importer of the SDK.
 */

/**
 * Presentational email field, extracted so the form's render-prop tree stays
 * within the repo's JSX nesting budget. `error` is the first validator message
 * (undefined when the field is valid).
 *
 * The input is a plain text field, mirroring the oracle's untyped `BbInput`.
 * `type="email"` would have the browser strip surrounding whitespace under the
 * value sanitisation algorithm, quietly doing the blank guard's job for it; the
 * guard is specified against `IsNullOrWhiteSpace` and is kept honest here.
 */
function EmailField(props: {
  readonly value: string;
  readonly error: string | undefined;
  readonly onChange: (value: string) => void;
}) {
  const { value, error, onChange } = props;

  return (
    <Field>
      <Label htmlFor="forgot-email">Email</Label>
      <Input
        id="forgot-email"
        type="text"
        placeholder="you@example.com"
        data-testid="forgot-password-email"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
      {error === undefined ? null : (
        <span className="text-sm text-destructive" data-testid="forgot-password-email-error">
          {error}
        </span>
      )}
    </Field>
  );
}

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
      <p className="text-sm font-medium text-foreground">Check your email</p>
      <p className="text-sm text-muted-foreground">
        If an account exists with that email, we&apos;ve sent a password reset link.
      </p>
    </div>
  );
}

/**
 * The request half of the screen: the email field plus the submit. It owns its
 * own `useForm` and hands the caller a validated, trimmed address — so the form
 * element is this component's root and the JSX nesting stays within the repo's
 * budget, the same shape wallow-web's canonical create-form template uses.
 */
function RequestResetForm(props: {
  readonly pending: boolean;
  readonly onSubmit: (email: string) => void;
}) {
  const { pending, onSubmit } = props;

  const form = useForm({
    defaultValues: { email: "" },
    onSubmit: ({ value }) => {
      onSubmit(value.email.trim());
    },
  });

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        void form.handleSubmit();
      }}
    >
      <form.Field
        name="email"
        validators={{
          // The oracle's `if (string.IsNullOrWhiteSpace(_email)) return;` — a
          // blank submit never reaches the network. Required-only: the oracle
          // does no format check, so neither does this port.
          onSubmit: ({ value }) => (value.trim() ? undefined : "Email is required"),
        }}
      >
        {(field) => (
          <EmailField
            value={field.state.value}
            error={field.state.meta.errors[0]}
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <Button type="submit" disabled={pending} data-testid="forgot-password-submit">
        {pending ? "Sending..." : "Send reset link"}
      </Button>
    </form>
  );
}

/** The card heading, mirroring the oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1">
      <CardTitle>Forgot your password?</CardTitle>
      <p className="text-sm text-muted-foreground">
        Enter your email address and we&apos;ll send you a reset link.
      </p>
    </div>
  );
}

/** The oracle's `BbCardFooter` — shown in both states, so it cannot distinguish them. */
function BackToSignIn() {
  return (
    <div className="text-center w-full">
      <a href="/login" className="text-sm text-muted-foreground hover:text-foreground">
        Back to sign in
      </a>
    </div>
  );
}

export function ForgotPasswordForm(): ReactNode {
  const [submitted, setSubmitted] = useState(false);

  const mutation = useMutation({
    mutationFn: async (email: string): Promise<void> => {
      try {
        await getWallowAuthSdk().auth.forgotPassword({ email });
      } catch {
        // Swallowed deliberately — see the anti-enumeration note above. The
        // reason never escapes this function, so the mutation cannot enter an
        // error state and there is no error surface for a branch to render.
      }
    },
  });

  // `_submitted` in the oracle: it swaps the whole card body, so the form goes
  // away and the confirmation is all that is left to read.
  const handleSubmit = (email: string): void => {
    mutation.mutate(email, {
      onSuccess: () => {
        setSubmitted(true);
      },
    });
  };

  return (
    <Card>
      <CardHeading />
      {submitted ? (
        <SubmittedConfirmation />
      ) : (
        <RequestResetForm pending={mutation.isPending} onSubmit={handleSubmit} />
      )}
      <BackToSignIn />
    </Card>
  );
}

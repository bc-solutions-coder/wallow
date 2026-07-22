import { Button, Card, CardTitle, ErrorBanner, Field, Input, Label } from "@bc-solutions-coder/ui";
import { useForm } from "@tanstack/react-form";
import { useMutation } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";

import { getWallowAuthSdk } from "../../../lib/wallow-auth-sdk";

/**
 * The ResetPassword screen (Wallow-vec7.3.2), ported from the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/ResetPassword.razor`.
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
 * The API is reached through `getWallowAuthSdk()`, never `@bc-solutions-coder/sdk`
 * directly — that facade is this app's only permitted importer of the SDK.
 *
 * ── WHY THE ORACLE'S ERROR SWITCH IS NOT PORTED LITERALLY ─────────────────────
 *
 * The oracle switches its message on `result.Error`:
 *
 *     "invalid_token" => "This reset link is invalid or has expired..."
 *     _               => "Failed to reset password. Please try again."
 *
 * That string does not survive the TS seam. `AccountController.ResetPassword`
 * (api/.../Controllers/AccountController.cs:771-794) returns its failures as
 * `BadRequest(new { succeeded = false, error = "invalid_token" })` — a 400 whose
 * body is a bare anon object, NOT RFC 7807 problem details. Blazor's
 * `AuthApiClient` reads that body back into an `AuthResponse` and keeps the
 * string; `unwrap()` THROWS on any non-2xx, and `toWallowError()`
 * (packages/sdk/src/auth-client.ts:257-280) builds its `code` from
 * `extensions.code` ?? `code` only — it never reads a top-level `error`. So the
 * screen receives `WallowError{ code: "UNKNOWN", title: "Unknown error" }` and
 * the reason is LOST (bd memory `wallow-auth-auth-client-ts-wallowerror-code-loss`).
 *
 * What survives is the HTTP status, and here that is enough: this endpoint has
 * exactly two failure returns and BOTH are `400 + error: "invalid_token"`
 * (unknown email, and a rejected `ResetPasswordAsync`). A 400 from this endpoint
 * therefore *means* invalid_token, so the oracle's two branches map onto status
 * with no loss of user-visible behaviour. Narrowing is STRUCTURAL rather than
 * `instanceof WallowError`, because that class is exported from the SDK's
 * `./server` entry and screens may not import the SDK at all.
 */

/** The oracle's guard for a link missing either half of its identity. */
const INVALID_LINK_MESSAGE = "Invalid reset link. Please request a new password reset.";

/** The oracle's client-side confirmation guard. */
const PASSWORD_MISMATCH_MESSAGE = "Passwords do not match.";

/** The oracle's `"invalid_token" =>` branch, reached here via HTTP 400. */
const EXPIRED_LINK_MESSAGE = "This reset link is invalid or has expired. Please request a new one.";

/** The oracle's `_ =>` branch: any other failure, including a network-level one. */
const GENERIC_FAILURE_MESSAGE = "Failed to reset password. Please try again.";

/**
 * The only failure status this endpoint distinguishes. Both of its failure
 * returns are `400 + error: "invalid_token"`, so a 400 from it *means* the link
 * is bad — see the seam note above.
 */
const INVALID_TOKEN_STATUS = 400;

/** The request the endpoint takes, once both halves of the link are known good. */
interface ResetPasswordRequest {
  readonly email: string;
  readonly token: string;
  readonly newPassword: string;
}

/**
 * Map a rejection onto one of the oracle's two messages by HTTP status — see the
 * seam note above for why the reason string cannot be read instead.
 *
 * Narrowed structurally and defensively: a network-level rejection carries no
 * `status` at all, and must fall through to the generic message rather than
 * throw or claim the link is bad.
 */
function resetFailureMessage(cause: unknown): string {
  if (typeof cause === "object" && cause !== null && "status" in cause) {
    const status: unknown = (cause as { readonly status: unknown }).status;

    if (status === INVALID_TOKEN_STATUS) {
      return EXPIRED_LINK_MESSAGE;
    }
  }

  return GENERIC_FAILURE_MESSAGE;
}

/**
 * A masked password field. `type="password"` mirrors the oracle and is pinned by
 * the spec — a reset form that echoed the new password would be a real
 * regression. `errorTestId` is omitted for fields that carry no validator, so no
 * empty error slot is rendered for them.
 */
function PasswordField(props: {
  readonly id: string;
  readonly label: string;
  readonly testId: string;
  readonly errorTestId?: string;
  readonly value: string;
  readonly error?: string;
  readonly onChange: (value: string) => void;
}) {
  const { id, label, testId, errorTestId, value, error, onChange } = props;

  return (
    <Field>
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="password"
        data-testid={testId}
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
      {error === undefined || errorTestId === undefined ? null : (
        <span className="text-sm text-destructive" data-testid={errorTestId}>
          {error}
        </span>
      )}
    </Field>
  );
}

/**
 * The two password fields plus the submit. Owns its own `useForm` and hands the
 * caller the raw pair, so the form element is this component's root and the JSX
 * nesting stays within the repo's budget — the same shape the sibling
 * ForgotPassword port and wallow-web's canonical create-form template use.
 */
function ResetPasswordFields(props: {
  readonly pending: boolean;
  readonly onSubmit: (newPassword: string, confirmPassword: string) => void;
}) {
  const { pending, onSubmit } = props;

  const form = useForm({
    defaultValues: { newPassword: "", confirmPassword: "" },
    onSubmit: ({ value }) => {
      onSubmit(value.newPassword, value.confirmPassword);
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
        name="newPassword"
        validators={{
          // DELIBERATE DEVIATION FROM THE ORACLE, flagged on the bead. Blazor
          // compares "" to "", finds them equal, and POSTs an empty password;
          // the server then fails and returns 400 invalid_token — so a user who
          // typed nothing is told their *link* expired. That is actively
          // misleading. This keeps the empty case local, and matches the
          // `{page}-{element}-error` convention the sibling ForgotPassword port
          // set. No confirm-side validator: an empty confirmation against a
          // typed password is a genuine mismatch and the oracle's own guard
          // already says so.
          onSubmit: ({ value }) => (value === "" ? "New password is required" : undefined),
        }}
      >
        {(field) => (
          <PasswordField
            id="new-password"
            label="New password"
            testId="reset-password-new-password"
            errorTestId="reset-password-new-password-error"
            value={field.state.value}
            error={field.state.meta.errors[0]}
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="confirmPassword">
        {(field) => (
          <PasswordField
            id="confirm-password"
            label="Confirm new password"
            testId="reset-password-confirm"
            value={field.state.value}
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <Button type="submit" disabled={pending} data-testid="reset-password-submit">
        {pending ? "Resetting..." : "Reset password"}
      </Button>
    </form>
  );
}

/** The oracle's `BbCardHeader`. */
function CardHeading() {
  return (
    <div className="space-y-1">
      <CardTitle>Reset your password</CardTitle>
      <p className="text-sm text-muted-foreground">Enter your new password below.</p>
    </div>
  );
}

/** The oracle's `BbCardFooter`. */
function BackToSignIn() {
  return (
    <div className="text-center w-full">
      <a href="/login" className="text-sm text-muted-foreground hover:text-foreground">
        Back to sign in
      </a>
    </div>
  );
}

export interface ResetPasswordFormProps {
  /** The `email` query parameter — `undefined` when the reset link omits it. */
  readonly email?: string;
  /** The `token` query parameter — `undefined` when the reset link omits it. */
  readonly token?: string;
}

export function ResetPasswordForm({ email, token }: ResetPasswordFormProps): ReactNode {
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async (request: ResetPasswordRequest): Promise<void> => {
      await getWallowAuthSdk().auth.resetPassword(request);
    },
  });

  const handleSubmit = (newPassword: string, confirmPassword: string): void => {
    // The oracle's `IsNullOrEmpty(Token) || IsNullOrEmpty(Email)` — an empty
    // string is a missing one, so `?token=` never reaches the endpoint. Checked
    // before the mismatch guard, matching the oracle's order, and narrowing both
    // to `string` for the request below without a cast.
    if (email === undefined || email === "" || token === undefined || token === "") {
      setError(INVALID_LINK_MESSAGE);
      return;
    }

    if (newPassword !== confirmPassword) {
      setError(PASSWORD_MISMATCH_MESSAGE);
      return;
    }

    // The oracle's `_error = null;` immediately before the call: a stale "link
    // expired" banner sitting above a successful reset would be a lie.
    setError(null);

    mutation.mutate(
      { email, token, newPassword },
      {
        onSuccess: () => {
          // `href` (a raw location) rather than `to` + `search`: /login's
          // `validateSearch` is owned by the in-flight Login task and this
          // screen must not couple to it (bd memory
          // `tanstack-router-redirect-to-an-unregistered-route-use-href-not-to`).
          void navigate({ href: "/login?message=password_reset" });
        },
        onError: (cause: unknown) => {
          setError(resetFailureMessage(cause));
        },
      },
    );
  };

  return (
    <Card>
      <CardHeading />
      {error === null ? null : (
        <ErrorBanner data-testid="reset-password-error">{error}</ErrorBanner>
      )}
      <ResetPasswordFields pending={mutation.isPending} onSubmit={handleSubmit} />
      <BackToSignIn />
    </Card>
  );
}

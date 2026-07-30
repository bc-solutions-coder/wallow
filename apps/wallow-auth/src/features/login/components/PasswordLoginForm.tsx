import { Button, Checkbox, Field, Input, Label } from "@bc-solutions-coder/ui";
import { useMutation } from "@bc-solutions-coder/query";
import { useRouteContext } from "@tanstack/react-router";
import { type ReactNode, useState } from "react";
import { accountLoginMutation } from "../api";
import { BLANK_CREDENTIALS_MESSAGE, loginFailureMessage } from "../auth-result";
import type { LoginPanelProps } from "../panel";
import { toAppHref } from "../../../lib/base-path";

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
 * `login-submit`.
 *
 * The API is reached through the request-scoped SDK on the router context
 * (`useRouteContext({ from: "__root__" })`), through the GENERATED
 * `{operation}Mutation()` factory — the app hand-rolls no `mutationFn` of its own
 * (Wallow-x4qn.9.3).
 */

/** The oracle's email `BbInput`. */
function EmailField(props: { readonly value: string; readonly onChange: (v: string) => void }) {
  const { value, onChange } = props;

  return (
    <Field>
      <Label htmlFor="email">Email</Label>
      <Input
        id="email"
        type="email"
        placeholder="name@example.com"
        data-testid="login-email"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
    </Field>
  );
}

/**
 * The oracle's password label row: the label and the forgot-password escape hatch
 * share a line. The link is a plain `<a>`, matching the oracle and the sibling
 * ports' footer links.
 */
function PasswordLabelRow() {
  return (
    <div className="flex items-center justify-between">
      <Label htmlFor="password">Password</Label>
      <a
        href={toAppHref("/forgot-password")}
        className="text-sm text-muted-foreground hover:text-primary"
        data-testid="login-forgot-password"
      >
        Forgot password?
      </a>
    </div>
  );
}

/** The oracle's password `BbInput`, under the label row above. */
function PasswordField(props: { readonly value: string; readonly onChange: (v: string) => void }) {
  const { value, onChange } = props;

  return (
    <Field>
      <PasswordLabelRow />
      <Input
        id="password"
        type="password"
        placeholder="Enter your password"
        data-testid="login-password"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
    </Field>
  );
}

/**
 * The oracle's `BbCheckbox` + label, on the catalog's `Checkbox`
 * (Wallow-m5aq.5.2). The testid sits on `Checkbox.Root` — the element that
 * carries the role, the name and the state — not on the hidden `<input>` Base UI
 * renders beside it for form submission.
 *
 * The `id` is threaded onto the Root rather than left to the hidden input: the
 * Root is a `<span role="checkbox">`, which a `<label htmlFor>` cannot name on
 * its own, so Base UI matches the label to the id and stamps the pairing itself.
 */
function RememberMeField(props: {
  readonly checked: boolean;
  readonly onChange: (v: boolean) => void;
}) {
  const { checked, onChange } = props;

  return (
    <div className="flex items-center space-x-2">
      <Checkbox.Root
        id="rememberMe"
        data-testid="login-remember-me"
        checked={checked}
        onCheckedChange={onChange}
      >
        <Checkbox.Indicator>✓</Checkbox.Indicator>
      </Checkbox.Root>
      <label className="text-sm font-normal text-foreground" htmlFor="rememberMe">
        Remember me
      </label>
    </div>
  );
}

/** The oracle's submit `BbButton`, with its `Loading`/`Disabled="_isSubmitting"`. */
function SubmitButton({ pending }: { readonly pending: boolean }) {
  return (
    <Button
      type="submit"
      // The oracle's `Disabled="_isSubmitting"` — one click, one attempt. A double
      // submit costs the user two of the five tries they get before lockout.
      disabled={pending}
      data-testid="login-submit"
    >
      {pending ? "Signing in..." : "Sign in"}
    </Button>
  );
}

export function PasswordLoginForm({ onAuthResult, onError }: LoginPanelProps): ReactNode {
  const { sdk } = useRouteContext({ from: "__root__" });
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [rememberMe, setRememberMe] = useState(false);

  // The generated factory's response type governs, and the narrowing still
  // belongs to the shell — `onAuthResult` takes the RAW body and `../panel`
  // decides, because three of this endpoint's four outcomes are 200s.
  const mutation = useMutation(accountLoginMutation({ client: sdk.client }));

  const handleSubmit = (): void => {
    // The oracle's `IsNullOrWhiteSpace` guard — note WHITEspace, so "   " is blank.
    // A blank submit cannot succeed and would spend one of the user's attempts
    // against the lockout counter, so it never reaches the API.
    if (email.trim() === "" || password.trim() === "") {
      onError(BLANK_CREDENTIALS_MESSAGE);
      return;
    }

    // The oracle's `_errorMessage = null;` at the top of `HandleLogin`: a stale
    // "invalid password" banner hanging over an in-flight retry is a lie.
    onError(null);

    mutation.mutate(
      // The oracle's `LoginRequest(_email, _password, _rememberMe)`, now spelled as
      // the generated artifact's REQUEST object rather than a bare body: the
      // factory assembles `{ body }`/`{ path }`/`{ query }` itself, and a bare body
      // here would send an empty request.
      { body: { email, password, rememberMe } },
      {
        // Resolution is NOT success here: three of this endpoint's four outcomes
        // arrive as 200 bodies. The shell narrows and branches.
        onSuccess: onAuthResult,
        onError: (cause: unknown) => {
          // The form deliberately stays up — the user has attempts left and no way
          // to spend them if it is gone.
          onError(loginFailureMessage(cause));
        },
      },
    );
  };

  return (
    <form
      className="space-y-4"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        handleSubmit();
      }}
    >
      <EmailField value={email} onChange={setEmail} />
      <PasswordField value={password} onChange={setPassword} />
      <RememberMeField checked={rememberMe} onChange={setRememberMe} />
      <SubmitButton pending={mutation.isPending} />
    </form>
  );
}

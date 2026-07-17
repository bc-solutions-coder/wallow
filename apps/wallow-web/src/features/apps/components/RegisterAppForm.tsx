/**
 * Register-app form (Wallow-8w1h.5.3) — copies the CANONICAL create-form
 * template (`CreateOrganizationForm`): `useForm` (TanStack Form) + `useMutation(
 * registerAppMutation(queryClient))`. On top of the template it adds the
 * behaviors unique to app registration:
 *
 *   - Blazor field remap (RegisterApp.razor / AppRegistrationService.cs):
 *     DisplayName -> clientName, Scopes -> requestedScopes; `clientType`
 *     defaults to "public"; redirect URIs are a newline-separated textarea,
 *     split on `\n` with blank lines dropped.
 *   - Scope multi-select toggle buttons (available: inquiries.read,
 *     inquiries.write, announcements.read, storage.read; default selected:
 *     inquiries.read).
 *   - The ONE-TIME client secret: `AppRegistrationResponse.clientSecret` comes
 *     back ONLY from the register call (GET /apps and GET /apps/{id} carry no
 *     secret), so it is rendered exactly once — in the post-success view, from
 *     `mutation.data`, never persisted beyond it, never re-fetchable. Mirrors
 *     RegisterAppResult.cs / RegisterApp.razor:37-49 ("Save your client secret
 *     now. It will not be shown again.").
 *
 * Testids follow the apps feature's `app-*` convention: `app-display-name`
 * (input), `app-client-type` (select), `app-redirect-uris` (textarea),
 * `app-scope-{scope-dashed}` (toggle buttons), `app-register-submit` (submit),
 * `app-display-name-error` (required-field validation), `app-register-error`
 * (server RFC 7807 ProblemDetails surface), and the one-time success reveal
 * `app-client-secret` + `app-client-secret-copy` + `app-client-id`.
 */
import { useForm } from "@tanstack/react-form";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import type { AppRegistrationResponse, ProblemDetails } from "@bc-solutions-coder/sdk";

import { registerAppMutation } from "../api";

/** Scopes a caller may request, mirroring Blazor RegisterApp.razor's list. */
const AVAILABLE_SCOPES = [
  "inquiries.read",
  "inquiries.write",
  "announcements.read",
  "storage.read",
] as const;

/**
 * Presentational display-name field, extracted so the form's render-prop tree
 * stays within the repo's JSX nesting budget (the template's convention).
 * `error` is the first validator message (undefined when the field is valid).
 */
function DisplayNameField(props: {
  value: string;
  error: string | undefined;
  onChange: (value: string) => void;
}) {
  const { value, error, onChange } = props;
  return (
    <>
      <input
        data-testid="app-display-name"
        value={value}
        onChange={(e) => {
          onChange(e.target.value);
        }}
      />
      {error === undefined ? null : <span data-testid="app-display-name-error">{error}</span>}
    </>
  );
}

/** Public/confidential client-type select (defaults to "public"). */
function ClientTypeField(props: { value: string; onChange: (value: string) => void }) {
  const { value, onChange } = props;
  return (
    <select
      data-testid="app-client-type"
      value={value}
      onChange={(e) => {
        onChange(e.target.value);
      }}
    >
      <option value="public">Public</option>
      <option value="confidential">Confidential</option>
    </select>
  );
}

/** Newline-separated redirect-URIs textarea. */
function RedirectUrisField(props: { value: string; onChange: (value: string) => void }) {
  const { value, onChange } = props;
  return (
    <textarea
      data-testid="app-redirect-uris"
      value={value}
      onChange={(e) => {
        onChange(e.target.value);
      }}
    />
  );
}

/** Scope multi-select toggle buttons; clicking one adds/removes it. */
function ScopeToggles(props: { value: string[]; onChange: (value: string[]) => void }) {
  const { value, onChange } = props;
  return (
    <div>
      {AVAILABLE_SCOPES.map((scope) => {
        const selected = value.includes(scope);
        return (
          <button
            key={scope}
            type="button"
            data-testid={`app-scope-${scope.replaceAll(".", "-")}`}
            aria-pressed={selected}
            onClick={() => {
              onChange(selected ? value.filter((s) => s !== scope) : [...value, scope]);
            }}
          >
            {scope}
          </button>
        );
      })}
    </div>
  );
}

/**
 * Optional branding subsection (Wallow-ffpq.3.6) — the React port of the
 * "Branding" block on Blazor `RegisterApp.razor` (which calls
 * `AppService.UpsertBrandingAsync` with a display name, tagline, and logo file).
 * It lives on the same register-app page, so the branding display-name / tagline
 * / logo inputs are reachable in the form view. Testids follow the apps feature's
 * `app-*` convention. Presentational (uncontrolled) per the epic's reachability
 * bar; the live upsert (`getWallowSdk().apps.upsertBranding`) needs the client id
 * the register call returns and is left as a structural seam here.
 */
function BrandingSection() {
  return (
    <fieldset data-testid="app-branding">
      <legend>Branding (optional)</legend>
      <input data-testid="app-branding-display-name" placeholder="Display name" />
      <input data-testid="app-branding-tagline" placeholder="Tagline" />
      <input data-testid="app-logo-input" type="file" accept="image/*" />
    </fieldset>
  );
}

/** One-time reveal of the returned client id + secret, with a copy affordance. */
function SuccessView(props: { result: AppRegistrationResponse }) {
  const { result } = props;
  return (
    <div data-testid="app-register-success">
      <p>Save your client secret now. It will not be shown again.</p>
      <span data-testid="app-client-id">{result.clientId}</span>
      <span data-testid="app-client-secret">{result.clientSecret}</span>
      <button
        type="button"
        data-testid="app-client-secret-copy"
        onClick={() => {
          void navigator.clipboard.writeText(result.clientSecret);
        }}
      >
        Copy secret
      </button>
    </div>
  );
}

export function RegisterAppForm() {
  const queryClient = useQueryClient();
  const mutation = useMutation(registerAppMutation(queryClient));

  const form = useForm({
    defaultValues: {
      displayName: "",
      clientType: "public",
      redirectUris: "",
      scopes: ["inquiries.read"] as string[],
    },
    onSubmit: ({ value }) => {
      // Fire-and-observe: the mutation captures success/error in its own state
      // (surfaced below), so we drive it with `mutate` rather than awaiting
      // `mutateAsync` — a rejected register must NOT escape the submit handler
      // as an unhandled rejection. The factory's own `onSuccess` invalidates
      // `['apps']`.
      mutation.mutate({
        clientName: value.displayName,
        requestedScopes: value.scopes,
        clientType: value.clientType,
        redirectUris: value.redirectUris
          .split("\n")
          .map((uri) => uri.trim())
          .filter(Boolean),
      });
    },
  });

  // One-time secret: the returned secret lives only in `mutation.data` and the
  // success view below — never copied into long-lived component state.
  if (mutation.isSuccess) {
    return <SuccessView result={mutation.data as AppRegistrationResponse} />;
  }

  return (
    <form
      data-testid="app-register-form"
      onSubmit={(e) => {
        e.preventDefault();
        e.stopPropagation();
        void form.handleSubmit();
      }}
    >
      <form.Field
        name="displayName"
        validators={{
          onSubmit: ({ value }) => (value.trim() ? undefined : "Display name is required"),
        }}
      >
        {(field) => (
          <DisplayNameField
            value={field.state.value}
            error={field.state.meta.errors[0]}
            onChange={field.handleChange}
          />
        )}
      </form.Field>

      <form.Field name="clientType">
        {(field) => <ClientTypeField value={field.state.value} onChange={field.handleChange} />}
      </form.Field>

      <form.Field name="redirectUris">
        {(field) => <RedirectUrisField value={field.state.value} onChange={field.handleChange} />}
      </form.Field>

      <form.Field name="scopes">
        {(field) => <ScopeToggles value={field.state.value} onChange={field.handleChange} />}
      </form.Field>

      <BrandingSection />

      {mutation.isError ? (
        <span data-testid="app-register-error">{(mutation.error as ProblemDetails).detail}</span>
      ) : null}

      <button type="submit" data-testid="app-register-submit">
        Register app
      </button>
    </form>
  );
}

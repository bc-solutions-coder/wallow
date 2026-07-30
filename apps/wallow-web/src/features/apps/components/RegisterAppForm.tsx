/**
 * Register-app form (Wallow-8w1h.5.3, migrated to `@bc-solutions-coder/forms` in
 * Wallow-ov6w.4.2) — one `useAppForm` call holding the zod schema, the GENERATED
 * `appsRegisterMutation({ client })` and the success work, rendered through the
 * shared `AppForm` shell (see `CreateOrganizationForm`, the canonical template).
 *
 * On top of the template it keeps the behaviors unique to app registration:
 *
 *   - Field remap (API request contract): `displayName` -> `clientName`,
 *     `scopes` -> `requestedScopes`; `clientType` defaults to "public"; the two
 *     redirect-URI lists are newline-separated textareas split on `\n` with
 *     blank lines dropped. All of it lives in `toVariables`, which is the one
 *     place the form's values become the request body.
 *   - Scope multi-select toggle buttons. `forms` has no multi-select-toggle
 *     field, so `ScopeToggles` stays hand-rolled on the `AppField` render-prop
 *     escape hatch — still a form field, just not a catalog one.
 *   - The uncontrolled `BrandingSection`, wired to nothing and therefore a plain
 *     child of the shell rather than a field.
 *   - The ONE-TIME client secret: `AppRegistrationResponse.clientSecret` comes
 *     back ONLY from the register call (GET /apps and GET /apps/{id} carry no
 *     secret), so it is rendered exactly once — in the post-success view, never
 *     persisted beyond it, never re-fetchable, with a "Save your client secret
 *     now. It will not be shown again." warning. `useAppForm` owns the mutation
 *     and does not hand the instance back, so the reveal is gated on the
 *     response captured in `onSuccess` rather than on `mutation.isSuccess`. It
 *     REPLACES the form: a live form beside a secret that can never be shown
 *     again would invite a second registration that discards the first secret.
 *
 * Testids follow the apps feature's `app-*` convention. `app-register-form`,
 * `app-register-error` and `app-register-submit` are DERIVED from the shell's
 * `testIdPrefix`; the four catalog fields predate the convention
 * (`app-display-name`, not `app-register-display-name`) so each carries an
 * explicit `testId`, which the catalog also suffixes for its message
 * (`app-display-name-error`, `app-redirect-uris-error`).
 */
import {
  AppForm,
  FormError,
  type SelectFieldOption,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import { useQueryClient } from "@bc-solutions-coder/query";
import { Button, Card, Field, Input, Label, Toggle, ToggleGroup } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";
import { z } from "zod";
import type { AppRegistrationResponse } from "@bc-solutions-coder/sdk";

import { appsRegisterMutation, queriesWithTag } from "../api";

/**
 * Scopes a caller may request: the developer-app scopes plus the OIDC login
 * scopes the reworked AppsController now accepts (`ApiScopes.LoginScopes`). The
 * login scopes have no dot, so their testids keep their raw name
 * (`app-scope-offline_access`).
 */
const AVAILABLE_SCOPES = [
  "inquiries.read",
  "inquiries.write",
  "announcements.read",
  "storage.read",
  "openid",
  "profile",
  "email",
  "offline_access",
] as const;

/** The two client types the API accepts, as catalog-`SelectField` options. */
const CLIENT_TYPE_OPTIONS: readonly SelectFieldOption[] = [
  { value: "public", label: "Public" },
  { value: "confidential", label: "Confidential" },
];

/**
 * The required-display-name rule, carried over verbatim from the hand-written
 * `value.trim() ? undefined : "Display name is required"` validator this form
 * used before the migration — including its message, which the suites assert.
 *
 * `.trim()` is what makes `"   "` fail the `min(1)`. It does NOT trim the value
 * the submit receives: TanStack's standard-schema adapter reads only the issue
 * list off a validation result and discards the parsed output, so
 * `form.state.values` stays raw — which is also what the pre-migration form
 * posted, so the payload is unchanged.
 */
const registerAppSchema = z.object({
  displayName: z.string().trim().min(1, "Display name is required"),
  clientType: z.string(),
  redirectUris: z.string(),
  postLogoutRedirectUris: z.string(),
  scopes: z.array(z.string()),
});

/**
 * A newline-separated textarea as the API's string array: one URI per line,
 * trimmed, with blank lines dropped so a trailing newline does not post `""`.
 */
function toUriList(value: string): string[] {
  return value
    .split("\n")
    .map((uri) => uri.trim())
    .filter(Boolean);
}

/**
 * Scope multi-select toggle buttons; clicking one adds/removes it. The catalog
 * `ToggleGroup` announces the eight buttons as ONE multi-select control and owns
 * the roving focus between them; `multiple` is what keeps every chosen scope
 * pressed instead of releasing the previous one.
 */
function ScopeToggles(props: { value: string[]; onChange: (value: string[]) => void }) {
  const { value, onChange } = props;
  return (
    <ToggleGroup multiple value={value} onValueChange={onChange}>
      {AVAILABLE_SCOPES.map((scope) => (
        <Toggle key={scope} value={scope} data-testid={`app-scope-${scope.replaceAll(".", "-")}`}>
          {scope}
        </Toggle>
      ))}
    </ToggleGroup>
  );
}

/**
 * Optional branding subsection (Wallow-ffpq.3.6) — a "Branding" block that
 * upserts an app display name, tagline, and logo file.
 * It lives on the same register-app page, so the branding display-name / tagline
 * / logo inputs are reachable in the form view. Testids follow the apps feature's
 * `app-*` convention. Presentational (uncontrolled) per the epic's reachability
 * bar; the live upsert (`clientBrandingUpsertBrandingMutation`) needs the client
 * id the register call returns and is left as a structural seam here. It is
 * wired to no form field at all, so it rides the shell as a plain child.
 */
function BrandingSection() {
  return (
    <fieldset data-testid="app-branding">
      {/* The legend names the FIELDSET, not the controls inside it, and a
          placeholder is not a label — it disappears the moment a value is typed.
          So each control carries a real one: the two Base UI-backed `Input`s
          associate through the `Field` row, and the raw file input spells the
          `htmlFor`/`id` pair out because there is no catalog part behind it to
          register. */}
      <legend>Branding (optional)</legend>
      <Field>
        <Label>Display name</Label>
        <Input data-testid="app-branding-display-name" placeholder="Display name" />
      </Field>
      <Field>
        <Label>Tagline</Label>
        <Input data-testid="app-branding-tagline" placeholder="Tagline" />
      </Field>
      <Field>
        <Label htmlFor="app-logo-input-control">Logo</Label>
        <input
          id="app-logo-input-control"
          data-testid="app-logo-input"
          type="file"
          accept="image/*"
        />
      </Field>
    </fieldset>
  );
}

/** One-time reveal of the returned client id + secret, with a copy affordance. */
function SuccessView(props: { result: AppRegistrationResponse }) {
  const { result } = props;
  return (
    <Card data-testid="app-register-success">
      <p>Save your client secret now. It will not be shown again.</p>
      <span data-testid="app-client-id">{result.clientId}</span>
      <span data-testid="app-client-secret">{result.clientSecret}</span>
      <Button
        type="button"
        data-testid="app-client-secret-copy"
        onClick={() => {
          void navigator.clipboard.writeText(result.clientSecret);
        }}
      >
        Copy secret
      </Button>
    </Card>
  );
}

export function RegisterAppForm() {
  // One-time secret: the registration response lives only here and in the
  // success view below — never written to the query cache, never re-fetched.
  // Holding it in the PARENT of the form is what lets the reveal REPLACE the
  // form instead of appearing beside it.
  const [registration, setRegistration] = useState<AppRegistrationResponse | null>(null);

  if (registration !== null) {
    return <SuccessView result={registration} />;
  }

  return (
    <Card>
      <RegisterAppFormFields onRegistered={setRegistration} />
    </Card>
  );
}

/**
 * The form body, split out so the `Card` surface stays a shallow wrapper and the
 * success reveal can unmount the form wholesale.
 */
function RegisterAppFormFields(props: { onRegistered: (result: AppRegistrationResponse) => void }) {
  const { onRegistered } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  const form = useAppForm({
    schema: registerAppSchema,
    defaultValues: {
      displayName: "",
      clientType: "public",
      redirectUris: "",
      postLogoutRedirectUris: "",
      scopes: ["inquiries.read"],
    },
    // The generated factory goes over WHOLE — `useAppForm` infers its `TError`
    // (Wallow-ov6w.2.6), so nothing here has to be destructured or cast.
    mutation: appsRegisterMutation({ client: sdk.client }),
    // The whole request contract in one place: the field remap the API expects,
    // and the newline split the two URI textareas carry.
    toVariables: (values) => ({
      body: {
        clientName: values.displayName,
        requestedScopes: values.scopes,
        clientType: values.clientType,
        redirectUris: toUriList(values.redirectUris),
        postLogoutRedirectUris: toUriList(values.postLogoutRedirectUris),
      },
    }),
    onSuccess: (data) => {
      // Generated keys are flat, so there is no `['apps']` prefix to invalidate
      // by — the Apps tag predicate is the sweep.
      void queryClient.invalidateQueries(queriesWithTag("Apps"));
      // No `form.reset()` here: this hands the page over to the reveal, which
      // unmounts the form and its values with it.
      onRegistered(data);
    },
    fallbackError: "Could not register the app.",
  });

  return (
    <AppForm form={form} testIdPrefix="app-register">
      <form.AppField name="displayName">
        {(field) => <field.TextField label="Display name" testId="app-display-name" />}
      </form.AppField>

      <form.AppField name="clientType">
        {(field) => (
          <field.SelectField
            label="Client type"
            testId="app-client-type"
            options={CLIENT_TYPE_OPTIONS}
          />
        )}
      </form.AppField>

      <form.AppField name="redirectUris">
        {(field) => <field.TextareaField label="Redirect URIs" testId="app-redirect-uris" />}
      </form.AppField>

      <form.AppField name="postLogoutRedirectUris">
        {(field) => (
          <field.TextareaField
            label="Post-logout redirect URIs"
            testId="app-post-logout-redirect-uris"
          />
        )}
      </form.AppField>

      {/* No catalog field is a multi-select toggle, so this one keeps its own
          control on the render-prop escape hatch — still a form field. */}
      <form.AppField name="scopes">
        {(field) => <ScopeToggles value={field.state.value} onChange={field.handleChange} />}
      </form.AppField>

      <BrandingSection />

      <FormError />

      <SubmitButton>Register app</SubmitButton>
    </AppForm>
  );
}

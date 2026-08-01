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
 *   - `BrandingSection` — three REAL fields whose values ride a SECOND request.
 *     Branding is a different endpoint (`POST /v1/identity/apps/{clientId}/branding`,
 *     multipart) keyed on a clientId that first exists in the register RESPONSE,
 *     so it can never be a `toVariables` remap; it is a post-register upsert
 *     fired from the parent instead (Wallow-lrlm.6.2). Before that the three
 *     controls were uncontrolled and bound to nothing, so anything typed into
 *     them was silently discarded on submit — no error, no trace.
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
  errorText,
  type AppFormApi,
  FormError,
  type SelectFieldOption,
  SubmitButton,
  useAppForm,
} from "@bc-solutions-coder/forms";
import { useMutation, useQueryClient } from "@bc-solutions-coder/query";
import { Button, Card, Field, Label, Text, Toggle, ToggleGroup } from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { useState } from "react";
import { z } from "zod";
import type { AppRegistrationResponse } from "@bc-solutions-coder/sdk";

import { appsRegisterMutation, clientBrandingUpsertBrandingMutation, queriesWithTag } from "../api";

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
const registerAppSchema = z
  .object({
    displayName: z.string().trim().min(1, "Display name is required"),
    clientType: z.string(),
    redirectUris: z.string(),
    postLogoutRedirectUris: z.string(),
    scopes: z.array(z.string()),
    // The branding trio. They are NOT part of the register request body (see
    // `toVariables`) — they are the second, multipart request's payload, held
    // as form state so the values survive to `onSuccess` where the clientId
    // they need finally exists.
    brandingDisplayName: z.string(),
    brandingTagline: z.string(),
    brandingLogo: z.instanceof(File).nullable(),
  })
  /*
   * The branding endpoint rejects a blank `DisplayName` with a 400, so a tagline
   * or a logo without one cannot be sent at all. Saying so on the control that
   * is missing beats letting the register succeed and the upsert fail. The issue
   * is pathed at the field so the catalog row renders and associates it; the
   * check runs only once the object itself parses, which is why nothing here
   * re-states the base rules.
   */
  .refine(
    (values) =>
      (values.brandingTagline.trim() === "" && values.brandingLogo === null) ||
      values.brandingDisplayName.trim() !== "",
    {
      path: ["brandingDisplayName"],
      message: "Branding display name is required to save a tagline or logo",
    },
  );

/**
 * The form instance this screen builds — named so `BrandingSection` can take it
 * as a prop rather than nest inside the shell's JSX.
 */
type RegisterAppFormApi = AppFormApi<z.input<typeof registerAppSchema>>;

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
 * upserts an app display name, tagline, and logo file. It lives on the same
 * register-app page, so the three controls are reachable in the form view;
 * testids follow the apps feature's `app-*` convention.
 *
 * All three are REAL fields. They take the `form` rather than rendering as
 * children of the fieldset at the call site because `react/jsx-max-depth` is 2
 * and `pnpm lint` runs `--deny-warnings`: a component per nesting level is the
 * same shape `SelectField`'s portal tree already uses in `packages/forms`. The
 * fieldset stays INSIDE the shell's `<form>`, which the suites assert through
 * `closest("form")`.
 */
function BrandingSection(props: { form: RegisterAppFormApi }) {
  const { form } = props;
  return (
    <fieldset data-testid="app-branding">
      {/* The legend names the FIELDSET, not the controls inside it, and a
          placeholder is not a label — it disappears the moment a value is typed.
          So each control carries a real one: the two catalog `TextField`s
          associate through the ui `Field` row, and the raw file input spells the
          `htmlFor`/`id` pair out because there is no catalog part behind it to
          register. */}
      {/* `variant="body"` overrides `legend`'s catalog default of `caption`: the
          shipped legend is unclassed, so caption would shrink it. */}
      <Text as="legend" variant="body">
        Branding (optional)
      </Text>
      {/* Explicit `testId`s, as mandatory here as on the four fields above: the
          shell's derivation would rename these to `app-register-branding-*` and
          break every suite (and E2E selector) that already names them. */}
      <form.AppField name="brandingDisplayName">
        {(field) => (
          <field.TextField label="Display name" optional testId="app-branding-display-name" />
        )}
      </form.AppField>
      <form.AppField name="brandingTagline">
        {(field) => <field.TextField label="Tagline" optional testId="app-branding-tagline" />}
      </form.AppField>
      <form.AppField name="brandingLogo">
        {(field) => <LogoInput onChange={field.handleChange} />}
      </form.AppField>
    </fieldset>
  );
}

/**
 * The logo file control, on the `AppField` render-prop escape hatch for the same
 * reason `ScopeToggles` is: the catalog is Text/Textarea/Select/Checkbox/Password
 * only, with no file field. A file input stays UNCONTROLLED (no `value`) — a
 * `FileList` cannot be assigned back — so the field holds the chosen `File` and
 * the input reports it.
 */
function LogoInput(props: { onChange: (logo: File | null) => void }) {
  const { onChange } = props;
  return (
    <Field>
      <Label htmlFor="app-logo-input-control">Logo</Label>
      <input
        id="app-logo-input-control"
        data-testid="app-logo-input"
        type="file"
        accept="image/*"
        onChange={(event) => {
          onChange(event.target.files?.[0] ?? null);
        }}
      />
    </Field>
  );
}

/**
 * Register succeeded, the branding upsert did not. Non-blocking by design: the
 * secret above it is minted once and can never be re-shown, so a failed SECOND
 * request must not cost the user the result of the first. The retry re-fires
 * ONLY the upsert — the registration is never re-run.
 */
function BrandingFailure(props: { message: string; pending: boolean; onRetry: () => void }) {
  const { message, pending, onRetry } = props;
  return (
    <div>
      <Text as="p" variant="body" data-testid="app-branding-error">
        {message}
      </Text>
      <Button type="button" data-testid="app-branding-retry" disabled={pending} onClick={onRetry}>
        Retry branding
      </Button>
    </div>
  );
}

interface SuccessViewProps {
  readonly result: AppRegistrationResponse;
  /** The branding upsert's failure text, or `null` when it succeeded or was skipped. */
  readonly brandingError: string | null;
  /** Whether a branding upsert (first attempt or retry) is in flight. */
  readonly brandingPending: boolean;
  readonly onRetryBranding: () => void;
}

/** One-time reveal of the returned client id + secret, with a copy affordance. */
function SuccessView(props: SuccessViewProps) {
  const { result, brandingError, brandingPending, onRetryBranding } = props;
  return (
    <Card data-testid="app-register-success">
      <Text as="p" variant="body">
        Save your client secret now. It will not be shown again.
      </Text>
      <Text as="span" variant="body" data-testid="app-client-id">
        {result.clientId}
      </Text>
      <Text as="span" variant="body" data-testid="app-client-secret">
        {result.clientSecret}
      </Text>
      <Button
        type="button"
        data-testid="app-client-secret-copy"
        onClick={() => {
          void navigator.clipboard.writeText(result.clientSecret);
        }}
      >
        Copy secret
      </Button>
      {brandingError === null ? null : (
        <BrandingFailure
          message={brandingError}
          pending={brandingPending}
          onRetry={onRetryBranding}
        />
      )}
    </Card>
  );
}

/** The branding trio, lifted off the form's values on a successful register. */
interface BrandingValues {
  readonly displayName: string;
  readonly tagline: string;
  readonly logo: File | null;
}

/** The multipart body of the branding upsert, with every blank member omitted. */
interface BrandingUpsertBody {
  readonly DisplayName: string;
  readonly Tagline?: string;
  readonly logo?: File;
}

/** Banner text for a branding upsert that carried no message of its own. */
const BRANDING_FALLBACK_ERROR = "Could not save the app branding.";

/**
 * The branding to upsert, or `null` when the section is PRISTINE.
 *
 * Trimmed rather than dirty-checked: the endpoint 400s on a blank `DisplayName`,
 * so whitespace typed and thought better of has to count as untouched — an
 * unconditional upsert would turn every plain registration into a visible
 * failure.
 */
function toBrandingValues(values: {
  brandingDisplayName: string;
  brandingTagline: string;
  brandingLogo: File | null;
}): BrandingValues | null {
  const pristine: boolean =
    values.brandingDisplayName.trim() === "" &&
    values.brandingTagline.trim() === "" &&
    values.brandingLogo === null;

  return pristine
    ? null
    : {
        displayName: values.brandingDisplayName,
        tagline: values.brandingTagline,
        logo: values.brandingLogo,
      };
}

/**
 * The upsert body. Blank members are dropped rather than sent empty — the
 * generated `formDataBodySerializer` already skips `null`/`undefined`, and
 * omitting them keeps the request to exactly what the user filled in.
 */
function toBrandingBody(branding: BrandingValues): BrandingUpsertBody {
  return {
    DisplayName: branding.displayName,
    ...(branding.tagline.trim() === "" ? {} : { Tagline: branding.tagline }),
    ...(branding.logo === null ? {} : { logo: branding.logo }),
  };
}

export function RegisterAppForm() {
  const { sdk } = useRouteContext({ from: "__root__" });
  // One-time secret: the registration response lives only here and in the
  // success view below — never written to the query cache, never re-fetched.
  // Holding it in the PARENT of the form is what lets the reveal REPLACE the
  // form instead of appearing beside it.
  const [registration, setRegistration] = useState<AppRegistrationResponse | null>(null);
  // Held for the retry: the upsert is re-fired from these values plus the
  // clientId above, so a retry never re-registers.
  const [branding, setBranding] = useState<BrandingValues | null>(null);
  const [brandingError, setBrandingError] = useState<string | null>(null);
  /*
   * The branding mutation belongs to THIS component, not to the form below.
   * Registering unmounts the form wholesale (the reveal replaces it), and
   * react-query drops `mutate(vars, { onSuccess })` callbacks the moment the
   * observing component unmounts — a callback hung off the form would silently
   * never fire. Nothing is invalidated: no screen in this app reads client
   * branding.
   */
  const upsertBranding = useMutation(clientBrandingUpsertBrandingMutation({ client: sdk.client }));

  function runBrandingUpsert(clientId: string, values: BrandingValues): void {
    upsertBranding.mutate(
      { path: { clientId }, body: toBrandingBody(values) },
      {
        onSuccess: () => {
          setBrandingError(null);
        },
        onError: (error: unknown) => {
          setBrandingError(errorText(error, BRANDING_FALLBACK_ERROR));
        },
      },
    );
  }

  function handleRegistered(result: AppRegistrationResponse, values: BrandingValues | null): void {
    setRegistration(result);
    setBranding(values);
    // The clientId the upsert is keyed on exists only now, in the response.
    if (values !== null) {
      runBrandingUpsert(result.clientId, values);
    }
  }

  function handleRetryBranding(): void {
    if (registration !== null && branding !== null) {
      runBrandingUpsert(registration.clientId, branding);
    }
  }

  if (registration !== null) {
    return (
      <SuccessView
        result={registration}
        brandingError={brandingError}
        brandingPending={upsertBranding.isPending}
        onRetryBranding={handleRetryBranding}
      />
    );
  }

  return (
    <Card>
      <RegisterAppFormFields onRegistered={handleRegistered} />
    </Card>
  );
}

/**
 * The form body, split out so the `Card` surface stays a shallow wrapper and the
 * success reveal can unmount the form wholesale.
 */
function RegisterAppFormFields(props: {
  onRegistered: (result: AppRegistrationResponse, branding: BrandingValues | null) => void;
}) {
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
      brandingDisplayName: "",
      brandingTagline: "",
      brandingLogo: null,
    },
    // The generated factory goes over WHOLE — `useAppForm` infers its `TError`
    // (Wallow-ov6w.2.6), so nothing here has to be destructured or cast.
    mutation: appsRegisterMutation({ client: sdk.client }),
    // The whole request contract in one place: the field remap the API expects,
    // and the newline split the two URI textareas carry. The three branding
    // values are deliberately ABSENT — they are a second request's payload, not
    // a widened first one.
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
      // unmounts the form and its values with it — which is exactly why the
      // branding trio is read off `form.state.values` and handed UP now, while
      // it still exists. Closing over `form` is safe: `onSuccess` only ever
      // runs after a render (same idiom as `CreateOrganizationForm`).
      onRegistered(data, toBrandingValues(form.state.values));
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

      <BrandingSection form={form} />

      <FormError />

      <SubmitButton>Register app</SubmitButton>
    </AppForm>
  );
}

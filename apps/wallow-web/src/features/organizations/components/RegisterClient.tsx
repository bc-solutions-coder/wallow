/**
 * The register-client stepper and the one-time reveal that follows it, for
 * both kinds: an application walks Basics → Redirects → Scopes → Branding, a
 * service account Basics → Scopes. One `useAppForm` owns every step: the inactive
 * steps stay mounted but hidden, so values survive navigation and a server
 * field error can pull the stepper back to the step that owns it. Only the
 * REQUIRED fields (name, a scope, and for an application a redirect URI) gate
 * Register — moving between steps is never blocked.
 */
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { ApiScopeDto, OrganizationClientRegistrationResponse } from "@bc-solutions-coder/sdk";
import { forkBranding } from "@bc-solutions-coder/styles";
import {
  Badge,
  Button,
  Card,
  CardHeader,
  Checkbox,
  CheckboxGroup,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useEffect, useMemo, useState } from "react";
import { useStore } from "@tanstack/react-form";
import { useRouteContext } from "@tanstack/react-router";
import { z } from "zod";

import { forkLinks } from "@shared/lib/fork-links";
import {
  organizationClientsListQueryKey,
  organizationClientsRegisterMutation,
  queriesForOperation,
  scopesListOptions,
} from "../api";

export type ClientKind = "application" | "service-account";

type Step = "basics" | "redirects" | "scopes" | "branding";
const STEP_LABELS: Record<Step, string> = {
  basics: "Basics",
  redirects: "Redirects",
  scopes: "Scopes",
  branding: "Branding",
};

/** How each kind presents: its steps, copy, test-id prefix and quickstart. */
interface KindPresentation {
  readonly testIdPrefix: string;
  readonly steps: readonly Step[];
  readonly registerTitle: string;
  readonly revealTitle: string;
  readonly namePlaceholder: string;
  readonly fallbackError: string;
  /** Where the reveal's quickstart link lands, relative to the docs site. */
  readonly quickstartPage: string;
  readonly defaultScopes: readonly string[];
  /** Whether the picker offers OIDC's login scopes (meaningless to a service account). */
  readonly offersLoginScopes: boolean;
}

const KINDS: Record<ClientKind, KindPresentation> = {
  application: {
    testIdPrefix: "organization-detail-register",
    steps: ["basics", "redirects", "scopes", "branding"],
    registerTitle: "Register application",
    revealTitle: "Application registered",
    namePlaceholder: "Dashboard",
    fallbackError: "Failed to register the application.",
    quickstartPage: "integrations/bff-pattern.html",
    defaultScopes: ["openid"],
    offersLoginScopes: true,
  },
  "service-account": {
    testIdPrefix: "organization-detail-register-service-account",
    steps: ["basics", "scopes"],
    registerTitle: "Register service account",
    revealTitle: "Service account registered",
    namePlaceholder: "Nightly sync",
    fallbackError: "Failed to register the service account.",
    quickstartPage: "api/service-accounts.html",
    defaultScopes: [],
    offersLoginScopes: false,
  },
};

/** Newline-separated textarea input to the wire's `string[]`. */
function toUriList(value: string): string[] {
  return value
    .split("\n")
    .map((uri) => uri.trim())
    .filter(Boolean);
}

/** The test-id prefix a kind's stepper, reveal and ledger button hang off. */
export function registerTestIdPrefix(kind: ClientKind): string {
  return KINDS[kind].testIdPrefix;
}

/**
 * What the stepper collects. The URI lists are one newline-separated string
 * each; `scopes` is the granted codes. `.trim()` makes `"   "` fail the
 * `min(1)` without trimming the submitted value.
 */
interface RegisterValues {
  name: string;
  refreshTokenLifetime: string;
  redirectUris: string;
  postLogoutRedirectUris: string;
  backchannelLogoutUri: string;
  scopes: string[];
  brandingDisplayName: string;
  brandingTagline: string;
}

/**
 * The fork's own name is reserved for the platform itself — the same rule the
 * API enforces, checked here so the person hears it before submitting.
 */
export function isReservedDisplayName(value: string): boolean {
  return value.trim().toLowerCase() === forkBranding.appName.trim().toLowerCase();
}

/** The message both the stepper and the branding editor show for a reserved display name. */
export const RESERVED_DISPLAY_NAME_MESSAGE = `'${forkBranding.appName}' is reserved for the platform itself.`;

/** The API's bounds on a per-client refresh-token lifetime, in seconds. */
const REFRESH_LIFETIME_MIN_SECONDS = 60;
const REFRESH_LIFETIME_MAX_SECONDS = 31_536_000;

/** What the stepper and the settings editor say when the lifetime is out of bounds. */
export const REFRESH_LIFETIME_RANGE_MESSAGE = `Enter a whole number of seconds between ${REFRESH_LIFETIME_MIN_SECONDS} and ${REFRESH_LIFETIME_MAX_SECONDS}, or leave it blank.`;

const WHOLE_SECONDS = /^\d+$/u;

/**
 * Blank is always fine — the server's default applies; otherwise the value must
 * be a whole number of seconds within the API's bounds, the same rule the
 * server enforces, checked here so the person hears it before submitting.
 */
export function isValidRefreshLifetime(value: string): boolean {
  const trimmed = value.trim();
  if (trimmed === "") {
    return true;
  }
  if (!WHOLE_SECONDS.test(trimmed)) {
    return false;
  }
  const seconds = Number(trimmed);
  return seconds >= REFRESH_LIFETIME_MIN_SECONDS && seconds <= REFRESH_LIFETIME_MAX_SECONDS;
}

/** A service account ignores every URI field, so only an application requires a redirect. */
function registerSchemaFor(kind: ClientKind): z.ZodType<RegisterValues, RegisterValues> {
  return z.object({
    name: z.string().trim().min(1, "Name is required"),
    refreshTokenLifetime: z.string().refine(isValidRefreshLifetime, REFRESH_LIFETIME_RANGE_MESSAGE),
    redirectUris:
      kind === "application"
        ? z
            .string()
            .refine((value) => toUriList(value).length > 0, "At least one redirect URI is required")
        : z.string(),
    postLogoutRedirectUris: z.string(),
    backchannelLogoutUri: z.string(),
    scopes: z.array(z.string()).min(1, "Choose at least one scope"),
    brandingDisplayName: z
      .string()
      .refine((value) => !isReservedDisplayName(value), RESERVED_DISPLAY_NAME_MESSAGE),
    brandingTagline: z.string(),
  });
}

/** Which step renders each field — where a server field error sends the user. */
const STEP_OF_FIELD: Record<keyof RegisterValues, Step> = {
  name: "basics",
  refreshTokenLifetime: "basics",
  redirectUris: "redirects",
  postLogoutRedirectUris: "redirects",
  backchannelLogoutUri: "redirects",
  scopes: "scopes",
  brandingDisplayName: "branding",
  brandingTagline: "branding",
};

function isFieldName(name: string): name is keyof RegisterValues {
  return name in STEP_OF_FIELD;
}

/** The required-field gate on Register. */
function canRegister(kind: ClientKind, values: RegisterValues): boolean {
  return (
    values.name.trim() !== "" &&
    (kind !== "application" || toUriList(values.redirectUris).length > 0) &&
    values.scopes.length > 0
  );
}

/**
 * The first field carrying a server (`onServer`) error, if any. Read off
 * `fieldMeta` rather than the form-level map because `setErrorMap` fans the
 * per-field messages out to the fields themselves.
 */
function firstServerErrorField(
  fieldMeta: Partial<Record<string, { readonly errorMap: { readonly onServer?: unknown } }>>,
): keyof RegisterValues | undefined {
  const entry = Object.entries(fieldMeta).find(([, meta]) => meta?.errorMap.onServer !== undefined);
  const name: string | undefined = entry?.[0];
  return name !== undefined && isFieldName(name) ? name : undefined;
}

/**
 * The login scopes every application may request. They are OIDC's own, not
 * rows in the scope catalog, so the picker lists them ahead of it.
 */
const LOGIN_SCOPES: readonly { code: string; displayName: string }[] = [
  { code: "openid", displayName: "Sign-in identity (openid)" },
  { code: "profile", displayName: "Profile" },
  { code: "email", displayName: "Email" },
  { code: "offline_access", displayName: "Refresh tokens (offline_access)" },
];

/**
 * The wire body. A service account sends empty URI lists whatever the hidden
 * Redirects fields hold — the API ignores them, and the stepper never shows them.
 */
function toRegisterBody(kind: ClientKind, values: RegisterValues) {
  const backchannel = values.backchannelLogoutUri.trim();
  const lifetime = values.refreshTokenLifetime.trim();
  const brandingDisplayName = values.brandingDisplayName.trim();
  const brandingTagline = values.brandingTagline.trim();
  return kind === "application"
    ? {
        kind,
        name: values.name,
        // Left blank, the server applies its per-client default.
        refreshTokenLifetime: lifetime === "" ? undefined : Number(lifetime),
        redirectUris: toUriList(values.redirectUris),
        postLogoutRedirectUris: toUriList(values.postLogoutRedirectUris),
        backchannelLogoutUri: backchannel === "" ? undefined : backchannel,
        scopes: values.scopes,
        // Left blank, registration defaults the branding to the client name.
        branding:
          brandingDisplayName === "" && brandingTagline === ""
            ? undefined
            : {
                displayName: brandingDisplayName === "" ? undefined : brandingDisplayName,
                tagline: brandingTagline === "" ? undefined : brandingTagline,
              },
      }
    : {
        kind,
        name: values.name,
        redirectUris: [],
        postLogoutRedirectUris: [],
        scopes: values.scopes,
      };
}

function useRegisterClientForm(
  kind: ClientKind,
  orgId: string,
  onRegistered: (result: OrganizationClientRegistrationResponse) => void,
) {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  return useAppForm({
    schema: registerSchemaFor(kind),
    defaultValues: {
      name: "",
      refreshTokenLifetime: "",
      redirectUris: "",
      postLogoutRedirectUris: "",
      backchannelLogoutUri: "",
      scopes: [...KINDS[kind].defaultScopes],
      brandingDisplayName: "",
      brandingTagline: "",
    },
    mutation: organizationClientsRegisterMutation({ client: sdk.client }),
    // Field by field, never a spread: the URI lists arrive as strings and an
    // empty back-channel URI is omitted.
    toVariables: (values) => ({
      path: { orgId },
      body: toRegisterBody(kind, values),
    }),
    onSuccess: (result) => {
      // Registering adds a row to the ledger the section renders above.
      void queryClient.invalidateQueries(
        queriesForOperation(
          organizationClientsListQueryKey({ client: sdk.client, path: { orgId } }),
        ),
      );
      // The secret is shown ONCE and never refetched, so the reveal cannot
      // read it back off the cache — the result is handed up.
      onRegistered(result);
    },
    fallbackError: KINDS[kind].fallbackError,
  });
}

type RegisterForm = ReturnType<typeof useRegisterClientForm>;

/** The stepper's fixed inputs, threaded to every part that renders a test id. */
interface Stepper {
  readonly kind: ClientKind;
  readonly form: RegisterForm;
}

/** The step rail: every step named, the current one marked. */
function StepRail(props: { kind: ClientKind; current: Step }) {
  const { steps, testIdPrefix } = KINDS[props.kind];
  return (
    <ol className="flex gap-4" aria-label="Registration steps">
      {steps.map((step, index) => (
        <li key={step} aria-current={step === props.current ? "step" : undefined}>
          <Text
            as="span"
            variant="bodySm"
            weight={step === props.current ? "medium" : undefined}
            color={step === props.current ? "onCard" : "muted"}
            data-testid={step === props.current ? `${testIdPrefix}-step` : undefined}
          >
            {index + 1}. {STEP_LABELS[step]}
          </Text>
        </li>
      ))}
    </ol>
  );
}

function BasicsStep(props: Stepper) {
  const { kind, form } = props;
  return (
    <div className="space-y-4">
      <form.AppField name="name">
        {(field) => <field.TextField label="Name" placeholder={KINDS[kind].namePlaceholder} />}
      </form.AppField>
      <MutedText>
        The client id is derived from the organization and this name. Neither can be changed after
        registration.
      </MutedText>
      {kind === "application" ? <LifetimeBasics form={form} /> : null}
    </div>
  );
}

/** The application-only lifetime field on Basics; a service account has no refresh tokens. */
function LifetimeBasics(props: { form: RegisterForm }) {
  const { form } = props;
  return (
    <>
      <form.AppField name="refreshTokenLifetime">
        {(field) => (
          <field.TextField
            label="Refresh token lifetime (seconds)"
            optional
            inputMode="numeric"
            placeholder="86400"
          />
        )}
      </form.AppField>
      <MutedText>
        How long after sign-in a session lasts before the user must sign in again. Leave blank for
        the platform default; changeable later from the ledger, for new logins only.
      </MutedText>
    </>
  );
}

function RedirectsStep(props: { form: RegisterForm }) {
  const { form } = props;
  return (
    <div className="space-y-4">
      <form.AppField name="redirectUris">
        {(field) => (
          <field.TextareaField
            label="Redirect URIs"
            placeholder="https://app.example.com/bff/callback"
            rows={3}
          />
        )}
      </form.AppField>
      <MutedText>
        One per line. Each must be absolute and fragment-free, and use HTTPS or http://localhost.
      </MutedText>
      <form.AppField name="postLogoutRedirectUris">
        {(field) => <field.TextareaField label="Post-logout redirect URIs" rows={2} optional />}
      </form.AppField>
      <form.AppField name="backchannelLogoutUri">
        {(field) => <field.TextField label="Back-channel logout URI" optional />}
      </form.AppField>
    </div>
  );
}

/**
 * The optional branding step: what END USERS see on the sign-in screen, as
 * opposed to Basics' internal ledger name. Both fields may stay blank —
 * registration then defaults the display name to the client name — and the
 * full editor (logo, theme) lives on the ledger row after registration.
 */
function BrandingStep(props: Stepper) {
  const { form } = props;
  return (
    <div className="space-y-4">
      <form.AppField name="brandingDisplayName">
        {(field) => <field.TextField label="Display name" optional />}
      </form.AppField>
      <form.AppField name="brandingTagline">
        {(field) => <field.TextField label="Tagline" optional />}
      </form.AppField>
      <MutedText>
        Shown to end users on the sign-in screen. Leave blank to use the application name; logo and
        theme colours can be added from the ledger after registration.
      </MutedText>
    </div>
  );
}

/** One scope checkbox; platform-only scopes render disabled with a badge. */
function ScopeOption(props: {
  prefix: string;
  code: string;
  displayName: string;
  platformOnly: boolean;
}) {
  const { prefix, code, displayName, platformOnly } = props;
  const testId = `${prefix}-scope-${code.replaceAll(".", "-")}`;
  return (
    <label className="flex items-center gap-3">
      <Checkbox.Root name={code} disabled={platformOnly} data-testid={testId}>
        <Checkbox.Indicator>✓</Checkbox.Indicator>
      </Checkbox.Root>
      <Text as="span" variant="bodySm" color="onCard">
        {displayName}
      </Text>
      <Text as="span" variant="bodySm" color="muted" className="font-mono">
        {code}
      </Text>
      {platformOnly ? <Badge data-testid={`${testId}-platform-only`}>Platform only</Badge> : null}
    </label>
  );
}

function ScopePicker(props: {
  kind: ClientKind;
  value: readonly string[];
  onChange: (next: string[]) => void;
  disabled: boolean;
  catalog: readonly ApiScopeDto[];
  catalogFailed: boolean;
  error: string | undefined;
}) {
  const { kind, value, onChange, disabled, catalog, catalogFailed, error } = props;
  const { testIdPrefix, offersLoginScopes } = KINDS[kind];
  const loginScopes = offersLoginScopes ? LOGIN_SCOPES : [];
  return (
    <fieldset className="space-y-3">
      <Text as="legend" variant="body" weight="medium" color="onCard">
        Scopes
      </Text>
      <CheckboxGroup
        value={[...value]}
        onValueChange={(next: string[]) => {
          onChange(next);
        }}
        disabled={disabled}
      >
        {loginScopes.map((scope) => (
          <ScopeOption
            key={scope.code}
            prefix={testIdPrefix}
            code={scope.code}
            displayName={scope.displayName}
            platformOnly={false}
          />
        ))}
        {catalog.map((scope) => (
          <ScopeOption
            key={scope.code}
            prefix={testIdPrefix}
            code={scope.code}
            displayName={scope.displayName}
            platformOnly={scope.platformOnly}
          />
        ))}
      </CheckboxGroup>
      {catalogFailed ? (
        <MutedText data-testid={`${testIdPrefix}-scopes-catalog-error`}>
          {offersLoginScopes
            ? "The scope catalog could not be loaded; only the login scopes are offered."
            : "The scope catalog could not be loaded."}
        </MutedText>
      ) : null}
      {error === undefined ? null : (
        <Text
          as="span"
          variant="bodySm"
          color="destructive"
          data-testid={`${testIdPrefix}-scopes-error`}
        >
          {error}
        </Text>
      )}
    </fieldset>
  );
}

function ScopesStep(props: Stepper) {
  const { kind, form } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isError } = useQuery(scopesListOptions({ client: sdk.client }));
  return (
    <form.AppField name="scopes">
      {(field) => (
        <ScopePicker
          kind={kind}
          value={field.state.value}
          onChange={field.handleChange}
          disabled={form.wallow.pending}
          catalog={data ?? []}
          catalogFailed={isError}
          error={firstMessage(field.state.meta.errors)}
        />
      )}
    </form.AppField>
  );
}

function firstMessage(errors: readonly unknown[]): string | undefined {
  const first: unknown = errors[0];
  if (typeof first === "string") {
    return first;
  }
  if (typeof first === "object" && first !== null && "message" in first) {
    const { message } = first as { message: unknown };
    return typeof message === "string" ? message : undefined;
  }
  return undefined;
}

function StepFooter(props: {
  kind: ClientKind;
  form: RegisterForm;
  step: Step;
  onBack: () => void;
  onNext: () => void;
  onCancel: () => void;
}) {
  const { kind, form, step, onBack, onNext, onCancel } = props;
  const { steps, testIdPrefix } = KINDS[kind];
  const ready: boolean = useStore(form.store, (state) => canRegister(kind, state.values));
  const index = steps.indexOf(step);
  const isFirst = index === 0;
  const isLast = index === steps.length - 1;
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Button
        type="button"
        variant="secondary"
        className="w-auto"
        disabled={isFirst}
        onClick={onBack}
        data-testid={`${testIdPrefix}-back`}
      >
        Back
      </Button>
      {isLast ? null : (
        <Button
          type="button"
          variant="secondary"
          className="w-auto"
          onClick={onNext}
          data-testid={`${testIdPrefix}-next`}
        >
          Next
        </Button>
      )}
      <SubmitButton className="w-auto rounded-full" disabled={!ready} pendingLabel="Registering…">
        Register
      </SubmitButton>
      <Button
        type="button"
        variant="secondary"
        className="w-auto"
        onClick={onCancel}
        data-testid={`${testIdPrefix}-cancel`}
      >
        Cancel
      </Button>
    </div>
  );
}

const STEP_BACK = -1;
const STEP_FORWARD = 1;

/**
 * Every step stays mounted so its values survive navigation; only the current
 * one is shown.
 */
function StepPanels(props: Stepper & { step: Step }) {
  const { kind, form, step } = props;
  return (
    <>
      <div hidden={step !== "basics"}>
        <BasicsStep kind={kind} form={form} />
      </div>
      {KINDS[kind].steps.includes("redirects") ? (
        <div hidden={step !== "redirects"}>
          <RedirectsStep form={form} />
        </div>
      ) : null}
      <div hidden={step !== "scopes"}>
        <ScopesStep kind={kind} form={form} />
      </div>
      {KINDS[kind].steps.includes("branding") ? (
        <div hidden={step !== "branding"}>
          <BrandingStep kind={kind} form={form} />
        </div>
      ) : null}
    </>
  );
}

/**
 * The stepper card. Every step stays mounted; the inactive ones are hidden so
 * a value typed on Basics survives a trip to Scopes and back.
 */
export function RegisterClient(props: {
  kind: ClientKind;
  orgId: string;
  onRegistered: (result: OrganizationClientRegistrationResponse) => void;
  onCancel: () => void;
}) {
  const { kind, orgId, onRegistered, onCancel } = props;
  const { steps, testIdPrefix, registerTitle } = KINDS[kind];
  const form = useRegisterClientForm(kind, orgId, onRegistered);
  const [step, setStep] = useState<Step>("basics");
  const serverErrorField: keyof RegisterValues | undefined = useStore(form.store, (state) =>
    firstServerErrorField(state.fieldMeta),
  );

  // A server field error lands on a hidden step otherwise: pull the stepper
  // back to the step that owns the field.
  useEffect(() => {
    if (serverErrorField !== undefined) {
      setStep(STEP_OF_FIELD[serverErrorField]);
    }
  }, [serverErrorField]);

  const move = (delta: number): void => {
    const index = steps.indexOf(step) + delta;
    const next: Step | undefined = steps[index];
    if (next !== undefined) {
      setStep(next);
    }
  };

  return (
    <Card data-testid={`${testIdPrefix}-card`}>
      <CardHeader title={registerTitle} />
      <StepRail kind={kind} current={step} />
      <AppForm form={form} testIdPrefix={testIdPrefix} className="space-y-6">
        <StepPanels kind={kind} form={form} step={step} />
        <FormError />
        <StepFooter
          kind={kind}
          form={form}
          step={step}
          onBack={() => {
            move(STEP_BACK);
          }}
          onNext={() => {
            move(STEP_FORWARD);
          }}
          onCancel={onCancel}
        />
      </AppForm>
    </Card>
  );
}

const COOKIE_PASSWORD_BYTES = 32;
const HEX_RADIX = 16;
const HEX_DIGITS_PER_BYTE = 2;

function randomHex(bytes: number): string {
  const buffer = new Uint8Array(bytes);
  crypto.getRandomValues(buffer);
  return Array.from(buffer, (byte) =>
    byte.toString(HEX_RADIX).padStart(HEX_DIGITS_PER_BYTE, "0"),
  ).join("");
}

/**
 * The env block the SDK reads, built once per reveal so a BFF's cookie password
 * is stable. An application feeds `createBffHandler`; a service account feeds
 * `createServiceClient`, so its block names the `OIDC_SERVICE_*` variables and
 * carries no redirect or cookie settings.
 */
function buildEnvBlock(kind: ClientKind, result: OrganizationClientRegistrationResponse): string {
  const { client } = result;
  const lines =
    kind === "application"
      ? [
          `OIDC_ISSUER=${result.issuer}`,
          `OIDC_CLIENT_ID=${client.clientId}`,
          `OIDC_CLIENT_SECRET=${result.clientSecret}`,
          `OIDC_REDIRECT_URI=${client.redirectUris[0] ?? ""}`,
          `OIDC_POST_LOGOUT_REDIRECT_URI=${client.postLogoutRedirectUris[0] ?? ""}`,
          `OIDC_SCOPES=${client.scopes.join(" ")}`,
          `BFF_API_BASE_URL=${result.apiBaseUrl}`,
          `COOKIE_PASSWORD=${randomHex(COOKIE_PASSWORD_BYTES)}`,
        ]
      : [
          `OIDC_ISSUER=${result.issuer}`,
          `OIDC_SERVICE_CLIENT_ID=${client.clientId}`,
          `OIDC_SERVICE_CLIENT_SECRET=${result.clientSecret}`,
          `OIDC_SERVICE_SCOPES=${client.scopes.join(" ")}`,
          `BFF_API_BASE_URL=${result.apiBaseUrl}`,
        ];
  return lines.join("\n");
}

function quickstartHref(kind: ClientKind): string {
  const { docsUrl } = forkLinks();
  const base = docsUrl.endsWith("/") ? docsUrl : `${docsUrl}/`;
  return new URL(KINDS[kind].quickstartPage, base).href;
}

function RevealRow(props: { label: string; value: string; testId: string }) {
  return (
    <div className="space-y-1">
      <Text as="span" variant="bodySm" color="muted">
        {props.label}
      </Text>
      <Text
        as="code"
        variant="bodySm"
        color="onCard"
        className="block font-mono"
        data-testid={props.testId}
      >
        {props.value}
      </Text>
    </div>
  );
}

function RevealActions(props: { kind: ClientKind; env: string; onDone: () => void }) {
  const { kind, env, onDone } = props;
  const { testIdPrefix } = KINDS[kind];
  const [copied, setCopied] = useState(false);
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Button
        type="button"
        className="w-auto"
        onClick={() => {
          void navigator.clipboard.writeText(env).then(() => {
            setCopied(true);
          });
        }}
        data-testid={`${testIdPrefix}-copy-env`}
      >
        {copied ? "Copied" : "Copy env block"}
      </Button>
      <Button
        render={<a href={quickstartHref(kind)} target="_blank" rel="noreferrer" />}
        nativeButton={false}
        variant="secondary"
        className="w-auto no-underline"
        data-testid={`${testIdPrefix}-quickstart`}
      >
        Open quickstart
      </Button>
      <Button
        type="button"
        variant="secondary"
        className="w-auto"
        onClick={onDone}
        data-testid={`${testIdPrefix}-done`}
      >
        Done
      </Button>
    </div>
  );
}

/**
 * The one-time reveal: client id, secret, and the env block, shown once. A
 * rotation reuses it under its own `title`; the reveal is otherwise the same.
 */
export function RegistrationReveal(props: {
  kind: ClientKind;
  result: OrganizationClientRegistrationResponse;
  onDone: () => void;
  title?: string;
}) {
  const { kind, result, onDone, title } = props;
  const { testIdPrefix, revealTitle } = KINDS[kind];
  const env: string = useMemo(() => buildEnvBlock(kind, result), [kind, result]);
  return (
    <Card data-testid={`${testIdPrefix}-success`}>
      <CardHeader
        title={title ?? revealTitle}
        description="Copy the client secret now — it is shown once and cannot be retrieved later."
      />
      <RevealRow
        label="Client id"
        value={result.client.clientId}
        testId={`${testIdPrefix}-client-id`}
      />
      <RevealRow
        label="Client secret"
        value={result.clientSecret}
        testId={`${testIdPrefix}-client-secret`}
      />
      <pre
        data-testid={`${testIdPrefix}-env`}
        className="overflow-x-auto rounded-md border border-border bg-background p-4 font-mono text-sm text-foreground"
      >
        {env}
      </pre>
      <RevealActions kind={kind} env={env} onDone={onDone} />
    </Card>
  );
}

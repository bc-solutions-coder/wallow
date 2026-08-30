/**
 * The register-application stepper (Basics → Redirects → Scopes) and the
 * one-time reveal that follows it. One `useAppForm` owns every step: the
 * inactive steps stay mounted but hidden, so values survive navigation and a
 * server field error can pull the stepper back to the step that owns it.
 * Only the REQUIRED fields (name, a redirect URI, a scope) gate Register —
 * moving between steps is never blocked.
 */
import { AppForm, FormError, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import { useQuery, useQueryClient } from "@bc-solutions-coder/query";
import type { ApiScopeDto, OrganizationClientRegistrationResponse } from "@bc-solutions-coder/sdk";
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

const TEST_ID_PREFIX = "organization-detail-register";

const STEPS = ["basics", "redirects", "scopes"] as const;
type Step = (typeof STEPS)[number];
const STEP_LABELS: Record<Step, string> = {
  basics: "Basics",
  redirects: "Redirects",
  scopes: "Scopes",
};

/** Newline-separated textarea input to the wire's `string[]`. */
function toUriList(value: string): string[] {
  return value
    .split("\n")
    .map((uri) => uri.trim())
    .filter(Boolean);
}

/**
 * What the stepper collects. The URI lists are one newline-separated string
 * each; `scopes` is the granted codes. `.trim()` makes `"   "` fail the
 * `min(1)` without trimming the submitted value.
 */
const registerApplicationSchema = z.object({
  name: z.string().trim().min(1, "Name is required"),
  redirectUris: z
    .string()
    .refine((value) => toUriList(value).length > 0, "At least one redirect URI is required"),
  postLogoutRedirectUris: z.string(),
  backchannelLogoutUri: z.string(),
  scopes: z.array(z.string()).min(1, "Choose at least one scope"),
});
type RegisterValues = z.input<typeof registerApplicationSchema>;

/** Which step renders each field — where a server field error sends the user. */
const STEP_OF_FIELD: Record<keyof RegisterValues, Step> = {
  name: "basics",
  redirectUris: "redirects",
  postLogoutRedirectUris: "redirects",
  backchannelLogoutUri: "redirects",
  scopes: "scopes",
};

function isFieldName(name: string): name is keyof RegisterValues {
  return name in STEP_OF_FIELD;
}

/** The required-field gate on Register. */
function canRegister(values: RegisterValues): boolean {
  return (
    values.name.trim() !== "" &&
    toUriList(values.redirectUris).length > 0 &&
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

function useRegisterApplicationForm(
  orgId: string,
  onRegistered: (result: OrganizationClientRegistrationResponse) => void,
) {
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();

  return useAppForm({
    schema: registerApplicationSchema,
    defaultValues: {
      name: "",
      redirectUris: "",
      postLogoutRedirectUris: "",
      backchannelLogoutUri: "",
      scopes: ["openid"],
    },
    mutation: organizationClientsRegisterMutation({ client: sdk.client }),
    // Field by field, never a spread: the URI lists arrive as strings, `kind`
    // is fixed to `application` here, and an empty back-channel URI is omitted.
    toVariables: (values) => ({
      path: { orgId },
      body: {
        kind: "application",
        name: values.name,
        redirectUris: toUriList(values.redirectUris),
        postLogoutRedirectUris: toUriList(values.postLogoutRedirectUris),
        backchannelLogoutUri:
          values.backchannelLogoutUri.trim() === ""
            ? undefined
            : values.backchannelLogoutUri.trim(),
        scopes: values.scopes,
      },
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
    fallbackError: "Failed to register the application.",
  });
}

type RegisterForm = ReturnType<typeof useRegisterApplicationForm>;

/** The step rail: every step named, the current one marked. */
function StepRail(props: { current: Step }) {
  return (
    <ol className="flex gap-4" aria-label="Registration steps">
      {STEPS.map((step, index) => (
        <li key={step} aria-current={step === props.current ? "step" : undefined}>
          <Text
            as="span"
            variant="bodySm"
            weight={step === props.current ? "medium" : undefined}
            color={step === props.current ? "onCard" : "muted"}
            data-testid={step === props.current ? `${TEST_ID_PREFIX}-step` : undefined}
          >
            {index + 1}. {STEP_LABELS[step]}
          </Text>
        </li>
      ))}
    </ol>
  );
}

function BasicsStep(props: { form: RegisterForm }) {
  const { form } = props;
  return (
    <div className="space-y-4">
      <form.AppField name="name">
        {(field) => <field.TextField label="Name" placeholder="Dashboard" />}
      </form.AppField>
      <MutedText>
        The client id is derived from the organization and this name. Neither can be changed after
        registration.
      </MutedText>
    </div>
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

/** One scope checkbox; platform-only scopes render disabled with a badge. */
function ScopeOption(props: { code: string; displayName: string; platformOnly: boolean }) {
  const { code, displayName, platformOnly } = props;
  const testId = `${TEST_ID_PREFIX}-scope-${code.replaceAll(".", "-")}`;
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
  value: readonly string[];
  onChange: (next: string[]) => void;
  disabled: boolean;
  catalog: readonly ApiScopeDto[];
  catalogFailed: boolean;
  error: string | undefined;
}) {
  const { value, onChange, disabled, catalog, catalogFailed, error } = props;
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
        {LOGIN_SCOPES.map((scope) => (
          <ScopeOption
            key={scope.code}
            code={scope.code}
            displayName={scope.displayName}
            platformOnly={false}
          />
        ))}
        {catalog.map((scope) => (
          <ScopeOption
            key={scope.code}
            code={scope.code}
            displayName={scope.displayName}
            platformOnly={scope.platformOnly}
          />
        ))}
      </CheckboxGroup>
      {catalogFailed ? (
        <MutedText data-testid={`${TEST_ID_PREFIX}-scopes-catalog-error`}>
          The scope catalog could not be loaded; only the login scopes are offered.
        </MutedText>
      ) : null}
      {error === undefined ? null : (
        <Text
          as="span"
          variant="bodySm"
          color="destructive"
          data-testid={`${TEST_ID_PREFIX}-scopes-error`}
        >
          {error}
        </Text>
      )}
    </fieldset>
  );
}

function ScopesStep(props: { form: RegisterForm }) {
  const { form } = props;
  const { sdk } = useRouteContext({ from: "__root__" });
  const { data, isError } = useQuery(scopesListOptions({ client: sdk.client }));
  return (
    <form.AppField name="scopes">
      {(field) => (
        <ScopePicker
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
  form: RegisterForm;
  step: Step;
  onBack: () => void;
  onNext: () => void;
  onCancel: () => void;
}) {
  const { form, step, onBack, onNext, onCancel } = props;
  const ready: boolean = useStore(form.store, (state) => canRegister(state.values));
  const index = STEPS.indexOf(step);
  const isFirst = index === 0;
  const isLast = index === STEPS.length - 1;
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Button
        type="button"
        variant="secondary"
        className="w-auto"
        disabled={isFirst}
        onClick={onBack}
        data-testid={`${TEST_ID_PREFIX}-back`}
      >
        Back
      </Button>
      {isLast ? null : (
        <Button
          type="button"
          variant="secondary"
          className="w-auto"
          onClick={onNext}
          data-testid={`${TEST_ID_PREFIX}-next`}
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
        data-testid={`${TEST_ID_PREFIX}-cancel`}
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
function StepPanels(props: { form: RegisterForm; step: Step }) {
  const { form, step } = props;
  return (
    <>
      <div hidden={step !== "basics"}>
        <BasicsStep form={form} />
      </div>
      <div hidden={step !== "redirects"}>
        <RedirectsStep form={form} />
      </div>
      <div hidden={step !== "scopes"}>
        <ScopesStep form={form} />
      </div>
    </>
  );
}

/**
 * The stepper card. Every step stays mounted; the inactive ones are hidden so
 * a value typed on Basics survives a trip to Scopes and back.
 */
export function RegisterApplication(props: {
  orgId: string;
  onRegistered: (result: OrganizationClientRegistrationResponse) => void;
  onCancel: () => void;
}) {
  const { orgId, onRegistered, onCancel } = props;
  const form = useRegisterApplicationForm(orgId, onRegistered);
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
    const index = STEPS.indexOf(step) + delta;
    const next: Step | undefined = STEPS[index];
    if (next !== undefined) {
      setStep(next);
    }
  };

  return (
    <Card data-testid={`${TEST_ID_PREFIX}-card`}>
      <CardHeader title="Register application" />
      <StepRail current={step} />
      <AppForm form={form} testIdPrefix={TEST_ID_PREFIX} className="space-y-6">
        <StepPanels form={form} step={step} />
        <FormError />
        <StepFooter
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

/** The env block a BFF needs, built once per reveal so the cookie password is stable. */
function buildEnvBlock(result: OrganizationClientRegistrationResponse): string {
  const { client } = result;
  return [
    `OIDC_ISSUER=${result.issuer}`,
    `OIDC_CLIENT_ID=${client.clientId}`,
    `OIDC_CLIENT_SECRET=${result.clientSecret}`,
    `OIDC_REDIRECT_URI=${client.redirectUris[0] ?? ""}`,
    `OIDC_POST_LOGOUT_REDIRECT_URI=${client.postLogoutRedirectUris[0] ?? ""}`,
    `OIDC_SCOPES=${client.scopes.join(" ")}`,
    `BFF_API_BASE_URL=${result.apiBaseUrl}`,
    `COOKIE_PASSWORD=${randomHex(COOKIE_PASSWORD_BYTES)}`,
  ].join("\n");
}

function quickstartHref(): string {
  const { docsUrl } = forkLinks();
  const base = docsUrl.endsWith("/") ? docsUrl : `${docsUrl}/`;
  return new URL("integrations/bff-pattern.html", base).href;
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

function RevealActions(props: { env: string; onDone: () => void }) {
  const [copied, setCopied] = useState(false);
  return (
    <div className="flex flex-wrap items-center gap-3">
      <Button
        type="button"
        className="w-auto"
        onClick={() => {
          void navigator.clipboard.writeText(props.env).then(() => {
            setCopied(true);
          });
        }}
        data-testid={`${TEST_ID_PREFIX}-copy-env`}
      >
        {copied ? "Copied" : "Copy env block"}
      </Button>
      <Button
        render={<a href={quickstartHref()} target="_blank" rel="noreferrer" />}
        nativeButton={false}
        variant="secondary"
        className="w-auto no-underline"
        data-testid={`${TEST_ID_PREFIX}-quickstart`}
      >
        Open quickstart
      </Button>
      <Button
        type="button"
        variant="secondary"
        className="w-auto"
        onClick={props.onDone}
        data-testid={`${TEST_ID_PREFIX}-done`}
      >
        Done
      </Button>
    </div>
  );
}

/** The one-time reveal: client id, secret, and the env block, shown once. */
export function RegistrationReveal(props: {
  result: OrganizationClientRegistrationResponse;
  onDone: () => void;
}) {
  const { result, onDone } = props;
  const env: string = useMemo(() => buildEnvBlock(result), [result]);
  return (
    <Card data-testid={`${TEST_ID_PREFIX}-success`}>
      <CardHeader
        title="Application registered"
        description="Copy the client secret now — it is shown once and cannot be retrieved later."
      />
      <RevealRow
        label="Client id"
        value={result.client.clientId}
        testId={`${TEST_ID_PREFIX}-client-id`}
      />
      <RevealRow
        label="Client secret"
        value={result.clientSecret}
        testId={`${TEST_ID_PREFIX}-client-secret`}
      />
      <pre
        data-testid={`${TEST_ID_PREFIX}-env`}
        className="overflow-x-auto rounded-md border border-border bg-background p-4 font-mono text-sm text-foreground"
      >
        {env}
      </pre>
      <RevealActions env={env} onDone={onDone} />
    </Card>
  );
}

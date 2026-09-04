/**
 * `useAppForm` against REAL generated SDK mutation factories, handed over whole.
 *
 * Compile time: no `as`, no `{ mutationFn }` destructuring — two factories with
 * different `TError` types pin the hook's error generic. Run time: the real
 * `createWallowSdk` client and generated operations in headless Chromium, with
 * only the transport (a recording `fetch`) standing in.
 */

import { QueryClient, QueryClientProvider } from "@bc-solutions-coder/query";
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import {
  organizationClientsRegisterMutation,
  organizationsCreateMutation,
} from "@bc-solutions-coder/sdk/query";
import { render } from "@bc-solutions-coder/testing/render";
import type { ReactElement, ReactNode } from "react";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { AppForm } from "./app-form";
import { FormError } from "./form-error";
import { SubmitButton } from "./submit-button";
import { useAppForm } from "./use-app-form";

/* ------------------------------------------------------------------ *
 * The transport: the one stand-in in this file.
 * ------------------------------------------------------------------ */

/** What the SDK actually put on the wire for one submit. */
interface SentRequest {
  readonly method: string;
  /** Path only — the base URL is the SDK's business, not this spec's. */
  readonly path: string;
  readonly body: unknown;
}

interface Transport {
  /** Every request the generated operation issued, in order. */
  readonly sent: readonly SentRequest[];
  readonly fetch: typeof globalThis.fetch;
}

/**
 * A `fetch` that records what it was handed and answers with `status`/`payload`.
 *
 * Recording the REQUEST is what makes these end-to-end rather than a restatement
 * of the harness: the method, the path and the JSON body are produced by the
 * generated operation, so they only come out right if the factory really ran.
 */
function createTransport(status: number, payload: unknown): Transport {
  const sent: SentRequest[] = [];

  const send: typeof globalThis.fetch = async (
    input: RequestInfo | URL,
    init?: RequestInit,
  ): Promise<Response> => {
    const request: Request = input instanceof Request ? input : new Request(input, init);
    const text: string = await request.text();

    sent.push({
      method: request.method,
      path: new URL(request.url).pathname,
      body: text === "" ? undefined : JSON.parse(text),
    });

    return new Response(JSON.stringify(payload), {
      status,
      headers: { "content-type": "application/json" },
    });
  };

  return { sent, fetch: send };
}

/**
 * A real request-scoped SDK over that transport — the same factory the apps
 * build their `sdk` with, so the client carries its real interceptors.
 */
function createSdk(transport: Transport): WallowSdk {
  return createWallowSdk({ baseUrl: "/api", fetch: transport.fetch });
}

/* ------------------------------------------------------------------ *
 * Harnesses: two forms, two generated factories, two `TError` types.
 * ------------------------------------------------------------------ */

const organizationSchema = z.object({
  name: z.string().trim().min(1, "Name is required"),
});

/**
 * The create-organization shape (`organizationsCreateMutation`, `TError =
 * DefaultError`), built exactly the way a migrated screen builds one — this is
 * the form Wallow-ov6w.4.1 had to write with a workaround.
 */
function CreateOrganizationHarness(props: {
  readonly sdk: WallowSdk;
  readonly onCreated: (organizationId: string) => void;
}): ReactElement {
  const form = useAppForm({
    schema: organizationSchema,
    defaultValues: { name: "" },
    // THE ASSERTION THIS FILE EXISTS FOR: the generated factory's result, whole.
    mutation: organizationsCreateMutation({ client: props.sdk.client }),
    // The create body carries a `domain` the form has no field for.
    toVariables: (values) => ({ body: { name: values.name, domain: null } }),
    onSuccess: (data) => {
      // Reading `organizationId` off the result also pins that `TData` is still
      // INFERRED from the factory rather than left at the `unknown` default.
      props.onCreated(data.organizationId);
    },
    fallbackError: "Could not create the organization.",
  });

  return (
    <AppForm form={form} testIdPrefix="organization-create">
      <form.AppField name="name">{(field) => <field.TextField label="Name" />}</form.AppField>
      <FormError />
      <SubmitButton>Create organization</SubmitButton>
    </AppForm>
  );
}

const registerAppSchema = z.object({
  name: z.string().trim().min(1, "Name is required"),
});

/**
 * The register-application shape (`organizationClientsRegisterMutation`), whose
 * `TError` is the operation's own `OrganizationClientsRegisterError` and NOT
 * `DefaultError`. Its presence is what stops the hook from being "fixed" by
 * hardcoding one concrete error type.
 */
function RegisterAppHarness(props: {
  readonly sdk: WallowSdk;
  readonly onRegistered: (clientId: string) => void;
}): ReactElement {
  const form = useAppForm({
    schema: registerAppSchema,
    defaultValues: { name: "" },
    mutation: organizationClientsRegisterMutation({ client: props.sdk.client }),
    toVariables: (values) => ({
      path: { orgId: "o1" },
      body: {
        kind: "application",
        name: values.name,
        redirectUris: ["https://app.example/cb"],
        postLogoutRedirectUris: [],
        scopes: ["openid"],
      },
    }),
    onSuccess: (data) => {
      props.onRegistered(data.client.clientId);
    },
    fallbackError: "Could not register the app.",
  });

  return (
    <AppForm form={form} testIdPrefix="app-register">
      <form.AppField name="name">{(field) => <field.TextField label="Name" />}</form.AppField>
      <FormError />
      <SubmitButton>Register app</SubmitButton>
    </AppForm>
  );
}

/* ------------------------------------------------------------------ *
 * Rendering helpers.
 * ------------------------------------------------------------------ */

/** Each case gets its own client so no mutation state leaks between them. */
function renderWithClient(children: ReactNode) {
  const client = new QueryClient({
    defaultOptions: { mutations: { retry: false }, queries: { retry: false } },
  });

  return render(<QueryClientProvider client={client}>{children}</QueryClientProvider>);
}

function byTestId(container: HTMLElement, id: string): HTMLElement {
  const element = container.querySelector(`[data-testid="${id}"]`);
  expect(element, id).not.toBeNull();

  return element as HTMLElement;
}

function queryTestId(container: HTMLElement, id: string): HTMLElement | null {
  return container.querySelector(`[data-testid="${id}"]`);
}

describe("useAppForm with a generated SDK mutation", () => {
  describe("organizationsCreateMutation (TError = DefaultError)", () => {
    it("submits through the generated operation and reports its parsed response", async () => {
      const transport = createTransport(200, { organizationId: "org-1" });
      const onCreated = vi.fn<(organizationId: string) => void>();
      const { container } = await renderWithClient(
        <CreateOrganizationHarness sdk={createSdk(transport)} onCreated={onCreated} />,
      );

      await userEvent.fill(byTestId(container, "organization-create-name"), "Acme");
      await userEvent.click(byTestId(container, "organization-create-submit"));

      // The request is the generated operation's own work — url, method and the
      // serialized body all come from it, not from this spec.
      await expect.poll(() => transport.sent.length).toBe(1);
      expect(transport.sent[0]).toEqual({
        method: "POST",
        path: "/api/v1/identity/organizations",
        body: { name: "Acme", domain: null },
      });
      // And the response the SDK parsed reaches `onSuccess` typed, not raw.
      await expect.poll(() => onCreated.mock.calls.length).toBe(1);
      expect(onCreated.mock.calls[0]?.[0]).toBe("org-1");
      expect(queryTestId(container, "organization-create-error")).toBeNull();
    });

    it("lets the schema block the submit before the generated operation runs", async () => {
      const transport = createTransport(200, { organizationId: "org-1" });
      const { container } = await renderWithClient(
        <CreateOrganizationHarness sdk={createSdk(transport)} onCreated={vi.fn()} />,
      );

      await userEvent.click(byTestId(container, "organization-create-submit"));

      await expect
        .poll(() => byTestId(container, "organization-create-name-error").textContent)
        .toBe("Name is required");
      // The half a "swallow the submit" implementation would fail: no HTTP call.
      expect(transport.sent).toHaveLength(0);
    });
  });

  describe("organizationClientsRegisterMutation (TError = the operation's own error)", () => {
    it("submits through the generated operation and reports its parsed response", async () => {
      const transport = createTransport(201, {
        client: {
          clientId: "client-1",
          name: "Dashboard",
          kind: "application",
          status: "active",
          redirectUris: ["https://app.example/cb"],
          postLogoutRedirectUris: [],
          scopes: ["openid"],
          createdByUserId: "u1",
          createdAt: "2026-01-01T00:00:00Z",
        },
        clientSecret: "secret",
        issuer: "https://auth.example/auth",
        apiBaseUrl: "https://api.example",
      });
      const onRegistered = vi.fn<(clientId: string) => void>();
      const { container } = await renderWithClient(
        <RegisterAppHarness sdk={createSdk(transport)} onRegistered={onRegistered} />,
      );

      await userEvent.fill(byTestId(container, "app-register-name"), "Dashboard");
      await userEvent.click(byTestId(container, "app-register-submit"));

      await expect.poll(() => transport.sent.length).toBe(1);
      expect(transport.sent[0]).toEqual({
        method: "POST",
        path: "/api/v1/identity/organizations/o1/clients",
        body: {
          kind: "application",
          name: "Dashboard",
          redirectUris: ["https://app.example/cb"],
          postLogoutRedirectUris: [],
          scopes: ["openid"],
        },
      });
      await expect.poll(() => onRegistered.mock.calls.length).toBe(1);
      expect(onRegistered.mock.calls[0]?.[0]).toBe("client-1");
    });

    it("splits the API's RFC 7807 failure across the field and the banner", async () => {
      // A real 400 problem details body: the SDK's own error interceptor turns
      // it into an `ApiFailure`, `splitServerError` folds `Name` onto the
      // form's `name`, and `Scopes` — which this form has no field for — joins
      // the banner instead of vanishing.
      const transport = createTransport(400, {
        status: 400,
        title: "Validation failed",
        detail: "One or more validation errors occurred.",
        errors: {
          Name: ["'Name' must not be empty."],
          Scopes: ["At least one scope is required."],
        },
        code: "VALIDATION_ERROR",
      });
      const { container } = await renderWithClient(
        <RegisterAppHarness sdk={createSdk(transport)} onRegistered={vi.fn()} />,
      );

      await userEvent.fill(byTestId(container, "app-register-name"), "Dashboard");
      await userEvent.click(byTestId(container, "app-register-submit"));

      await expect
        .poll(() => byTestId(container, "app-register-name-error").textContent)
        .toBe("'Name' must not be empty.");
      expect(byTestId(container, "app-register-error").textContent).toBe(
        "At least one scope is required.",
      );
    });
  });
});

/**
 * `useAppForm` against a REAL generated SDK mutation factory.
 *
 * `use-app-form.test.tsx` covers the hook's behaviour, but every one of its
 * cases hands over a hand-made `{ mutationFn }` — so nothing in this package
 * ever met the thing the option actually receives in production, and the whole
 * point of the option (a form author writes
 * `mutation: organizationsCreateMutation({ client: sdk.client })` and is done)
 * went uncovered. Wallow-ov6w.4.1 found out at the call site: the generated
 * factory returns `UseMutationOptions<TData, TError, TVariables>` with a REAL
 * error type, `UseAppFormOptions.mutation` pins that slot to `unknown`, and
 * `TError` sits in the contravariant position of the optional
 * `onError`/`onSettled` members — so handing the factory over whole is a hard
 * TS2322 and the migration had to destructure `mutationFn` back out.
 *
 * This file is that missing coverage, and it is deliberately BOTH kinds of test:
 *
 *   - COMPILE TIME. Every `useAppForm` call below passes the factory's result
 *     WHOLE — no `as`, no `as any`, no `{ mutationFn }` destructuring, no
 *     intermediate re-annotation. `pnpm --filter @bc-solutions-coder/forms
 *     typecheck` (`tsc --noEmit`, part of `pnpm check`) is therefore the gate:
 *     it fails today and must pass once the hook takes a `TError` generic.
 *     TWO factories are used on purpose — `organizationsCreateMutation` has
 *     `TError = DefaultError` while `appsRegisterMutation` has
 *     `TError = AppsRegisterError` — so a fix that merely swaps `unknown` for
 *     `DefaultError` (which would satisfy Wallow-ov6w.4.1 alone) still fails
 *     here, as it would for .4.2 and .4.3.
 *
 *   - RUN TIME. The cases drive the real chain end to end in headless Chromium:
 *     the real `createWallowSdk` client (its CSRF and RFC 7807 error
 *     interceptors included), the real generated operation, the real
 *     `AppForm`/`TextField`/`FormError`/`SubmitButton` shell. The ONLY stand-in
 *     is the transport — a `fetch` that records the outgoing request and answers
 *     it — so the assertions are about the HTTP call the SDK genuinely made and
 *     the `WallowError` it genuinely raised, not about a spy the spec invented.
 *
 * Nothing is mocked: no jsdom, no `vi.mock`, no stubbed `@bc-solutions-coder/ui`
 * (.claude/rules/TESTING.md).
 */

import { QueryClient, QueryClientProvider } from "@bc-solutions-coder/query";
import { createWallowSdk, type WallowSdk } from "@bc-solutions-coder/sdk";
import { appsRegisterMutation, organizationsCreateMutation } from "@bc-solutions-coder/sdk/query";
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
  clientName: z.string().trim().min(1, "Client name is required"),
});

/**
 * The register-app shape (`appsRegisterMutation`), whose `TError` is the
 * operation's own `AppsRegisterError` and NOT `DefaultError`. Its presence is
 * what stops the hook from being "fixed" by hardcoding one concrete error type.
 */
function RegisterAppHarness(props: {
  readonly sdk: WallowSdk;
  readonly onRegistered: (clientId: string) => void;
}): ReactElement {
  const form = useAppForm({
    schema: registerAppSchema,
    defaultValues: { clientName: "" },
    mutation: appsRegisterMutation({ client: props.sdk.client }),
    toVariables: (values) => ({
      body: { clientName: values.clientName, requestedScopes: ["openid"] },
    }),
    onSuccess: (data) => {
      props.onRegistered(data.clientId);
    },
    fallbackError: "Could not register the app.",
  });

  return (
    <AppForm form={form} testIdPrefix="app-register">
      <form.AppField name="clientName">
        {(field) => <field.TextField label="Client name" />}
      </form.AppField>
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

  describe("appsRegisterMutation (TError = the operation's own error)", () => {
    it("submits through the generated operation and reports its parsed response", async () => {
      const transport = createTransport(201, {
        clientId: "client-1",
        clientSecret: "secret",
        registrationAccessToken: "token",
      });
      const onRegistered = vi.fn<(clientId: string) => void>();
      const { container } = await renderWithClient(
        <RegisterAppHarness sdk={createSdk(transport)} onRegistered={onRegistered} />,
      );

      await userEvent.fill(byTestId(container, "app-register-client-name"), "Dashboard");
      await userEvent.click(byTestId(container, "app-register-submit"));

      await expect.poll(() => transport.sent.length).toBe(1);
      expect(transport.sent[0]).toEqual({
        method: "POST",
        path: "/api/v1/identity/apps/register",
        body: { clientName: "Dashboard", requestedScopes: ["openid"] },
      });
      await expect.poll(() => onRegistered.mock.calls.length).toBe(1);
      expect(onRegistered.mock.calls[0]?.[0]).toBe("client-1");
    });

    it("splits the API's RFC 7807 failure across the field and the banner", async () => {
      // A real 400 problem details body: the SDK's own error interceptor turns
      // it into a `WallowError`, `splitServerError` folds `ClientName` onto the
      // form's `clientName`, and `RequestedScopes` — which this form has no
      // field for — joins the banner instead of vanishing.
      const transport = createTransport(400, {
        status: 400,
        title: "Validation failed",
        detail: "One or more validation errors occurred.",
        errors: {
          ClientName: ["'Client Name' must not be empty."],
          RequestedScopes: ["At least one scope is required."],
        },
        extensions: { code: "VALIDATION_ERROR" },
      });
      const { container } = await renderWithClient(
        <RegisterAppHarness sdk={createSdk(transport)} onRegistered={vi.fn()} />,
      );

      await userEvent.fill(byTestId(container, "app-register-client-name"), "Dashboard");
      await userEvent.click(byTestId(container, "app-register-submit"));

      await expect
        .poll(() => byTestId(container, "app-register-client-name-error").textContent)
        .toBe("'Client Name' must not be empty.");
      expect(byTestId(container, "app-register-error").textContent).toBe(
        "At least one scope is required.",
      );
    });
  });
});

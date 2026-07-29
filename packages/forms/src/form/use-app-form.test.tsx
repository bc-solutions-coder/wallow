import { WallowError } from "@bc-solutions-coder/sdk";
import { render } from "@bc-solutions-coder/testing/render";
import { QueryClient, QueryClientProvider, type UseMutationOptions } from "@tanstack/react-query";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { firstErrorMessage } from "../core/errors";
import { AppForm } from "./app-form";
import { FormError } from "./form-error";
import { SubmitButton } from "./submit-button";
import { useAppForm } from "./use-app-form";

/*
 * `useAppForm` end to end, in the browser project (real headless Chromium, a
 * real `QueryClient`, the real `AppForm`/`SubmitButton`/`FormError` shell and the
 * real `WallowError` from the SDK — nothing is mocked except the userland
 * `mutationFn`, which stands in for a generated SDK operation).
 *
 * The catalog fields do not exist yet (Wallow-ov6w.2.4), so the harness mounts
 * one bare `form.Field` render prop to observe field-level messages. That is
 * deliberate and load-bearing rather than a shortcut: `form.setErrorMap` only
 * reaches fields that are already registered, so a server field error can only
 * be observed through a mounted field.
 *
 * What is pinned here:
 *
 *   1. The schema is wired as TanStack's `onSubmit` validator — invalid values
 *      surface on the field AND the mutation never runs. Both halves matter: a
 *      hook that merely swallowed the submit would satisfy either alone.
 *   2. A passing submit calls the mutation exactly ONCE, with the default
 *      `{ body: values }` variables the generated SDK operations expect, and
 *      hands the result to `onSuccess`.
 *   3. `toVariables` replaces that default for operations that also take a path.
 *   4. `pending` is real even on the no-mutation escape hatch (the
 *      forgot-password shape), because that path still runs through a mutation.
 *   5./6. A failure is SPLIT: an RFC 7807 `detail` becomes the banner, while
 *      `errors` entries become field messages and leave the banner clear. Both
 *      cases render through the shell with NO `pending`/`serverError` prop
 *      passed, which is what proves `AppForm` defaults them off `form.wallow`.
 *   7. A banner does not outlive the submit that produced it.
 */

const schema = z.object({
  name: z.string().min(1, "Name is required."),
  email: z.string().min(1, "Email is required."),
});

type Values = z.output<typeof schema>;

/** What a generated SDK mutation returns and takes — `body`, optionally `path`. */
interface MutationData {
  readonly id: string;
}

interface MutationVariables {
  readonly body: Values;
  readonly path?: { readonly organizationId: string };
}

type Mutation = UseMutationOptions<MutationData, unknown, MutationVariables>;

const VALID_VALUES: Values = { name: "Ada", email: "ada@example.com" };

interface HarnessProps {
  readonly defaultValues?: Values;
  readonly mutation?: Mutation;
  readonly toVariables?: (values: Values) => MutationVariables;
  readonly onSubmit?: (values: Values) => Promise<void> | void;
  readonly onSuccess?: (data: MutationData) => void;
  readonly fallbackError?: string;
}

/**
 * A form built the way a migrated screen will build one: the hook, the shell,
 * the banner and the submit button, with no `pending`/`serverError` threaded
 * through props.
 */
function Harness(props: HarnessProps) {
  const form = useAppForm({
    schema,
    defaultValues: props.defaultValues ?? VALID_VALUES,
    mutation: props.mutation,
    toVariables: props.toVariables,
    onSubmit: props.onSubmit,
    onSuccess: props.onSuccess,
    fallbackError: props.fallbackError,
  });

  return (
    <AppForm form={form} testIdPrefix="demo">
      <FormError />
      <form.Field name="name">
        {(field) => (
          <span data-testid="demo-name-error">
            {firstErrorMessage(field.state.meta.errors) ?? ""}
          </span>
        )}
      </form.Field>
      <SubmitButton pendingLabel="Saving...">Save</SubmitButton>
    </AppForm>
  );
}

/** Each case gets its own client so no mutation state leaks between them. */
function renderHarness(props: HarnessProps = {}) {
  const client = new QueryClient({
    defaultOptions: { mutations: { retry: false }, queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={client}>
      <Harness {...props} />
    </QueryClientProvider>,
  );
}

function byTestId(container: HTMLElement, id: string): HTMLElement {
  const element = container.querySelector(`[data-testid="${id}"]`);
  expect(element, id).not.toBeNull();
  return element as HTMLElement;
}

function queryTestId(container: HTMLElement, id: string): HTMLElement | null {
  return container.querySelector(`[data-testid="${id}"]`);
}

function submitButton(container: HTMLElement): HTMLButtonElement {
  return byTestId(container, "demo-submit") as HTMLButtonElement;
}

/** A promise the spec settles by hand, so "in flight" is an observable state. */
function createDeferred(): { readonly promise: Promise<void>; readonly resolve: () => void } {
  let settle: () => void = () => undefined;
  const promise = new Promise<void>((resolve) => {
    settle = resolve;
  });

  return { promise, resolve: () => settle() };
}

function succeedingMutationFn() {
  return vi.fn<(variables: MutationVariables) => Promise<MutationData>>().mockResolvedValue({
    id: "created",
  });
}

describe("useAppForm", () => {
  describe("schema validation", () => {
    it("blocks the submit and surfaces the schema message on the field", async () => {
      const mutationFn = succeedingMutationFn();
      const { container } = await renderHarness({
        defaultValues: { name: "", email: "ada@example.com" },
        mutation: { mutationFn },
      });

      await userEvent.click(submitButton(container));

      await expect
        .poll(() => byTestId(container, "demo-name-error").textContent)
        .toBe("Name is required.");
      // The half that a "swallow the submit" implementation would fail.
      expect(mutationFn).not.toHaveBeenCalled();
    });
  });

  describe("the mutation", () => {
    it("runs once with the default { body: values } variables and reports the result", async () => {
      // `{ body: ... }` is not arbitrary: every generated `{operation}Mutation`
      // takes the request options object whose `body` member is the payload, and
      // the forms this replaces already call `mutate({ body: value })` by hand.
      const mutationFn = succeedingMutationFn();
      const onSuccess = vi.fn<(data: MutationData) => void>();
      const { container } = await renderHarness({ mutation: { mutationFn }, onSuccess });

      await userEvent.click(submitButton(container));

      await expect.poll(() => mutationFn.mock.calls.length).toBe(1);
      expect(mutationFn.mock.calls[0]?.[0]).toEqual({ body: VALID_VALUES });
      await expect.poll(() => onSuccess.mock.calls.length).toBe(1);
      expect(onSuccess.mock.calls[0]?.[0]).toEqual({ id: "created" });
    });

    it("lets toVariables replace the default body wrapping", async () => {
      // The operations that take a path parameter as well as a body.
      const mutationFn = succeedingMutationFn();
      const { container } = await renderHarness({
        mutation: { mutationFn },
        toVariables: (values) => ({ body: values, path: { organizationId: "org-1" } }),
      });

      await userEvent.click(submitButton(container));

      await expect.poll(() => mutationFn.mock.calls.length).toBe(1);
      expect(mutationFn.mock.calls[0]?.[0]).toEqual({
        body: VALID_VALUES,
        path: { organizationId: "org-1" },
      });
    });

    it("reports pending through the shell while a mutation-less onSubmit is in flight", async () => {
      // The forgot-password shape: no SDK mutation, just a callback. It still
      // runs through an internal mutation so the button behaves identically.
      const deferred = createDeferred();
      const { container } = await renderHarness({ onSubmit: () => deferred.promise });

      await userEvent.click(submitButton(container));

      await expect.poll(() => submitButton(container).disabled).toBe(true);
      expect(submitButton(container).textContent).toBe("Saving...");

      deferred.resolve();

      await expect.poll(() => submitButton(container).disabled).toBe(false);
      expect(submitButton(container).textContent).toBe("Save");
    });
  });

  describe("server failures", () => {
    it("shows a WallowError's detail in the banner without being handed one", async () => {
      // The harness passes neither `pending` nor `serverError` to `AppForm`, so
      // this only renders if the shell defaults them off `form.wallow`.
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(
          new WallowError({
            status: 409,
            code: "CONFLICT",
            title: "Conflict",
            detail: "That name is taken.",
          }),
        );
      const { container } = await renderHarness({ mutation: { mutationFn } });

      await userEvent.click(submitButton(container));

      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("That name is taken.");
    });

    it("routes a WallowError's field errors onto the matching field and leaves the banner clear", async () => {
      // Client validation passed, so this message can only have come from the
      // server's RFC 7807 `errors` member, folded from `Name` onto `name`.
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(
          new WallowError({
            status: 400,
            code: "VALIDATION_ERROR",
            title: "Validation failed",
            fieldErrors: { Name: ["'Name' must not be empty."] },
          }),
        );
      const { container } = await renderHarness({ mutation: { mutationFn } });

      await userEvent.click(submitButton(container));

      await expect
        .poll(() => byTestId(container, "demo-name-error").textContent)
        .toBe("'Name' must not be empty.");
      expect(queryTestId(container, "demo-error")).toBeNull();
    });

    it("falls back to the caller's message for a failure that carries none", async () => {
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(new Error(""));
      const { container } = await renderHarness({
        mutation: { mutationFn },
        fallbackError: "Could not save the organization.",
      });

      await userEvent.click(submitButton(container));

      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("Could not save the organization.");
    });

    it("does not let a banner outlive the submit that produced it", async () => {
      const attempts: MutationVariables[] = [];
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockImplementation((variables) => {
          attempts.push(variables);

          return attempts.length === 1
            ? Promise.reject(new Error(""))
            : Promise.resolve({ id: "created" });
        });
      const { container } = await renderHarness({
        mutation: { mutationFn },
        fallbackError: "Could not save the organization.",
      });

      await userEvent.click(submitButton(container));
      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("Could not save the organization.");

      await userEvent.click(submitButton(container));

      await expect.poll(() => queryTestId(container, "demo-error")).toBeNull();
    });
  });
});

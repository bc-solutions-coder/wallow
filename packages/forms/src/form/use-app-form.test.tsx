import {
  ApiFailure,
  defineFailureMessages,
  type FailureMessageRegistry,
} from "@bc-solutions-coder/api-errors";
import {
  createQueryClient,
  QueryClientProvider,
  type UnhandledFailure,
  type UseMutationOptions,
} from "@bc-solutions-coder/query";
import { render } from "@bc-solutions-coder/testing/render";
import { FailureMessagesProvider } from "@bc-solutions-coder/ui/failure-messages";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { firstErrorMessage } from "../core/errors";
import { AppForm } from "./app-form";
import { FormError } from "./form-error";
import { SubmitButton } from "./submit-button";
import { useAppForm } from "./use-app-form";

/*
 * Browser coverage for form validation, mutation state, and failure messages.
 * A real QueryClient and form shell carry the submitted failure to the screen.
 * The bare form.Field registers the input whose server messages are observed.
 */

const schema = z.object({
  name: z.string().min(1, "Name is required."),
  email: z.string().min(1, "Email is required."),
});

type Values = z.output<typeof schema>;

/** A 400 under a code no shipped message knows, with no detail. */
function closedOrderFailure(): ApiFailure {
  return new ApiFailure({ status: 400, code: "Orders.Closed", title: "Bad Request" });
}

const closedOrderRegistry: FailureMessageRegistry = defineFailureMessages({
  "Orders.Closed": () => "That order is closed.",
});

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
  readonly messages?: FailureMessageRegistry;
  readonly fallbackError?: string;
}

/** What the host around the harness provides: the app registry, if any. */
interface HostProps {
  readonly registry?: FailureMessageRegistry;
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
    messages: props.messages,
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

/**
 * Each case gets its own client so no mutation state leaks between them. It is
 * the real `createQueryClient`, with the callback an app would toast from, so a
 * case can assert the form never reaches it.
 */
async function renderHarness(props: HarnessProps = {}, host: HostProps = {}) {
  const onUnhandledFailure = vi.fn<(failure: UnhandledFailure) => void>();
  const client = createQueryClient({ onUnhandledFailure });
  const tree = (
    <QueryClientProvider client={client}>
      <Harness {...props} />
    </QueryClientProvider>
  );
  const screen = await render(
    host.registry ? (
      <FailureMessagesProvider registry={host.registry}>{tree}</FailureMessagesProvider>
    ) : (
      tree
    ),
  );

  return { ...screen, client, onUnhandledFailure };
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
      // takes the request options object whose `body` member is the payload.
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
    it("shows a ApiFailure's detail in the banner without being handed one", async () => {
      // The harness passes neither `pending` nor `serverError` to `AppForm`, so
      // this only renders if the shell defaults them off `form.wallow`.
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(
          new ApiFailure({
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

    it("routes a ApiFailure's field errors onto the matching field and leaves the banner clear", async () => {
      // Client validation passed, so this message can only have come from the
      // server's RFC 7807 `errors` member, folded from `Name` onto `name`.
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(
          new ApiFailure({
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

    it("falls back to the caller's message for a failure nothing else covers", async () => {
      // A 400 under a code no registry knows, with no detail: steps 1-5 of the
      // resolver all miss, so the form's own last resort is what shows.
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(closedOrderFailure());
      const { container } = await renderHarness({
        mutation: { mutationFn },
        fallbackError: "Could not save the organization.",
      });

      await userEvent.click(submitButton(container));

      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("Could not save the organization.");
    });

    it("resolves the banner through the app registry ahead of the fallback", async () => {
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(closedOrderFailure());
      const { container } = await renderHarness(
        { mutation: { mutationFn }, fallbackError: "Could not save." },
        { registry: closedOrderRegistry },
      );

      await userEvent.click(submitButton(container));

      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("That order is closed.");
    });

    it("lets the form's own messages win over the app registry", async () => {
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(closedOrderFailure());
      const { container } = await renderHarness(
        {
          mutation: { mutationFn },
          messages: defineFailureMessages({ "Orders.Closed": () => "Reopen the order first." }),
        },
        { registry: closedOrderRegistry },
      );

      await userEvent.click(submitButton(container));

      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("Reopen the order first.");
    });

    it("shows the shipped network sentence for a transport failure, never its text", async () => {
      // The escape-hatch shape: no SDK operation, and the callback itself
      // throws what `fetch` throws. The banner must not echo "Failed to fetch".
      const { container, onUnhandledFailure } = await renderHarness({
        onSubmit: () => Promise.reject(new TypeError("Failed to fetch")),
        fallbackError: "Could not save the organization.",
      });

      await userEvent.click(submitButton(container));

      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("Unable to reach the server. Check your connection and try again.");
      expect(onUnhandledFailure).not.toHaveBeenCalled();
    });

    it("marks its mutation handled over the caller's meta so the client never toasts it", async () => {
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValue(
          new ApiFailure({ status: 409, code: "CONFLICT", title: "Conflict", detail: "Taken." }),
        );
      const { container, client, onUnhandledFailure } = await renderHarness({
        mutation: { mutationFn, meta: { audit: "member-add" } },
      });

      await userEvent.click(submitButton(container));

      await expect.poll(() => queryTestId(container, "demo-error")?.textContent).toBe("Taken.");
      expect(onUnhandledFailure).not.toHaveBeenCalled();
      expect(client.getMutationCache().getAll()[0]?.meta).toEqual({
        audit: "member-add",
        failureHandled: true,
      });
    });

    it("replaces unmatched wording on a later failure and clears it after success", async () => {
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockRejectedValueOnce(
          new ApiFailure({
            status: 400,
            code: "Validation.Failed",
            title: "Validation failed",
            fieldErrors: { Captcha: ["Captcha verification failed."] },
          }),
        )
        .mockRejectedValueOnce(
          new ApiFailure({
            status: 400,
            code: "Validation.Failed",
            title: "Validation failed",
            detail: "Please review the request.",
          }),
        )
        .mockResolvedValueOnce({ id: "created" });
      const { container } = await renderHarness({ mutation: { mutationFn } });

      await userEvent.click(submitButton(container));
      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("Captcha verification failed.");
      await userEvent.click(submitButton(container));
      await expect
        .poll(() => queryTestId(container, "demo-error")?.textContent)
        .toBe("Please review the request.");
      await userEvent.click(submitButton(container));
      await expect.poll(() => queryTestId(container, "demo-error")).toBeNull();
      expect(mutationFn).toHaveBeenCalledTimes(3);
    });

    it("does not let a banner outlive the submit that produced it", async () => {
      const attempts: MutationVariables[] = [];
      const mutationFn = vi
        .fn<(variables: MutationVariables) => Promise<MutationData>>()
        .mockImplementation((variables) => {
          attempts.push(variables);

          return attempts.length === 1
            ? Promise.reject(
                new ApiFailure({
                  status: 409,
                  code: "CONFLICT",
                  title: "Conflict",
                  detail: "Taken.",
                }),
              )
            : Promise.resolve({ id: "created" });
        });
      const { container } = await renderHarness({ mutation: { mutationFn } });

      await userEvent.click(submitButton(container));
      await expect.poll(() => queryTestId(container, "demo-error")?.textContent).toBe("Taken.");

      await userEvent.click(submitButton(container));

      await expect.poll(() => queryTestId(container, "demo-error")).toBeNull();
    });
  });
});

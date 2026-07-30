import {
  QueryClient,
  QueryClientProvider,
  type UseMutationOptions,
} from "@bc-solutions-coder/query";
import { WallowError } from "@bc-solutions-coder/sdk";
import { render } from "@bc-solutions-coder/testing/render";
import { userEvent } from "vitest/browser";
import { describe, expect, it, vi } from "vitest";
import { z } from "zod";

import { AppForm } from "./app-form";
import { SubmitButton } from "./submit-button";
import { useAppForm } from "./use-app-form";

/*
 * WHEN `useAppForm` validates (Wallow-ov6w.6) — the timing contract, in the
 * browser project (real headless Chromium, a real `QueryClient`, the real
 * `AppForm` + `AppField` + catalog `TextField` + ui `Field`/`Input`, the real
 * `WallowError`; nothing is mocked but the userland `mutationFn`).
 *
 * The sibling `use-app-form.test.tsx` pins WHAT validation says and where the
 * message lands. This file pins WHEN it runs, which is a separate contract and
 * the one the package guide got wrong: the design specified revalidate-on-
 * change and the hook was built submit-only.
 *
 * The rule has two halves, and an implementation that satisfies only one of them
 * is wrong in a user-visible way:
 *
 *   FIRST TOUCH IS QUIET. A field nobody has submitted yet must stay silent
 *   while it is being typed into, however invalid the half-typed value is.
 *   Erroring on the first keystroke of an empty required field is the classic
 *   "validate on change" regression, so it is asserted here directly (cases 1
 *   and 2) rather than left implied.
 *
 *   ONCE FLAGGED, IT KEEPS UP. After a failed submit the field is already
 *   showing a message, and from then on it re-validates as the user types: the
 *   text swaps to whatever rule the new value breaks (case 3), disappears the
 *   moment the value is good (case 4) and comes BACK if the value goes bad again
 *   (case 5) — with NO second submit, which every one of them proves by
 *   asserting the submit callback never ran again.
 *
 * Case 6 is the blast-radius guard: revalidation is per FIELD, so typing into
 * `name` must not silence the message a failed submit put on `email`. A "clear
 * every error on any change" implementation passes cases 3-5 and fails this.
 *
 * Case 7 is a REGRESSION GUARD over the other producer of field messages: the
 * `onServer` map key `splitServerError` fills from an RFC 7807 body. It passes
 * today and has to keep passing, because revalidation writes to the same field
 * meta. What it pins is the pair that is stable either way — the server message
 * reaches the field, and it leaves when the user edits the value the server
 * objected to.
 *
 * The ladder every case climbs comes from the schema below: "" breaks the min
 * rule, "Adalovelace" breaks the max rule, "Ada" breaks neither. Two different
 * failing messages are what make "the text updated live" distinguishable from
 * "the old text is still sitting there".
 */

const schema = z.object({
  name: z.string().min(1, "Name is required.").max(3, "Name must be 3 characters or fewer."),
  email: z.string().min(1, "Email is required."),
});

type Values = z.output<typeof schema>;

/** Breaks the max rule — the second message on the `name` ladder. */
const TOO_LONG_NAME = "Adalovelace";

/** Breaks neither rule. */
const VALID_NAME = "Ada";

interface MutationData {
  readonly id: string;
}

interface MutationVariables {
  readonly body: Values;
}

type Mutation = UseMutationOptions<MutationData, unknown, MutationVariables>;

interface HarnessProps {
  readonly defaultValues?: Values;
  readonly mutation?: Mutation;
  readonly onSubmit?: (values: Values) => Promise<void> | void;
}

/**
 * A form built the way a migrated screen builds one — the hook, the shell and
 * two REAL catalog fields. The catalog field is deliberate rather than a bare
 * `form.Field` readout: whether a message is on screen is exactly the question
 * here, and `CatalogFieldError` renders nothing at all when the field is valid,
 * so "no error" is the element's absence and cannot be confused with an empty
 * string.
 */
function Harness(props: HarnessProps) {
  const form = useAppForm({
    schema,
    defaultValues: props.defaultValues ?? { name: "", email: "" },
    mutation: props.mutation,
    onSubmit: props.onSubmit,
  });

  return (
    <AppForm form={form} testIdPrefix="demo">
      <form.AppField name="name">{(field) => <field.TextField label="Full name" />}</form.AppField>
      <form.AppField name="email">{(field) => <field.TextField label="Email" />}</form.AppField>
      <SubmitButton>Save</SubmitButton>
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

function input(container: HTMLElement, id: string): HTMLInputElement {
  return byTestId(container, id) as HTMLInputElement;
}

function submitButton(container: HTMLElement): HTMLButtonElement {
  return byTestId(container, "demo-submit") as HTMLButtonElement;
}

/**
 * Type `value` into the field and wait until it has reached FORM state.
 *
 * The catalog control is controlled off `field.state.value`, so the input
 * reading back what was typed is only possible after the change went through
 * `field.handleChange` and React re-rendered — which is the point at which a
 * revalidate-on-change implementation would have validated. Without this wait, a
 * "no error appeared" assertion could pass simply by running too early.
 */
async function typeInto(container: HTMLElement, id: string, value: string): Promise<void> {
  await userEvent.fill(input(container, id), value);
  await expect.poll(() => input(container, id).value).toBe(value);
}

describe("useAppForm validation timing", () => {
  it("stays quiet while a never-submitted field is being typed into", async () => {
    // The regression this whole contract has to avoid: an empty required field
    // that starts shouting on the first keystroke. `TOO_LONG_NAME` breaks the
    // schema, so the only thing keeping the message away is the timing rule.
    const { container } = await renderHarness();

    await typeInto(container, "demo-name", TOO_LONG_NAME);

    expect(queryTestId(container, "demo-name-error")).toBeNull();
  });

  it("stays quiet on a field left untouched while a sibling is typed into", async () => {
    // `email` is empty and required, and nothing has been submitted. Typing next
    // door must not validate the whole form.
    const { container } = await renderHarness();

    await typeInto(container, "demo-name", VALID_NAME);

    expect(queryTestId(container, "demo-email-error")).toBeNull();
  });

  it("swaps the message live once a failed submit has already flagged the field", async () => {
    const onSubmit = vi.fn<(values: Values) => void>();
    const { container } = await renderHarness({ onSubmit });

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-name-error")?.textContent)
      .toBe("Name is required.");

    await typeInto(container, "demo-name", TOO_LONG_NAME);

    await expect
      .poll(() => queryTestId(container, "demo-name-error")?.textContent)
      .toBe("Name must be 3 characters or fewer.");
    // Nothing was submitted a second time: the new message came from typing.
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("drops the message as soon as the value becomes valid, with no second submit", async () => {
    const onSubmit = vi.fn<(values: Values) => void>();
    const { container } = await renderHarness({ onSubmit });

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-name-error")?.textContent)
      .toBe("Name is required.");

    await typeInto(container, "demo-name", VALID_NAME);

    await expect.poll(() => queryTestId(container, "demo-name-error")).toBeNull();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("brings the message back when a corrected field is emptied again", async () => {
    // The half of "keeps up" that needs no second rule, and so applies to every
    // migrated form: required-only fields are the common case. A field that goes
    // quiet on correction but never speaks again is still validating once.
    const onSubmit = vi.fn<(values: Values) => void>();
    const { container } = await renderHarness({ onSubmit });

    await userEvent.click(submitButton(container));
    await typeInto(container, "demo-name", VALID_NAME);
    await expect.poll(() => queryTestId(container, "demo-name-error")).toBeNull();

    await typeInto(container, "demo-name", "");

    await expect
      .poll(() => queryTestId(container, "demo-name-error")?.textContent)
      .toBe("Name is required.");
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it("revalidates only the field being typed into, leaving a sibling's message alone", async () => {
    // The guard against "clear every error on any change", which would pass the
    // two cases above while wiping a message the user has not addressed yet.
    const { container } = await renderHarness();

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-email-error")?.textContent)
      .toBe("Email is required.");

    await typeInto(container, "demo-name", VALID_NAME);

    await expect.poll(() => queryTestId(container, "demo-name-error")).toBeNull();
    expect(queryTestId(container, "demo-email-error")?.textContent).toBe("Email is required.");
  });

  it("keeps showing a server field message until the user edits that field", async () => {
    // The `onServer` contract, pinned as a REGRESSION GUARD: it passes today and
    // has to keep passing, because revalidation touches the same field meta the
    // RFC 7807 split writes through `setErrorMap({ onServer })`.
    //
    // The submitted values PASS the schema, so the message can only have come
    // from the server — and it has to reach the field at all before anything can
    // be said about when it leaves. It leaves when the user edits the value the
    // server objected to, which is the only edit that can make the objection
    // stale.
    const mutationFn = vi
      .fn<(variables: MutationVariables) => Promise<MutationData>>()
      .mockRejectedValueOnce(
        new WallowError({
          status: 400,
          code: "VALIDATION_ERROR",
          title: "Validation failed",
          fieldErrors: { Name: ["That name is already taken."] },
        }),
      )
      .mockResolvedValue({ id: "created" });
    const { container } = await renderHarness({
      defaultValues: { name: VALID_NAME, email: "ada@example.com" },
      mutation: { mutationFn },
    });

    await userEvent.click(submitButton(container));
    await expect
      .poll(() => queryTestId(container, "demo-name-error")?.textContent)
      .toBe("That name is already taken.");

    // A different, still schema-valid value for the field the server rejected.
    await typeInto(container, "demo-name", "Bea");

    await expect.poll(() => queryTestId(container, "demo-name-error")).toBeNull();
  });
});

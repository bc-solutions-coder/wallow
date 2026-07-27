import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, userEvent } from "storybook/test";

import { Field } from "./field";

/*
 * `@storybook/addon-vitest` turns each export below into a Vitest test case in
 * the same headless Chromium the `browser` project uses, so these stories are
 * the VISUAL half of the Field spec — one per state a reviewer needs to eyeball
 * — while field.test.tsx holds the assertions a screenshot cannot make.
 */

const meta = {
  title: "Components/Field",
  component: Field,
} satisfies Meta<typeof Field>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The anatomy at rest: label over control, with no state attributes set. */
export const Default: Story = {
  render: () => (
    <Field>
      <Field.Label>Email</Field.Label>
      <Field.Control placeholder="name@example.com" />
    </Field>
  ),
};

export const WithDescription: Story = {
  render: () => (
    <Field>
      <Field.Label>Email</Field.Label>
      <Field.Control placeholder="name@example.com" />
      <Field.Description>We only use this to sign you in.</Field.Description>
    </Field>
  ),
};

/** `invalid` with no `validate`: the state is set, but there is no message. */
export const Invalid: Story = {
  render: () => (
    <Field invalid>
      <Field.Label>Email</Field.Label>
      <Field.Control defaultValue="not-an-email" />
    </Field>
  ),
};

/** The same field with a message, shown unconditionally via `match`. */
export const InvalidWithError: Story = {
  render: () => (
    <Field invalid>
      <Field.Label>Email</Field.Label>
      <Field.Control defaultValue="not-an-email" />
      <Field.Error match>Enter a valid email address.</Field.Error>
    </Field>
  ),
};

export const Disabled: Story = {
  render: () => (
    <Field disabled>
      <Field.Label>Email</Field.Label>
      <Field.Control defaultValue="ada@example.com" />
      <Field.Description>Contact support to change this.</Field.Description>
    </Field>
  ),
};

/** A control grouped beside its own label, as a checkbox or radio would be. */
export const Item: Story = {
  render: () => (
    <Field>
      <Field.Item>
        <Field.Control type="checkbox" />
        <Field.Label>Remember me</Field.Label>
      </Field.Item>
    </Field>
  ),
};

/**
 * The interaction half: a bad value, blurred, surfaces the `validate`
 * callback's message and flips the whole field into its invalid state.
 */
export const ValidatesOnBlur: Story = {
  render: () => (
    <Field
      validationMode="onBlur"
      validate={(value) => (String(value).includes("@") ? null : "Enter a valid email address.")}
      data-testid="validated-field"
    >
      <Field.Label>Email</Field.Label>
      <Field.Control data-testid="validated-control" />
      <Field.Error data-testid="validated-error" />
    </Field>
  ),
  play: async ({ canvas }) => {
    const control = canvas.getByTestId("validated-control");

    await userEvent.type(control, "nope");
    await userEvent.tab();

    await expect(canvas.getByTestId("validated-error")).toHaveTextContent(
      "Enter a valid email address.",
    );
    await expect(control).toHaveAttribute("aria-invalid", "true");
  },
};

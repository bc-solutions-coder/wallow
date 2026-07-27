import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect, fn, userEvent, waitFor } from "storybook/test";

import { Button } from "../button/button";
import { Field } from "../field/field";
import { Form } from "./form";

/*
 * `@storybook/addon-vitest` turns each export below into a Vitest test case in
 * the same headless Chromium the `browser` project uses, so these stories are
 * the VISUAL half of the Form spec — the states a reviewer needs to eyeball —
 * while form.test.tsx holds the assertions a screenshot cannot make.
 *
 * Form has no look of its own beyond the stacking rhythm, so what these show is
 * the two flows it exists for: a submit that reaches the handler with the field
 * values, and errors coming back from a server and landing on the right field.
 */

const meta = {
  title: "Components/Form",
  component: Form,
  args: {
    onFormSubmit: fn(),
  },
} satisfies Meta<typeof Form>;

export default meta;

type Story = StoryObj<typeof meta>;

/** The anatomy at rest: two fields and a submit, nothing in an error state. */
export const Default: Story = {
  render: (args) => (
    <Form {...args}>
      <Field name="email">
        <Field.Label>Email</Field.Label>
        <Field.Control data-testid="email" placeholder="name@example.com" />
      </Field>
      <Field name="password">
        <Field.Label>Password</Field.Label>
        <Field.Control data-testid="password" type="password" />
      </Field>
      <Button type="submit" data-testid="submit">
        Sign in
      </Button>
    </Form>
  ),
};

/** Errors handed back by a server, routed to the field named in the object. */
export const ServerErrors: Story = {
  args: { errors: { email: "That email is already registered." } },
  render: (args) => (
    <Form {...args}>
      <Field name="email">
        <Field.Label>Email</Field.Label>
        <Field.Control data-testid="email" defaultValue="ada@example.com" />
        <Field.Error data-testid="email-error" />
      </Field>
      <Field name="password">
        <Field.Label>Password</Field.Label>
        <Field.Control data-testid="password" type="password" />
        <Field.Error data-testid="password-error" />
      </Field>
      <Button type="submit" data-testid="submit">
        Sign in
      </Button>
    </Form>
  ),
};

/** Several messages for one field render as a list rather than joined text. */
export const SeveralErrorsOnOneField: Story = {
  args: { errors: { password: ["Too short.", "Needs a digit."] } },
  render: (args) => (
    <Form {...args}>
      <Field name="password">
        <Field.Label>Password</Field.Label>
        <Field.Control data-testid="password" type="password" defaultValue="abc" />
        <Field.Error data-testid="password-error" />
      </Field>
      <Button type="submit" data-testid="submit">
        Sign up
      </Button>
    </Form>
  ),
};

/** The happy path: filled fields submit, and the handler gets them by name. */
export const SubmitsValidValues: Story = {
  render: (args) => (
    <Form {...args}>
      <Field name="email">
        <Field.Label>Email</Field.Label>
        <Field.Control data-testid="email" />
        <Field.Error data-testid="email-error" />
      </Field>
      <Field name="password">
        <Field.Label>Password</Field.Label>
        <Field.Control data-testid="password" type="password" />
        <Field.Error data-testid="password-error" />
      </Field>
      <Button type="submit" data-testid="submit">
        Sign in
      </Button>
    </Form>
  ),
  play: async ({ args, canvas }) => {
    await userEvent.type(canvas.getByTestId("email"), "ada@example.com");
    await userEvent.type(canvas.getByTestId("password"), "hunter2");
    await userEvent.click(canvas.getByTestId("submit"));

    await expect(args.onFormSubmit).toHaveBeenCalledTimes(1);
    await expect(args.onFormSubmit).toHaveBeenCalledWith(
      { email: "ada@example.com", password: "hunter2" },
      expect.objectContaining({ reason: "none" }),
    );
  },
};

/** The unhappy path: an empty field blocks the submit and shows its message. */
export const ShowsValidationError: Story = {
  render: (args) => (
    <Form {...args}>
      <Field name="email" validate={(value) => (String(value) ? null : "Enter your email.")}>
        <Field.Label>Email</Field.Label>
        <Field.Control data-testid="email" />
        <Field.Error data-testid="email-error" />
      </Field>
      <Button type="submit" data-testid="submit">
        Sign in
      </Button>
    </Form>
  ),
  play: async ({ args, canvas }) => {
    await userEvent.click(canvas.getByTestId("submit"));

    await expect(canvas.getByTestId("email-error")).toHaveTextContent("Enter your email.");
    await expect(canvas.getByTestId("email")).toHaveAttribute("aria-invalid", "true");
    await expect(args.onFormSubmit).not.toHaveBeenCalled();
  },
};

/** Editing the field the server complained about takes the message away again. */
export const ClearsTheServerErrorOnEdit: Story = {
  args: { errors: { email: "That email is already registered." } },
  render: (args) => (
    <Form {...args}>
      <Field name="email">
        <Field.Label>Email</Field.Label>
        <Field.Control data-testid="email" defaultValue="ada@example.com" />
        <Field.Error data-testid="email-error" />
      </Field>
      <Button type="submit" data-testid="submit">
        Sign in
      </Button>
    </Form>
  ),
  play: async ({ canvas }) => {
    await expect(canvas.getByTestId("email-error")).toHaveTextContent(
      "That email is already registered.",
    );

    await userEvent.type(canvas.getByTestId("email"), ".uk");

    // `waitFor`, because the error leaves on a transition: read too early and the
    // element is still in the document carrying `data-ending-style` (measured
    // under CPU contention). Re-query inside the callback — the node the first
    // read returned is the one on its way out.
    await waitFor(async () => {
      await expect(canvas.queryByTestId("email-error")).not.toBeInTheDocument();
    });
  },
};

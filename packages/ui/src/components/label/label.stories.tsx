import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";

import { Field } from "../field/field";
import { Label } from "./label";

/*
 * `Label` is the compat name for `Field.Label`, so every story wraps it in a
 * `<Field>` — outside one, Base UI throws. That constraint is the reason these
 * stories exist separately from the Field ones: they are what a fork looks at
 * when it keeps writing `<Label>` rather than `<Field.Label>`.
 */

const meta = {
  title: "Components/Label",
  component: Label,
  decorators: [
    (Story) => (
      <Field>
        <Story />
      </Field>
    ),
  ],
  args: { children: "Email" },
} satisfies Meta<typeof Label>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {};

/** The pre-rebuild shape: an explicit htmlFor against a hand-written id. */
export const WithExplicitHtmlFor: Story = {
  args: { htmlFor: "email" },
};

export const Disabled: Story = {
  decorators: [
    (Story) => (
      <Field disabled>
        <Story />
        <Field.Control />
      </Field>
    ),
  ],
};

/**
 * The upgrade the alias buys: with no htmlFor, the label finds the field's
 * control by itself.
 */
export const AssociatesWithTheControl: Story = {
  decorators: [
    (Story) => (
      <Field>
        <Story />
        <Field.Control data-testid="associated-control" />
      </Field>
    ),
  ],
  play: async ({ canvas }) => {
    const control = canvas.getByTestId("associated-control");
    const label = canvas.getByText("Email");

    await expect(label).toHaveAttribute("for", control.getAttribute("id"));
  },
};

import type { Meta, StoryObj } from "@storybook/react-vite";
import { expect } from "storybook/test";

import { Field } from "../field/field";
import { Fieldset } from "./fieldset";

/*
 * The visual half of the Fieldset spec. What a reviewer is checking here is the
 * hierarchy — a legend reading as the heading for the labels beneath it — and
 * that a disabled group looks disabled all the way down even though its
 * controls carry no state attribute of their own.
 */

const meta = {
  title: "Components/Fieldset",
  component: Fieldset,
} satisfies Meta<typeof Fieldset>;

export default meta;

type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => (
    <Fieldset>
      <Fieldset.Legend>Contact details</Fieldset.Legend>
      <Field>
        <Field.Label>Email</Field.Label>
        <Field.Control placeholder="name@example.com" />
      </Field>
      <Field>
        <Field.Label>Phone</Field.Label>
        <Field.Control placeholder="+1 555 0100" />
      </Field>
    </Fieldset>
  ),
};

/** A group with no legend — valid, and named by nothing. */
export const WithoutLegend: Story = {
  render: () => (
    <Fieldset>
      <Field>
        <Field.Label>Email</Field.Label>
        <Field.Control placeholder="name@example.com" />
      </Field>
    </Fieldset>
  ),
};

export const Disabled: Story = {
  render: () => (
    <Fieldset disabled>
      <Fieldset.Legend>Contact details</Fieldset.Legend>
      <Field>
        <Field.Label>Email</Field.Label>
        <Field.Control defaultValue="ada@example.com" />
      </Field>
      <Field>
        <Field.Label>Phone</Field.Label>
        <Field.Control defaultValue="+1 555 0100" />
      </Field>
    </Fieldset>
  ),
};

/** The accessible name a fork gets for free by using the legend part. */
export const NamedByItsLegend: Story = {
  render: () => (
    <Fieldset data-testid="named-group">
      <Fieldset.Legend data-testid="named-legend">Contact details</Fieldset.Legend>
      <Field>
        <Field.Label>Email</Field.Label>
        <Field.Control />
      </Field>
    </Fieldset>
  ),
  play: async ({ canvas }) => {
    const group = canvas.getByTestId("named-group");
    const legend = canvas.getByTestId("named-legend");

    await expect(group).toHaveAttribute("aria-labelledby", legend.getAttribute("id"));
  },
};

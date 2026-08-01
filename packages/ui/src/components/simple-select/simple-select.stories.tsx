import type { Meta, StoryObj } from "@storybook/react-vite";
import { useState } from "react";
import type { ReactElement } from "react";
import { expect, screen, userEvent, waitFor } from "storybook/test";

import { SimpleSelect, type SimpleSelectOption } from "./simple-select";

/*
 * SimpleSelect's render and interaction coverage. `@storybook/addon-vitest`
 * turns every export below into a Vitest test case in the same headless
 * Chromium the `browser` project uses, with the real Tailwind pipeline attached
 * — which this component needs: the popup's `role="option"` rows measure 0x0
 * without a stylesheet and every click hangs on Playwright's actionability
 * check.
 *
 * The popup is PORTALLED to <body>, outside the story canvas, so the play
 * functions reach it through `screen` rather than `canvas`.
 */

const STATUSES: readonly SimpleSelectOption[] = [
  { value: "open", label: "Open" },
  { value: "in-progress", label: "In Progress" },
  { value: "closed", label: "Closed" },
];

/**
 * The controlled wrapper every story renders through — the component takes
 * `value` + `onChange`, so a story needs somewhere for the choice to live.
 */
function StatusSelect(props: {
  readonly initialValue?: string;
  readonly placeholder?: string;
}): ReactElement {
  const [value, setValue] = useState<string>(props.initialValue ?? "");

  return (
    <SimpleSelect
      testId="status-select"
      label="Status"
      value={value}
      options={STATUSES}
      onChange={setValue}
      placeholder={props.placeholder}
    />
  );
}

const meta = {
  title: "Components/SimpleSelect",
  component: StatusSelect,
} satisfies Meta<typeof StatusSelect>;

export default meta;

type Story = StoryObj<typeof meta>;

/** Nothing chosen yet: `""` on the caller's side, the placeholder on screen. */
export const Placeholder: Story = {
  args: { placeholder: "Choose a status" },
};

/** A chosen value reported by its LABEL, which is what `items` buys. */
export const Chosen: Story = {
  args: { initialValue: "in-progress" },
  play: async () => {
    await expect(screen.getByTestId("status-select")).toHaveTextContent("In Progress");
  },
};

/** Choosing an option: the popup opens, and the trigger takes the new label. */
export const Choosing: Story = {
  args: { placeholder: "Choose a status" },
  play: async () => {
    const trigger = screen.getByTestId("status-select");

    await userEvent.click(trigger);
    await waitFor(() => {
      expect(trigger).toHaveAttribute("aria-expanded", "true");
    });

    await userEvent.click(screen.getByRole("option", { name: "Closed" }));
    await waitFor(() => {
      expect(trigger).toHaveTextContent("Closed");
    });
  },
};

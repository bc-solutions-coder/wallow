import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import {
  createSdkHarness,
  routeHarness,
  type SdkHarness,
} from "@bc-solutions-coder/testing/sdk-harness";
import { useState } from "react";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { useUserPicker } from "./use-user-picker";

/**
 * The add-member picker's state, driven through its own returned API rather than
 * through the control.
 *
 * `MemberList.picker.test.tsx` covers everything the combobox can reach by
 * typing into it. What is here is the rule that ties the hook's two halves
 * together and which no click can isolate: the popup is open only when the
 * request AND a non-empty match list agree, so a request against a directory
 * that narrowed to nothing keeps it shut.
 */

/** The operation the directory comes from. */
const USERS_PATH = "/v1/identity/users";

/**
 * Ids are opaque on purpose: none of them contains `acme`, `globex` or `carol`,
 * so a filter matching the ID rather than the email could not pass these cases.
 */
const USERS = [
  {
    id: "11111111-1111-1111-1111-111111111111",
    email: "ada@acme.io",
    firstName: "Ada",
    lastName: "Lovelace",
    enabled: true,
    roles: ["Owner"],
  },
  {
    id: "22222222-2222-2222-2222-222222222222",
    email: "Bob@ACME.io",
    firstName: "Bob",
    lastName: "Rivers",
    enabled: true,
    roles: ["Member"],
  },
  {
    id: "33333333-3333-3333-3333-333333333333",
    email: "carol@globex.io",
    firstName: "Carol",
    lastName: "Nguyen",
    enabled: true,
    roles: ["Member"],
  },
];

let harness: SdkHarness;

/**
 * Renders the hook's state as text and exposes its two inputs — the control's
 * text and the open REQUEST — as controls, so a spec drives it directly.
 */
function Probe() {
  const [value, setValue] = useState("");
  const { matches, open, onOpenChange } = useUserPicker(value);

  return (
    <div>
      <output data-testid="probe-matches">
        {matches.map((user) => user.email).join(",") || "none"}
      </output>
      <output data-testid="probe-open">{open ? "open" : "closed"}</output>
      <input
        aria-label="value"
        data-testid="probe-value"
        value={value}
        onChange={(event) => {
          setValue(event.target.value);
        }}
      />
      <button
        type="button"
        data-testid="probe-request-open"
        onClick={() => {
          onOpenChange(true);
        }}
      >
        request open
      </button>
    </div>
  );
}

function matches(): string {
  return page.getByTestId("probe-matches").element().textContent ?? "";
}

/** Wait for the directory read to land — until it does, everything narrows to nothing. */
async function awaitDirectory(): Promise<void> {
  await expect.element(page.getByTestId("probe-matches")).toHaveTextContent("ada@acme.io");
}

describe("useUserPicker", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    routeHarness(harness, {
      [`GET ${USERS_PATH}`]: { items: USERS, totalCount: USERS.length, page: 1, pageSize: 20 },
    });
  });

  it("offers the whole directory once the read lands", async () => {
    renderWithWallow(<Probe />, { harness });

    await awaitDirectory();
    expect(matches()).toBe("ada@acme.io,Bob@ACME.io,carol@globex.io");
  });

  it("narrows by email substring, ignoring case and surrounding space", async () => {
    renderWithWallow(<Probe />, { harness });
    await awaitDirectory();

    // Leading space and a capital: the operator's typing, not a normalised value.
    await userEvent.fill(page.getByTestId("probe-value"), "  ACME");

    await expect.poll(() => matches()).toBe("ada@acme.io,Bob@ACME.io");
  });

  it("stays closed until the control asks", async () => {
    renderWithWallow(<Probe />, { harness });
    await awaitDirectory();

    await expect.element(page.getByTestId("probe-open")).toHaveTextContent("closed");
  });

  it("opens when the control asks and there is something to show", async () => {
    renderWithWallow(<Probe />, { harness });
    await awaitDirectory();

    await userEvent.click(page.getByTestId("probe-request-open"));

    await expect.element(page.getByTestId("probe-open")).toHaveTextContent("open");
  });

  it("stays closed when the control asks but nothing matches", async () => {
    // The live case: the input's text is a user ID once somebody has picked, and
    // no email contains it. An open popup here would sit empty over the submit
    // button and have `aria-expanded` claim a list that is not there.
    renderWithWallow(<Probe />, { harness });
    await awaitDirectory();

    await userEvent.click(page.getByTestId("probe-request-open"));
    await expect.element(page.getByTestId("probe-open")).toHaveTextContent("open");

    await userEvent.fill(page.getByTestId("probe-value"), "11111111-1111-1111-1111-111111111111");

    await expect.poll(() => matches()).toBe("none");
    await expect.element(page.getByTestId("probe-open")).toHaveTextContent("closed");
  });
});

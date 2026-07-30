import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { MemberList } from "./MemberList";

/**
 * Accessible-name spec for the add-member form (Wallow-lrlm.4.4).
 *
 * `AddMemberForm` renders `<Field><Input data-testid="organization-member-userid"
 * /></Field>` — a `Field.Root` with no `Field.Label` inside it, so there is
 * nothing for the root to associate and the input reaches the accessibility
 * tree unnamed. The visible "Add member" button names the SUBMIT, not the box,
 * and the form has no heading of its own, so a screen-reader user is asked for
 * a value with no indication that it is a user id.
 *
 * The gap is identical to the one in the sibling org-detail register-client
 * form; both are `Field`-without-`Label`, and the correct shape already ships in
 * `features/mfa/components/MfaSettingsSection.tsx`'s `ConfirmPanel`
 * (`<Field><Label htmlFor=…/><Input id=…/></Field>`).
 */

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

describe("MemberList — accessible names", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    // The add-member form is NOT query-backed, so it paints regardless of the
    // members read; answering with an empty roster keeps the console quiet.
    harness.resolveJson([]);
  });

  it("gives the add-member user-id input a non-empty accessible name", async () => {
    renderWithWallow(<MemberList orgId="o1" />, { harness });

    await expect.element(page.getByTestId("organization-member-userid")).toHaveAccessibleName();
  });

  it("names the add-member input after the value it collects", async () => {
    renderWithWallow(<MemberList orgId="o1" />, { harness });

    // Loose on purpose: "User ID", "User id", "Member user ID" all pass. What
    // must not pass is a control announced with no name, or one named only by
    // the button beside it.
    await expect
      .element(page.getByTestId("organization-member-userid"))
      .toHaveAccessibleName(/user id/i);
  });
});

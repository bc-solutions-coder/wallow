import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import { routeHarness } from "@shared/testing/harness-routes";
import { OrganizationDetail } from "./OrganizationDetail";

/**
 * Accessible-name spec for the org-detail register-client form
 * (Wallow-lrlm.4.4).
 *
 * `RegisterClientForm` ships THREE controls and names none of them:
 *
 *   - `organization-detail-register-display-name` — an `Input` inside a bare
 *     `Field`. `Field.Root` associates a `Field.Label` with its control, but
 *     this field has no label part inside it at all, so there is nothing to
 *     associate and the input reaches the a11y tree unnamed.
 *   - `organization-detail-register-redirect-uris` — the SECOND bare
 *     `<textarea>` in this app (the bead says "at least one"; there are two).
 *     It sits immediately after the `Field`-wrapped input, so the form is
 *     already inconsistent with itself.
 *   - `organization-detail-register-client-type` — the `SelectControl` trigger,
 *     which renders as `<button role="combobox">Public</button>`. `combobox`
 *     does NOT take its name from its contents, so the chosen option is the
 *     control's value and nothing else names it: it too reaches the a11y tree
 *     unnamed. `SelectControl` accepts no accessible-name prop at all today, and
 *     the inquiry-status trigger shares the gap.
 *
 * The already-correct reference in the same repo is
 * `features/apps/components/RegisterAppForm.tsx`, whose migrated `forms`
 * catalog renders `label="Display name"` / `label="Redirect URIs"` /
 * `label="Client type"` for the identical three controls. These assertions
 * deliberately reuse that wording so the two register forms converge instead of
 * inventing a third vocabulary.
 */

const org = { id: "o1", name: "Acme", domain: "acme.io", memberCount: "2" };

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/** Answer the org / members / clients reads so the register form is on screen. */
function seedLoadedOrg(): void {
  routeHarness(
    harness,
    {
      "GET /v1/identity/organizations/o1": org,
      "GET /v1/identity/organizations/o1/members": [],
      "GET /v1/identity/clients/by-tenant/o1": [],
    },
    { fallback: [] },
  );
}

/** The register form only exists once the org read resolves. */
async function awaitLoaded(): Promise<void> {
  await expect.element(page.getByTestId("organization-detail-register-form")).toBeInTheDocument();
}

describe("OrganizationDetail — register-client accessible names", () => {
  beforeEach(() => {
    harness = createSdkHarness();
    seedLoadedOrg();
  });

  it("gives the register-client display-name input a non-empty accessible name", async () => {
    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await expect
      .element(page.getByTestId("organization-detail-register-display-name"))
      .toHaveAccessibleName();
  });

  it("gives the register-client redirect-URIs textarea a non-empty accessible name", async () => {
    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris"))
      .toHaveAccessibleName();
  });

  it("names the register-client controls after what they collect", async () => {
    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    // The same wording the migrated `RegisterAppForm` already uses for the same
    // two fields, matched loosely so a longer sentence ("App display name")
    // still satisfies it.
    await expect
      .element(page.getByTestId("organization-detail-register-display-name"))
      .toHaveAccessibleName(/display name/i);
    await expect
      .element(page.getByTestId("organization-detail-register-redirect-uris"))
      .toHaveAccessibleName(/redirect uris?/i);
  });

  it("names the client-type select after the choice it governs", async () => {
    renderWithWallow(<OrganizationDetail orgId="o1" />, { harness });
    await awaitLoaded();

    // Today the trigger has no accessible name at all: `combobox` takes no name
    // from its contents, so the visible "Public" is the control's value and
    // nothing supplies a name. The assertion is therefore on the PURPOSE, which
    // is what the missing name has to say.
    await expect
      .element(page.getByTestId("organization-detail-register-client-type"))
      .toHaveAccessibleName(/client type/i);
  });
});

import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { beforeEach, describe, expect, it } from "vitest";

import {
  allByTestId,
  byTestId,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
  within,
} from "../../../test/style-contract";
import { ProfileSection } from "./ProfileSection";

/**
 * Restyle spec for the settings profile section (Wallow-urec.4.4). The profile
 * is form-shaped, so unlike the list pages (`.4.1`) it KEEPS the `ui` Card and
 * gains the old design's labelled field rows: a small uppercase caption above
 * each read-only value.
 *
 * Behaviour — which state renders when, and the `settings-profile-name` /
 * `-email` / `-roles` / `-role` / `-no-roles` / `-loading` testids — stays pinned
 * by the sibling `ProfileSection.test.tsx`, which the restyle must not edit.
 */

/** The uppercase caption above each read-only value (ported from the old design). */
const FIELD_LABEL = "block text-xs font-semibold text-foreground/70 uppercase tracking-wider mb-1";

/** A read-only field value. */
const FIELD_VALUE = "text-sm text-foreground";

/** The shared status/type pill from the Phase 4 recipe. */
const CHIP =
  "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full";

/** `ui` Card's rendered surface at its default spacing. */
const CARD = "rounded-lg border border-border bg-card p-6 space-y-6";

const PROFILE = {
  id: "u1",
  email: "ada@lovelace.io",
  firstName: "Ada",
  lastName: "Lovelace",
  roles: ["Owner", "Admin"],
  permissions: [],
};

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Render the section answering the current-user read with `profile` (omit for
 * the loading state, which a never-settling request drives) and resolve the
 * settled element named by `anchor` — the testid of the state under test.
 */
async function renderSection(profile: unknown | undefined, anchor: string): Promise<HTMLElement> {
  if (profile === undefined) {
    harness.pending();
  } else {
    harness.resolveJson(profile);
  }
  renderWithWallow(<ProfileSection />, { harness });
  return waitForTestId(anchor);
}

/**
 * The `ui` Card surface containing `element`. Located by its token surface rather
 * than by counting parents, so the restyle stays free to group fields as it likes.
 */
function cardOf(element: HTMLElement): HTMLElement {
  const card = element.closest("div.bg-card");
  expect(card, "expected a ui Card (div.bg-card) ancestor").not.toBeNull();
  return card as HTMLElement;
}

/** The labelled field row wrapping a value element, plus its caption. */
function fieldOf(value: HTMLElement): { row: HTMLElement; label: HTMLElement } {
  const row = parentOf(value);
  return { row, label: within(row, "span") };
}

describe("ProfileSection (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("keeps the profile on the ui card surface under its title", async () => {
    const name = await renderSection(PROFILE, "settings-profile-name");

    const card = cardOf(name);
    expectClasses(card, CARD);

    const title = within(card, "h2");
    expect(title.textContent).toBe("Profile");
    expectClasses(title, "text-lg font-semibold text-card-foreground");
  });

  it("captions the name field without changing its value element", async () => {
    const name = await renderSection(PROFILE, "settings-profile-name");

    const { label } = fieldOf(name);
    expect(label.textContent).toBe("Name");
    expectClasses(label, FIELD_LABEL);

    expectTag(name, "div");
    expectClasses(name, FIELD_VALUE);
    // Regression guard: the restyle adds a caption, it does not touch the value.
    expect(name.textContent).toBe("Ada Lovelace");
  });

  it("captions the email field without changing its value element", async () => {
    await renderSection(PROFILE, "settings-profile-name");

    const email = byTestId("settings-profile-email");
    const { label } = fieldOf(email);
    expect(label.textContent).toBe("Email");
    expectClasses(label, FIELD_LABEL);

    expectTag(email, "div");
    expectClasses(email, FIELD_VALUE);
    expect(email.textContent).toBe("ada@lovelace.io");
  });

  it("captions the roles field and wraps its chips", async () => {
    const roles = await renderSection(PROFILE, "settings-profile-roles");

    const { label } = fieldOf(roles);
    expect(label.textContent).toBe("Roles");
    expectClasses(label, FIELD_LABEL);

    expectTag(roles, "div");
    expectClasses(roles, "flex flex-wrap gap-2");
  });

  it("renders every role as a chip", async () => {
    await renderSection(PROFILE, "settings-profile-roles");

    const chips = allByTestId("settings-profile-role");
    expect(chips).toHaveLength(PROFILE.roles.length);
    for (const chip of chips) {
      expectTag(chip, "span");
      expectClasses(chip, CHIP);
    }
    expect(chips[0].textContent).toBe("Owner");
  });

  it("keeps the no-roles sentence inside the captioned roles field", async () => {
    const noRoles = await renderSection({ ...PROFILE, roles: [] }, "settings-profile-no-roles");

    // MutedText renders a <p>; the empty case only gains the field caption.
    expectTag(noRoles, "p");
    expect(noRoles.textContent).toBe("No roles assigned.");

    const { label } = fieldOf(noRoles);
    expect(label.textContent).toBe("Roles");
    expectClasses(label, FIELD_LABEL);
  });

  it("centers the loading state without changing its wording", async () => {
    const loading = await renderSection(undefined, "settings-profile-loading");

    expect(loading.textContent).toBe("Loading profile…");
    expectClasses(loading, "text-center py-12");
  });

  it("styles the profile with theme tokens only", async () => {
    const name = await renderSection(PROFILE, "settings-profile-name");

    expectTokenColorsOnly(cardOf(name));
  });
});

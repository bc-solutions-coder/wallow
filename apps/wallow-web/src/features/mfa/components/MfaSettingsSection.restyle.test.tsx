import { createSdkHarness, type SdkHarness } from "@bc-solutions-coder/testing/sdk-harness";
import { renderWithWallow } from "@bc-solutions-coder/testing/render-with-wallow";
import { page, userEvent } from "vitest/browser";
import { beforeEach, describe, expect, it } from "vitest";

import {
  byTestId,
  expectBadge,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
  within,
} from "@shared/testing/style-contract";
import { MfaSettingsSection } from "./MfaSettingsSection";

/** The transport backing each render, rebuilt per test. */
let harness: SdkHarness;

/**
 * Restyle spec for the MFA settings section (Wallow-urec.4.4). Like the profile
 * section it stays on the `ui` Card and gains the old design's labelled field
 * rows; unlike it, the card also regains the section title the Blazor original
 * carried ("Multi-Factor Authentication") and sits `mt-6` below the profile.
 *
 * Behaviour — every `settings-mfa-*` testid, the Disabled/Enabled wording, the
 * confirm-panel flow, and the regenerated-codes reveal — stays pinned by the
 * sibling `MfaSettingsSection.test.tsx`, which the restyle must not edit.
 *
 * Wallow-lrlm.5.2 moves the status pill onto the catalog `Badge`, and the two
 * states stop being the same chip. This file's own comment recorded why they
 * were identical: "the old design tinted this by state (green when enabled);
 * there is no success token in the theme, so the chip stays state-independent
 * rather than reaching for a raw palette hue." F1.T1 added `--color-success` /
 * `--color-success-foreground` and `Badge` exposes them as `variant="success"`,
 * so the stated blocker is gone and the design's intent is now expressible in
 * tokens. Enabled is `success`; Disabled stays `neutral`.
 *
 * This is the one judgement call in the task that is not forced by the catalog —
 * flagged on the bead. If the verifier rules the tint out of F5.T2's scope it
 * reverts to `neutral` here and in the component, and nothing else moves.
 *
 * The regenerated-codes `<ul>` is NOT a `ListCard`: it reveals one-time codes as
 * a plain content list inside `PANEL`, with no card surface, no row cell and no
 * per-row id. `features/list-catalog.test.ts` exempts it by name.
 */

/** The uppercase caption above each read-only value (ported from the old design). */
const FIELD_LABEL =
  "block text-xs font-semibold text-muted-foreground uppercase tracking-wider mb-1";

/** A read-only field value. */
const FIELD_VALUE = "text-sm text-foreground";

/** `ui` Card's rendered surface at its default spacing, plus the section offset. */
const CARD = "rounded-lg border border-border bg-card p-6 space-y-6 mt-6";

/** The confirm / reveal panels nested inside the card. */
const PANEL = "rounded-md border border-border p-4";

const DISABLED_STATUS = { enabled: false, method: null, backupCodeCount: 0 };
const ENABLED_STATUS = { enabled: true, method: "totp", backupCodeCount: 7 };

const REGENERATE_PATH = "/api/v1/identity/mfa/backup-codes/regenerate";

function json(body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status: 200,
    headers: { "content-type": "application/json" },
  });
}

/**
 * Render the section seeded with `status` (omit for the loading state) and
 * resolve the settled element named by `anchor` — the testid of the state under
 * test.
 */
async function renderSection(status: unknown | undefined, anchor: string): Promise<HTMLElement> {
  if (status !== undefined) {
    harness.resolveJson(status);
  }
  renderWithWallow(<MfaSettingsSection />, { harness });
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

/** Open the shared password-confirm panel from the enabled card's `action`. */
async function openConfirm(action: "disable" | "regenerate"): Promise<HTMLElement> {
  await renderSection(ENABLED_STATUS, `settings-mfa-${action}`);
  await userEvent.click(page.getByTestId(`settings-mfa-${action}`));
  return waitForTestId("settings-mfa-confirm-submit");
}

describe("MfaSettingsSection (restyle)", () => {
  beforeEach(() => {
    harness = createSdkHarness();
  });

  it("sits on its own titled card below the profile section", async () => {
    const status = await renderSection(DISABLED_STATUS, "settings-mfa-status");

    const card = cardOf(status);
    expectClasses(card, CARD);

    const title = within(card, "h2");
    expect(title.textContent).toBe("Multi-Factor Authentication");
    // `text-xl` since Wallow-io5f made 20px the catalog-wide heading standard.
    // Presence only — the computed size is measured in `src/heading-scale.test.tsx`.
    expectClasses(title, "text-xl font-semibold text-card-foreground");
  });

  it("captions the status field and renders Disabled as a chip", async () => {
    const status = await renderSection(DISABLED_STATUS, "settings-mfa-status");

    const { label } = fieldOf(status);
    expect(label.textContent).toBe("Status");
    expectClasses(label, FIELD_LABEL);

    expectBadge(status, "neutral");
    // Regression guard: the chip is the same element, with the same word.
    expect(status.textContent).toBe("Disabled");
  });

  it("tints the Enabled chip with the success token", async () => {
    const status = await renderSection(ENABLED_STATUS, "settings-mfa-status");

    expectBadge(status, "success");
    expect(status.textContent).toBe("Enabled");
  });

  it("captions the backup-code count without changing its value element", async () => {
    const count = await renderSection(ENABLED_STATUS, "settings-mfa-backup-count");

    const { label } = fieldOf(count);
    expect(label.textContent).toBe("Backup Codes Remaining");
    expectClasses(label, FIELD_LABEL);

    expectTag(count, "span");
    expectClasses(count, FIELD_VALUE);
    expect(count.textContent).toBe("7");
  });

  it("lays the enabled actions out in one action row", async () => {
    await renderSection(ENABLED_STATUS, "settings-mfa-disable");

    // A grid, not a flex row: `ui` Button is `w-full`, so the cells size the
    // buttons instead of fighting the primitive's own width.
    const actions = parentOf(byTestId("settings-mfa-disable"));
    expectClasses(actions, "grid gap-3 sm:grid-cols-2");
    expect(actions.contains(byTestId("settings-mfa-regenerate"))).toBe(true);
  });

  it("frames the shared confirm panel", async () => {
    const submit = await openConfirm("disable");

    const panel = parentOf(submit);
    expectClasses(panel, `${PANEL} bg-muted space-y-3`);
    expect(panel.contains(byTestId("settings-mfa-confirm-password"))).toBe(true);
  });

  it("frames the regenerated-codes reveal without changing its copy", async () => {
    harness.respond((call) =>
      call.path === REGENERATE_PATH ? json({ codes: ["z1", "z2"] }) : json(ENABLED_STATUS),
    );
    renderWithWallow(<MfaSettingsSection />, { harness });
    await userEvent.click(await waitForTestId("settings-mfa-regenerate"));
    await waitForTestId("settings-mfa-confirm-submit");
    await userEvent.type(page.getByTestId("settings-mfa-confirm-password"), "hunter2");
    await userEvent.click(page.getByTestId("settings-mfa-confirm-submit"));

    const codes = await waitForTestId("settings-mfa-regenerated-codes");
    expectTag(codes, "ul");
    expectClasses(codes, "font-mono text-sm space-y-1 text-foreground");

    const panel = parentOf(codes);
    expectClasses(panel, PANEL);

    const intro = within(panel, "p");
    expect(intro.textContent).toBe(
      "New backup codes — save these somewhere safe. They will not be shown again.",
    );
    expectClasses(intro, "text-sm font-semibold text-foreground mb-2");
  });

  it("centers the loading state without changing its wording", async () => {
    harness.pending();
    const loading = await renderSection(undefined, "settings-mfa-loading");

    expect(loading.textContent).toBe("Loading MFA status…");
    expectClasses(loading, "text-center py-12");
  });

  it("styles the disabled card with theme tokens only", async () => {
    const status = await renderSection(DISABLED_STATUS, "settings-mfa-status");

    expectTokenColorsOnly(cardOf(status));
  });

  it("styles the enabled card with theme tokens only", async () => {
    const status = await renderSection(ENABLED_STATUS, "settings-mfa-status");

    expectTokenColorsOnly(cardOf(status));
  });
});

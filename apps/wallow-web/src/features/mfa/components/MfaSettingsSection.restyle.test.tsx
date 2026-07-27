import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import type { ReactElement } from "react";
import { page, userEvent } from "vitest/browser";
import { render } from "vitest-browser-react";
import { beforeEach, describe, expect, it } from "vitest";

import { installSdkClientMock, type SdkClientMock } from "../../../test/sdk-client-mock";
import {
  byTestId,
  expectClasses,
  expectTag,
  expectTokenColorsOnly,
  parentOf,
  waitForTestId,
  within,
} from "../../../test/style-contract";
import { MfaSettingsSection } from "./MfaSettingsSection";

/**
 * Restyle spec for the MFA settings section (Wallow-urec.4.4). Like the profile
 * section it stays on the `ui` Card and gains the old design's labelled field
 * rows; unlike it, the card also regains the section title the Blazor original
 * carried ("Multi-Factor Authentication") and sits `mt-6` below the profile.
 *
 * Behaviour — every `settings-mfa-*` testid, the Disabled/Enabled wording, the
 * confirm-panel flow, and the regenerated-codes reveal — stays pinned by the
 * sibling `MfaSettingsSection.test.tsx`, which the restyle must not edit.
 */

/** The uppercase caption above each read-only value (ported from the old design). */
const FIELD_LABEL = "block text-xs font-semibold text-foreground/70 uppercase tracking-wider mb-1";

/** A read-only field value. */
const FIELD_VALUE = "text-sm text-foreground";

/** The shared status/type pill from the Phase 4 recipe. */
const CHIP =
  "inline-block bg-accent text-accent-foreground text-xs font-medium px-2.5 py-0.5 rounded-full";

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

function newClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false, staleTime: Number.POSITIVE_INFINITY },
      mutations: { retry: false },
    },
  });
}

function renderWithClient(client: QueryClient, ui: ReactElement) {
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

/**
 * Render the section seeded with `status` (omit for the loading state) and
 * resolve the settled element named by `anchor` — the testid of the state under
 * test.
 */
async function renderSection(status: unknown | undefined, anchor: string): Promise<HTMLElement> {
  const client = newClient();
  if (status !== undefined) {
    client.setQueryData(["mfa", "status"], status);
  }
  renderWithClient(client, <MfaSettingsSection />);
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
  let sdk: SdkClientMock;

  beforeEach(() => {
    sdk = installSdkClientMock();
  });

  it("sits on its own titled card below the profile section", async () => {
    const status = await renderSection(DISABLED_STATUS, "settings-mfa-status");

    const card = cardOf(status);
    expectClasses(card, CARD);

    const title = within(card, "h2");
    expect(title.textContent).toBe("Multi-Factor Authentication");
    expectClasses(title, "text-lg font-semibold text-card-foreground");
  });

  it("captions the status field and renders Disabled as a chip", async () => {
    const status = await renderSection(DISABLED_STATUS, "settings-mfa-status");

    const { label } = fieldOf(status);
    expect(label.textContent).toBe("Status");
    expectClasses(label, FIELD_LABEL);

    expectTag(status, "span");
    expectClasses(status, CHIP);
    // Regression guard: the chip is the same element, with the same word.
    expect(status.textContent).toBe("Disabled");
  });

  it("renders Enabled as the same chip", async () => {
    const status = await renderSection(ENABLED_STATUS, "settings-mfa-status");

    expectClasses(status, CHIP);
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
    expectClasses(panel, `${PANEL} bg-background/50 space-y-3`);
    expect(panel.contains(byTestId("settings-mfa-confirm-password"))).toBe(true);
  });

  it("frames the regenerated-codes reveal without changing its copy", async () => {
    sdk.respond((call) =>
      call.path === REGENERATE_PATH ? json({ codes: ["z1", "z2"] }) : json(ENABLED_STATUS),
    );
    await openConfirm("regenerate");
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
    sdk.pending();
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

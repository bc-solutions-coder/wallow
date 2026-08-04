/**
 * MFA settings status card (Wallow-8w1h.6.4) — the status + actions card that
 * lives alongside the profile section on the settings route.
 *
 * The card is presentational: `useMfaSettings` owns the status read, the two
 * writes, and every branch-selecting piece of state, so what is left here is
 * which surface each state renders.
 *
 * It renders:
 *   - `settings-mfa-status` — "Enabled"/"Disabled" text.
 *   - When DISABLED: `settings-mfa-enable`; clicking it enters the inline
 *     `MfaEnrollFlow` (no cross-app redirect — the SPA is same-origin).
 *   - When ENABLED: `settings-mfa-backup-count`, plus `settings-mfa-disable`
 *     and `settings-mfa-regenerate`. Each opens a shared password-confirm panel
 *     (`settings-mfa-confirm-password` + `settings-mfa-confirm-submit`) driving
 *     the `disable` / `regenerateBackupCodes` mutations.
 *   - `settings-mfa-error` — shared RFC 7807 error surface for the two
 *     MUTATIONS (disable / regenerate).
 *   - `settings-mfa-status-error` — the initial status READ's own failure
 *     surface. It is deliberately NOT `settings-mfa-error`: the two never
 *     co-render, but a spec (or a reader) asserting on one testid has to be able
 *     to say WHICH failure it saw, and the E2E page object already binds
 *     `settings-mfa-error` to the confirm-panel flow.
 *
 * Testids mirror the C# E2E page object `SettingsMfaSection`.
 */
import { AppForm, SubmitButton, useAppForm } from "@bc-solutions-coder/forms";
import {
  Badge,
  Button,
  Card,
  CardTitle,
  ErrorBanner,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import type { ReactNode } from "react";
import { z } from "zod";

import { useMfaSettings } from "../hooks/use-mfa-settings";
import { MfaEnrollFlow } from "./MfaEnrollFlow";

/** The framed sub-panels nested inside the card (confirm + codes reveal). */
const PANEL = "rounded-md border border-border p-4";

/** A captioned read-only field row (extracted to keep the card's JSX shallow). */
function MfaField(props: { label: string; children: ReactNode }) {
  return (
    <div>
      {/* `overline` IS the uppercase caption scale; only the layout stays local. */}
      <Text as="span" variant="overline" color="muted" className="block mb-1">
        {props.label}
      </Text>
      {props.children}
    </div>
  );
}

/** DISABLED-state affordances: status text + the enable CTA. */
function DisabledCard(props: { onEnable: () => void }) {
  return (
    <div className="space-y-4">
      <MfaField label="Status">
        <Badge data-testid="settings-mfa-status">Disabled</Badge>
      </MfaField>
      <Button type="button" data-testid="settings-mfa-enable" onClick={props.onEnable}>
        Enable MFA
      </Button>
    </div>
  );
}

/** ENABLED-state affordances: status + backup count + disable/regenerate. */
function EnabledCard(props: {
  // The OpenAPI document types the count as `number | string`, so the card
  // renders whichever the API sent rather than casting one into the other.
  backupCodeCount: number | string;
  onDisable: () => void;
  onRegenerate: () => void;
}) {
  const { backupCodeCount, onDisable, onRegenerate } = props;
  return (
    <div className="space-y-4">
      {/* The old design tinted this green when enabled and the port could not:
          the theme had no success token. F1.T1 added `--color-success`, so the
          state the card reports is expressible in tokens again. */}
      <MfaField label="Status">
        <Badge variant="success" data-testid="settings-mfa-status">
          Enabled
        </Badge>
      </MfaField>
      <MfaField label="Backup Codes Remaining">
        <Text as="span" variant="bodySm" data-testid="settings-mfa-backup-count">
          {backupCodeCount}
        </Text>
      </MfaField>
      {/* A grid, not a flex row: `ui` Button is `w-full`, so the cells size the
          buttons instead of fighting the primitive's own width. */}
      <div className="grid gap-3 sm:grid-cols-2">
        <Button
          type="button"
          variant="destructive"
          data-testid="settings-mfa-disable"
          onClick={onDisable}
        >
          Disable MFA
        </Button>
        <Button
          type="button"
          variant="secondary"
          data-testid="settings-mfa-regenerate"
          onClick={onRegenerate}
        >
          Regenerate backup codes
        </Button>
      </div>
    </div>
  );
}

/**
 * The one value the confirm panel collects. Its NAME is load-bearing:
 * `settings-mfa-confirm` + `password` is what derives the
 * `settings-mfa-confirm-password` testid the panel had before the migration.
 */
interface ConfirmValues {
  readonly password: string;
}

const confirmSchema = z.object({
  password: z.string().min(1, "Enter your password to continue."),
});

const NO_PASSWORD: ConfirmValues = { password: "" };

/**
 * Shared password-confirm panel reused by both the disable and regenerate flows.
 *
 * It takes the plain-`onSubmit` escape hatch rather than a mutation: the write
 * belongs to `useMfaSettings`, which drives both actions off one `confirmAction`
 * and owns the card's own `settings-mfa-error` banner. `submitConfirm` settles
 * without rejecting, so awaiting it gives `SubmitButton` a real `pending` while
 * the failure stays on the card's banner instead of raising a second one here.
 */
function ConfirmPanel(props: { onConfirm: (password: string) => Promise<void> }) {
  const { onConfirm } = props;
  const form = useAppForm({
    schema: confirmSchema,
    defaultValues: NO_PASSWORD,
    onSubmit: (values: ConfirmValues) => onConfirm(values.password),
  });

  return (
    <AppForm form={form} testIdPrefix="settings-mfa-confirm" className={`${PANEL} bg-muted`}>
      <form.AppField name="password">
        {(field) => <field.PasswordField label="Password" autoComplete="current-password" />}
      </form.AppField>
      <SubmitButton pendingLabel="Confirming…">Confirm</SubmitButton>
    </AppForm>
  );
}

/**
 * One-time reveal of freshly regenerated backup codes (one child per code),
 * shown in a "New Backup Codes" panel. Shown after a
 * successful regenerate because the old codes are now invalidated.
 */
function RegeneratedCodes(props: { codes: readonly string[] }) {
  return (
    <div className={PANEL}>
      <Text as="p" variant="bodySm" weight="semibold" className="mb-2">
        New backup codes — save these somewhere safe. They will not be shown again.
      </Text>
      <ul
        data-testid="settings-mfa-regenerated-codes"
        className="font-mono text-sm space-y-1 text-foreground"
      >
        {props.codes.map((codeValue) => (
          <li key={codeValue}>{codeValue}</li>
        ))}
      </ul>
    </div>
  );
}

export function MfaSettingsSection() {
  // Destructured rather than kept as one object: `react/jsx-handler-names`
  // checks member expressions but not plain identifiers, so `onDone={mfa.
  // endEnroll}` is a warning where `onDone={endEnroll}` is not.
  const {
    isPending,
    statusErrorText,
    enabled,
    backupCodeCount,
    enrolling,
    confirmAction,
    error,
    regeneratedCodes,
    beginEnroll,
    endEnroll,
    openConfirm,
    submitConfirm,
  } = useMfaSettings();

  if (isPending) {
    return (
      <MutedText data-testid="settings-mfa-loading" className="text-center py-12">
        Loading MFA status…
      </MutedText>
    );
  }

  if (statusErrorText !== null) {
    return <ErrorBanner data-testid="settings-mfa-status-error">{statusErrorText}</ErrorBanner>;
  }

  if (enrolling) {
    return <MfaEnrollFlow onDone={endEnroll} onCancel={endEnroll} />;
  }

  return (
    <Card className="mt-6">
      <CardTitle>Multi-Factor Authentication</CardTitle>

      {enabled ? (
        <EnabledCard
          backupCodeCount={backupCodeCount}
          onDisable={() => {
            openConfirm("disable");
          }}
          onRegenerate={() => {
            openConfirm("regenerate");
          }}
        />
      ) : (
        <DisabledCard onEnable={beginEnroll} />
      )}

      {/* Keyed by action so switching between disable and regenerate mounts a
          FRESH form: the panel owns the typed password now, and carrying it
          across a switch would arm the other action with it. */}
      {confirmAction === null ? null : (
        <ConfirmPanel key={confirmAction} onConfirm={submitConfirm} />
      )}

      {regeneratedCodes === null ? null : <RegeneratedCodes codes={regeneratedCodes} />}

      {error === null ? null : <ErrorBanner data-testid="settings-mfa-error">{error}</ErrorBanner>}
    </Card>
  );
}

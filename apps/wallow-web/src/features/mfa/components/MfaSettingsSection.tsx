/**
 * MFA settings status card (Wallow-8w1h.6.4) — the status + actions card that
 * lives alongside the profile section on the settings route, mirroring the
 * Blazor oracle `api/src/Wallow.Web/Components/Shared/MfaSettingsSection.razor`.
 *
 * Drives `useQuery(mfaQueries.status())` and renders:
 *   - `settings-mfa-status` — "Enabled"/"Disabled" text.
 *   - When DISABLED: `settings-mfa-enable`; clicking it enters the inline
 *     `MfaEnrollFlow` (no cross-app redirect — the SPA is same-origin).
 *   - When ENABLED: `settings-mfa-backup-count`, plus `settings-mfa-disable`
 *     and `settings-mfa-regenerate`. Each opens a shared password-confirm panel
 *     (`settings-mfa-confirm-password` + `settings-mfa-confirm-submit`) driving
 *     the `disable` / `regenerateBackupCodes` mutations.
 *   - `settings-mfa-error` — shared RFC 7807 error surface.
 *
 * Testids mirror the C# E2E page object `SettingsMfaSection`.
 */
import { Button, Card, ErrorBanner, Field, Input, Label, MutedText } from "@bc-solutions-coder/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { disableMfaMutation, mfaQueries, regenerateBackupCodesMutation } from "../api";
import { problemDetail } from "../errors";
import type { MfaRegenerateBackupCodesResponse, MfaStatusResponse } from "../types";
import { MfaEnrollFlow } from "./MfaEnrollFlow";

/** Which enabled-only action opened the shared password-confirm panel. */
type ConfirmAction = "disable" | "regenerate";

const CONFIRM_FAILED = "Unable to complete that action.";

/** DISABLED-state affordances: status text + the enable CTA. */
function DisabledCard(props: { onEnable: () => void }) {
  return (
    <div>
      <span data-testid="settings-mfa-status">Disabled</span>
      <Button type="button" data-testid="settings-mfa-enable" onClick={props.onEnable}>
        Enable MFA
      </Button>
    </div>
  );
}

/** ENABLED-state affordances: status + backup count + disable/regenerate. */
function EnabledCard(props: {
  backupCodeCount: number;
  onDisable: () => void;
  onRegenerate: () => void;
}) {
  const { backupCodeCount, onDisable, onRegenerate } = props;
  return (
    <div>
      <span data-testid="settings-mfa-status">Enabled</span>
      <span data-testid="settings-mfa-backup-count">{backupCodeCount}</span>
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
  );
}

/** Shared password-confirm panel reused by both the disable and regenerate flows. */
function ConfirmPanel(props: {
  password: string;
  onPasswordChange: (value: string) => void;
  onSubmit: () => void;
}) {
  const { password, onPasswordChange, onSubmit } = props;
  return (
    <div>
      <Field>
        <Label htmlFor="settings-mfa-confirm-password-input">Password</Label>
        <Input
          id="settings-mfa-confirm-password-input"
          type="password"
          data-testid="settings-mfa-confirm-password"
          value={password}
          onChange={(e) => {
            onPasswordChange(e.target.value);
          }}
        />
      </Field>
      <Button type="button" data-testid="settings-mfa-confirm-submit" onClick={onSubmit}>
        Confirm
      </Button>
    </div>
  );
}

/**
 * One-time reveal of freshly regenerated backup codes (one child per code),
 * mirroring the Blazor oracle's "New Backup Codes" panel. Shown after a
 * successful regenerate because the old codes are now invalidated.
 */
function RegeneratedCodes(props: { codes: string[] }) {
  return (
    <div>
      <p>New backup codes — save these somewhere safe. They will not be shown again.</p>
      <ul data-testid="settings-mfa-regenerated-codes">
        {props.codes.map((codeValue) => (
          <li key={codeValue}>{codeValue}</li>
        ))}
      </ul>
    </div>
  );
}

export function MfaSettingsSection() {
  const queryClient = useQueryClient();
  const { data, isPending } = useQuery(mfaQueries.status());
  const disable = useMutation(disableMfaMutation(queryClient));
  const regenerate = useMutation(regenerateBackupCodesMutation(queryClient));

  const [enrolling, setEnrolling] = useState(false);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [regeneratedCodes, setRegeneratedCodes] = useState<string[] | null>(null);

  if (isPending) {
    return <MutedText data-testid="settings-mfa-loading">Loading MFA status…</MutedText>;
  }

  // The facade returns status as `unknown`; narrow at the render boundary.
  const status = (data ?? null) as MfaStatusResponse | null;
  const enabled = status?.enabled ?? false;

  if (enrolling) {
    return (
      <MfaEnrollFlow
        onDone={() => {
          setEnrolling(false);
        }}
        onCancel={() => {
          setEnrolling(false);
        }}
      />
    );
  }

  const openConfirm = (action: ConfirmAction) => {
    setError(null);
    setPassword("");
    setRegeneratedCodes(null);
    setConfirmAction(action);
  };

  const handleConfirmSubmit = () => {
    if (confirmAction === null) {
      return;
    }
    setError(null);
    const onError = (err: unknown) => {
      setError(problemDetail(err, CONFIRM_FAILED));
    };
    const closePanel = () => {
      setConfirmAction(null);
      setPassword("");
    };
    if (confirmAction === "disable") {
      disable.mutate(password, { onSuccess: closePanel, onError });
    } else {
      regenerate.mutate(password, {
        onSuccess: (payload) => {
          // Reveal the freshly minted codes once: the old codes are now invalid,
          // so the user must save these. The factory's onSuccess invalidates
          // `['mfa', 'status']` so the card stays Enabled with the new count.
          const result = payload as MfaRegenerateBackupCodesResponse;
          setRegeneratedCodes(result.codes ?? []);
          closePanel();
        },
        onError,
      });
    }
  };

  return (
    <Card>
      {enabled ? (
        <EnabledCard
          backupCodeCount={status?.backupCodeCount ?? 0}
          onDisable={() => {
            openConfirm("disable");
          }}
          onRegenerate={() => {
            openConfirm("regenerate");
          }}
        />
      ) : (
        <DisabledCard
          onEnable={() => {
            setEnrolling(true);
          }}
        />
      )}

      {confirmAction === null ? null : (
        <ConfirmPanel
          password={password}
          onPasswordChange={setPassword}
          onSubmit={handleConfirmSubmit}
        />
      )}

      {regeneratedCodes === null ? null : <RegeneratedCodes codes={regeneratedCodes} />}

      {error === null ? null : <ErrorBanner data-testid="settings-mfa-error">{error}</ErrorBanner>}
    </Card>
  );
}

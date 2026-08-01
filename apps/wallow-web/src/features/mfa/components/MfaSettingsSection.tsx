/**
 * MFA settings status card (Wallow-8w1h.6.4) — the status + actions card that
 * lives alongside the profile section on the settings route.
 *
 * Drives `useQuery(mfaGetStatusOptions(...))` and renders:
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
import { errorText } from "@bc-solutions-coder/forms";
import { useMutation, useQuery, useQueryClient } from "@bc-solutions-coder/query";
import {
  Badge,
  Button,
  Card,
  CardTitle,
  ErrorBanner,
  Field,
  Input,
  Label,
  MutedText,
  Text,
} from "@bc-solutions-coder/ui";
import { useRouteContext } from "@tanstack/react-router";
import { useState, type ReactNode } from "react";

import {
  mfaDisableMutation,
  mfaGetStatusOptions,
  mfaGetStatusQueryKey,
  mfaRegenerateBackupCodesMutation,
  queriesForOperation,
} from "../api";
import { problemDetail } from "../errors";
import { MfaEnrollFlow } from "./MfaEnrollFlow";

/** Which enabled-only action opened the shared password-confirm panel. */
type ConfirmAction = "disable" | "regenerate";

const CONFIRM_FAILED = "Unable to complete that action.";

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

/** Shared password-confirm panel reused by both the disable and regenerate flows. */
function ConfirmPanel(props: {
  password: string;
  onPasswordChange: (value: string) => void;
  onSubmit: () => void;
}) {
  const { password, onPasswordChange, onSubmit } = props;
  return (
    <div className={`${PANEL} bg-muted space-y-3`}>
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
 * shown in a "New Backup Codes" panel. Shown after a
 * successful regenerate because the old codes are now invalidated.
 */
function RegeneratedCodes(props: { codes: string[] }) {
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
  const { sdk } = useRouteContext({ from: "__root__" });
  const queryClient = useQueryClient();
  // The status READ's rejection is `statusError`; the plain `error` below is the
  // MUTATIONS' copy, which is a different failure with a different surface.
  const {
    data,
    isPending,
    isError,
    error: statusError,
  } = useQuery(mfaGetStatusOptions({ client: sdk.client }));
  // Both writes change the status the card renders (enrollment state and the
  // remaining-code count), so each re-reads the status OPERATION. Generated keys
  // are flat, so there is no `['mfa']` prefix to sweep by; the status query is
  // tagged `Identity`, which is far broader than these two writes touch.
  const invalidateStatus = (): void => {
    void queryClient.invalidateQueries(
      queriesForOperation(mfaGetStatusQueryKey({ client: sdk.client })),
    );
  };
  const disable = useMutation({
    ...mfaDisableMutation({ client: sdk.client }),
    onSuccess: invalidateStatus,
  });
  const regenerate = useMutation({
    ...mfaRegenerateBackupCodesMutation({ client: sdk.client }),
    onSuccess: invalidateStatus,
  });

  const [enrolling, setEnrolling] = useState(false);
  const [confirmAction, setConfirmAction] = useState<ConfirmAction | null>(null);
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [regeneratedCodes, setRegeneratedCodes] = useState<string[] | null>(null);

  if (isPending) {
    return (
      <MutedText data-testid="settings-mfa-loading" className="text-center py-12">
        Loading MFA status…
      </MutedText>
    );
  }

  // `enabled = status?.enabled ?? false` would otherwise make a failed read look
  // like a confirmed "MFA is off" and invite a second enrolment, so the card
  // refuses to claim a state it does not have — unless a cached status survives
  // the failure, which is still the truth as of the last successful read.
  if (isError && data === undefined) {
    return (
      <ErrorBanner data-testid="settings-mfa-status-error">
        {errorText(statusError, "Could not load your MFA status.")}
      </ErrorBanner>
    );
  }

  // The generated read already resolves `MfaStatusResponse`, so there is nothing
  // left to narrow at the render boundary — only the not-yet-loaded case.
  const status = data ?? null;
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
      disable.mutate({ body: { password } }, { onSuccess: closePanel, onError });
    } else {
      regenerate.mutate(
        { body: { password } },
        {
          onSuccess: (payload) => {
            // Reveal the freshly minted codes once: the old codes are now
            // invalid, so the user must save these. The mutation's own
            // onSuccess re-reads the status so the card stays Enabled with the
            // new count.
            setRegeneratedCodes(payload.codes);
            closePanel();
          },
          onError,
        },
      );
    }
  };

  return (
    <Card className="mt-6">
      <CardTitle>Multi-Factor Authentication</CardTitle>

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

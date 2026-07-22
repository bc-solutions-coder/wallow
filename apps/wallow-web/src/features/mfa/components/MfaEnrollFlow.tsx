/**
 * MFA enroll flow component (Wallow-8w1h.6.4) — the multi-step TOTP enrollment
 * UX, mirroring the Blazor oracle
 * `api/src/Wallow.Auth/Components/Pages/MfaEnroll.razor` (single component, a
 * small step machine). In wallow-web the whole SPA is same-origin via the BFF
 * proxy, so there is NO cross-app EnrollToken redirect dance — this component
 * calls `postV1IdentityMfaEnrollTotp` directly through the facade.
 *
 * STEP MACHINE (strict order, derived from local state):
 *   "setup"  — the `mfa-enroll-begin-setup` CTA is shown; clicking it runs the
 *              `enrollTotp` mutation to mint the one-time secret + QR.
 *   "verify" — once the secret exists, `mfa-enroll-secret` + `mfa-enroll-qr`
 *              (QR from the `qrUri`) + `mfa-enroll-code` (input) + `mfa-enroll-
 *              submit` are shown; submit runs the `confirmEnroll` mutation.
 *   "done"   — on a `{ succeeded: true }` confirm, the one-time
 *              `mfa-enroll-backup-codes` (one child per code) are revealed ONCE
 *              with a Done action; status is invalidated so the card flips to
 *              Enabled.
 *
 * `mfa-enroll-error` surfaces any step's failure (RFC 7807 `detail` on a thrown
 * ProblemDetails, or the mapped `error` code when confirm returns
 * `{ succeeded: false }`); `mfa-enroll-cancel` is always visible.
 *
 * Testids mirror the C# E2E page object `MfaEnrollPage`.
 */
import { Button, Card, CardTitle, ErrorBanner, Field, Input, Label } from "@bc-solutions-coder/ui";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";

import { confirmEnrollMutation, enrollTotpMutation } from "../api";
import { mapMfaError, problemDetail } from "../errors";
import type { MfaConfirmResponse, MfaEnrollResponse } from "../types";

/** Props: `onDone` fires after the backup codes are acknowledged; `onCancel` backs out. */
export interface MfaEnrollFlowProps {
  onDone?: () => void;
  onCancel?: () => void;
}

/** Fallback copy when a thrown error carries neither a ProblemDetails `detail` nor a known code. */
const ENROLL_FAILED = "Unable to start MFA enrollment.";
const CONFIRM_FAILED = "That verification code is not valid.";

/** The setup CTA (initial state): clicking it mints the one-time secret + QR. */
function SetupStep(props: { onBegin: () => void }) {
  return (
    <Button type="button" data-testid="mfa-enroll-begin-setup" onClick={props.onBegin}>
      Begin setup
    </Button>
  );
}

/** The verify step: shows the secret + QR and collects the confirmation code. */
function VerifyStep(props: {
  secret: string;
  qrUri: string;
  code: string;
  onCodeChange: (value: string) => void;
  onSubmit: () => void;
}) {
  const { secret, qrUri, code, onCodeChange, onSubmit } = props;
  return (
    <div>
      <span data-testid="mfa-enroll-secret">{secret}</span>
      <div data-testid="mfa-enroll-qr">
        <code>{qrUri}</code>
      </div>
      <Field>
        <Label htmlFor="mfa-enroll-code-input">Verification code</Label>
        <Input
          id="mfa-enroll-code-input"
          data-testid="mfa-enroll-code"
          value={code}
          onChange={(e) => {
            onCodeChange(e.target.value);
          }}
        />
      </Field>
      <Button type="button" data-testid="mfa-enroll-submit" onClick={onSubmit}>
        Verify
      </Button>
    </div>
  );
}

/** The done step: the one-time backup codes reveal (one child per code) + Done. */
function DoneStep(props: { codes: string[]; onDone: () => void }) {
  const { codes, onDone } = props;
  return (
    <div>
      <p>Save your backup codes now. They will not be shown again.</p>
      <ul data-testid="mfa-enroll-backup-codes">
        {codes.map((codeValue) => (
          <li key={codeValue}>{codeValue}</li>
        ))}
      </ul>
      <Button type="button" data-testid="mfa-enroll-done" onClick={onDone}>
        Done
      </Button>
    </div>
  );
}

export function MfaEnrollFlow(props: MfaEnrollFlowProps) {
  const { onDone, onCancel } = props;
  const queryClient = useQueryClient();
  const enroll = useMutation(enrollTotpMutation());
  const confirm = useMutation(confirmEnrollMutation(queryClient));

  const [secret, setSecret] = useState<string | null>(null);
  const [qrUri, setQrUri] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [backupCodes, setBackupCodes] = useState<string[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleBegin = () => {
    setError(null);
    enroll.mutate(undefined, {
      onSuccess: (data) => {
        const result = data as MfaEnrollResponse;
        setSecret(result.secret);
        setQrUri(result.qrUri);
      },
      onError: (err) => {
        setError(problemDetail(err, ENROLL_FAILED));
      },
    });
  };

  const handleSubmit = () => {
    if (secret === null) {
      return;
    }
    setError(null);
    confirm.mutate(
      { secret, code },
      {
        onSuccess: (data) => {
          const result = data as MfaConfirmResponse;
          if (result.succeeded) {
            // One-time reveal: hold the codes locally, drop the secret so the
            // verify step is gone. The factory's own onSuccess invalidates
            // `['mfa', 'status']` so the card flips to Enabled.
            setBackupCodes(result.backupCodes ?? []);
            setSecret(null);
          } else {
            // Resolved-but-rejected: inspect `succeeded`, not `isError`.
            setError(mapMfaError(result.error) ?? CONFIRM_FAILED);
          }
        },
        onError: (err) => {
          setError(problemDetail(err, CONFIRM_FAILED));
        },
      },
    );
  };

  const handleDone = () => {
    onDone?.();
  };

  const handleCancel = () => {
    onCancel?.();
  };

  return (
    <Card>
      <CardTitle>Set up two-factor authentication</CardTitle>
      {renderStep()}
      {error === null ? null : <ErrorBanner data-testid="mfa-enroll-error">{error}</ErrorBanner>}
      <Button
        type="button"
        variant="secondary"
        data-testid="mfa-enroll-cancel"
        onClick={handleCancel}
      >
        Cancel
      </Button>
    </Card>
  );

  function renderStep() {
    if (backupCodes !== null) {
      return <DoneStep codes={backupCodes} onDone={handleDone} />;
    }
    if (secret !== null) {
      return (
        <VerifyStep
          secret={secret}
          qrUri={qrUri ?? ""}
          code={code}
          onCodeChange={setCode}
          onSubmit={handleSubmit}
        />
      );
    }
    return <SetupStep onBegin={handleBegin} />;
  }
}
